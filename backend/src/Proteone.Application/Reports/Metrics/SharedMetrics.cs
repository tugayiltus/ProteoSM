using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Domain.Reports;
using Proteone.Domain.Sla;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Reports.Metrics;

/// <summary>Metric keys and bucket keys used by more than one report.</summary>
public static class MetricKeys
{
    public const string Volume = "volume";
    public const string SlaCompliance = "sla_compliance";
    public const string ExcludedOutcomes = "excluded_outcomes";
    public const string Calendar = "calendar";
    public const string Sla = "sla";
    public const string Cancelled = "cancelled";
    public const string Median = "median";
    public const string ByService = "by_service";

    public const string UnitTickets = "tickets";
    public const string UnitAssets = "assets";
    public const string UnitChanges = "changes";
    public const string UnitTasks = "tasks";
    public const string UnitProblems = "problems";
    public const string UnitLicences = "licences";
    public const string UnitSeconds = "seconds";
}

/// <summary>Row rules shared by the metrics of the incident and request reports.</summary>
internal static class TicketRules
{
    public static readonly Expression<Func<Ticket, bool>> Completed = t => t.Status == TicketStatus.Completed;

    public static readonly Expression<Func<Ticket, bool>> Open = t => t.Status != TicketStatus.Completed && t.Status != TicketStatus.Cancelled;

    /// <summary>Completed tickets whose SLA counts: not "service request required" and not rejected.</summary>
    public static readonly Expression<Func<Ticket, bool>> SlaEligible =
        t => t.Status == TicketStatus.Completed && t.Outcome != TicketOutcome.ServiceRequestRequired && t.Outcome != TicketOutcome.ApprovalRejected;

    public static Expression<Func<Ticket, bool>> CompletedWith(TicketOutcome outcome) =>
        t => t.Status == TicketStatus.Completed && t.Outcome == outcome;

    /// <summary>Calendar seconds from creation to closing.</summary>
    public static readonly Expression<Func<Ticket, long>> CalendarSeconds = t => (long)(t.CompletedAt!.Value - t.CreatedAt).TotalSeconds;

    /// <summary>The resolution timer's SLA seconds (0 when the ticket has no timer).</summary>
    public static Expression<Func<Ticket, long>> SlaSeconds(ReportContext context)
    {
        var timers = ReportQueries.ResolutionTimers(context.Db);
        return t => timers.Where(i => i.TicketId == t.Id).Select(i => i.ElapsedSeconds).FirstOrDefault();
    }

    public static Task<DurationSums> ResolutionSumsAsync(IQueryable<Ticket> tickets, ReportContext context, CancellationToken cancellationToken) =>
        DurationStats.SumAsync(tickets, CalendarSeconds, SlaSeconds(context), cancellationToken);
}

/// <summary>
/// Counting by service for the <c>by_service</c> figure of the four record reports. A bucket key is the service id (a name such as
/// "none" must not collide with the no-service bucket) or <see cref="OperationsMetrics.None"/> for records without a service; the label
/// is the name, null for none (the UI translates it).
/// </summary>
internal static class ServiceBuckets
{
    public static async Task<List<ReportBucket>> CountAsync(IQueryable<Guid?> serviceIds, IAppDbContext db, CancellationToken cancellationToken)
    {
        var groups = await serviceIds.GroupBy(id => id)
            .Select(g => new { g.Key, Count = g.LongCount() })
            .ToListAsync(cancellationToken);
        var ids = groups.Where(g => g.Key != null).Select(g => g.Key!.Value).ToList();
        var names = await db.Services.Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        return groups
            .Select(g => (Key: g.Key?.ToString() ?? OperationsMetrics.None, Name: g.Key is { } id ? names[id] : null, g.Count))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Name is null)
            .ThenBy(g => g.Name, StringComparer.Ordinal)
            .Select(g => new ReportBucket(g.Key, g.Name, g.Count))
            .ToList();
    }

    /// <summary>The service id a bucket key names, or null for the bucket of records without a service.</summary>
    public static Guid? ServiceId(string bucket) =>
        bucket == OperationsMetrics.None ? null
        : Guid.TryParse(bucket, out var id) ? id
        : throw new NotSupportedException($"Unknown bucket '{bucket}'.");
}

/// <summary>Tickets created in the window, by the service they are set on.</summary>
internal sealed class ServiceMetric(ReportName report) : ReportMetric
{
    public override string Key => MetricKeys.ByService;

    public override ReportName Report => report;

    public override ReportDateBasis DateBasis => ReportDateBasis.Created;

    public override MetricShape Shape => MetricShape.Distribution;

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) => filtered;

    public override IQueryable<Ticket> BucketRows(IQueryable<Ticket> filtered, ReportContext context, string bucket)
    {
        var serviceId = ServiceBuckets.ServiceId(bucket);
        return serviceId is null ? filtered.Where(t => t.ServiceId == null) : filtered.Where(t => t.ServiceId == serviceId);
    }

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
    {
        var buckets = await ServiceBuckets.CountAsync(filtered.Select(t => t.ServiceId), context.Db, cancellationToken);
        return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitTickets);
    }
}

/// <summary>Tickets created in the window.</summary>
internal sealed class VolumeMetric(ReportName report) : ReportMetric
{
    public override string Key => MetricKeys.Volume;

    public override ReportName Report => report;

    public override ReportDateBasis DateBasis => ReportDateBasis.Created;

    public override MetricShape Shape => MetricShape.Count;

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) => filtered;

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
    {
        var count = await filtered.LongCountAsync(cancellationToken);
        return new ReportFigure(Key, Shape, count, null, null, null, MetricKeys.UnitTickets);
    }
}

/// <summary>Average calendar and SLA time from creation to completion of tickets completed with one outcome.</summary>
internal sealed class CompletionTimeMetric(ReportName report, string key, TicketOutcome outcome) : ReportMetric
{
    public override string Key => key;

    public override ReportName Report => report;

    public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

    public override MetricShape Shape => MetricShape.Duration;

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) => filtered.Where(TicketRules.CompletedWith(outcome));

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
    {
        var sums = await TicketRules.ResolutionSumsAsync(Rows(filtered, context), context, cancellationToken);
        return DurationFigure.Create(Key, sums);
    }
}

internal static class DurationFigure
{
    /// <summary>Value is the average calendar seconds, <c>Denominator</c> how many were averaged; the buckets carry the rounded averages.</summary>
    public static ReportFigure Create(string key, DurationSums sums) => new(
        key,
        MetricShape.Duration,
        sums.CalendarAverage,
        null,
        sums.Count,
        sums.Count == 0 ? null : [Bucket(MetricKeys.Calendar, sums.CalendarAverage), Bucket(MetricKeys.Sla, sums.SlaAverage)],
        MetricKeys.UnitSeconds);

    private static ReportBucket Bucket(string key, double? average) => new(key, null, (long)Math.Round(average ?? 0));
}

/// <summary>Resolution timer met ÷ completed tickets whose SLA counts.</summary>
internal sealed class SlaComplianceMetric(ReportName report) : ReportMetric
{
    public override string Key => MetricKeys.SlaCompliance;

    public override ReportName Report => report;

    public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

    public override MetricShape Shape => MetricShape.Ratio;

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context)
    {
        var met = ReportQueries.ResolutionTimers(context.Db).Where(i => i.Result == SlaResult.Met);
        return DenominatorRows(filtered, context)!.Where(t => met.Any(i => i.TicketId == t.Id));
    }

    public override IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) => filtered.Where(TicketRules.SlaEligible);

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken) =>
        ReportFigure.Ratio(Key, await Rows(filtered, context).LongCountAsync(cancellationToken), await DenominatorRows(filtered, context)!.LongCountAsync(cancellationToken));
}

/// <summary>Tickets closed without counting toward SLA, split into two disjoint groups.</summary>
internal sealed class ExcludedOutcomesMetric(
    ReportName report,
    (string Key, Expression<Func<Ticket, bool>> Rule) first,
    (string Key, Expression<Func<Ticket, bool>> Rule) second) : ReportMetric
{
    public override string Key => MetricKeys.ExcludedOutcomes;

    public override ReportName Report => report;

    public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

    public override MetricShape Shape => MetricShape.Distribution;

    public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) =>
        filtered.Where(Or(first.Rule, second.Rule));

    public override IQueryable<Ticket> BucketRows(IQueryable<Ticket> filtered, ReportContext context, string bucket) =>
        filtered.Where(bucket == first.Key ? first.Rule : bucket == second.Key ? second.Rule : throw new NotSupportedException($"Unknown bucket '{bucket}'."));

    public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
    {
        var firstCount = await filtered.Where(first.Rule).LongCountAsync(cancellationToken);
        var secondCount = await filtered.Where(second.Rule).LongCountAsync(cancellationToken);
        return new ReportFigure(
            Key, Shape, firstCount + secondCount, null, null, [new(first.Key, null, firstCount), new(second.Key, null, secondCount)], MetricKeys.UnitTickets);
    }

    private static Expression<Func<Ticket, bool>> Or(Expression<Func<Ticket, bool>> left, Expression<Func<Ticket, bool>> right)
    {
        var ticket = left.Parameters[0];
        var rightBody = new ParameterReplacer(right.Parameters[0], ticket).Visit(right.Body);
        return Expression.Lambda<Func<Ticket, bool>>(Expression.OrElse(left.Body, rightBody), ticket);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
