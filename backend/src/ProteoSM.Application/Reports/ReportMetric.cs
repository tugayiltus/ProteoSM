using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Assets;
using ProteoSM.Domain.Changes;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Reports;
using ProteoSM.Domain.Tasks;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Reports;

/// <summary>
/// What a ticket metric needs besides the filtered tickets: the database (SLA instances, approvals), the current instant and the
/// report's day window, for metrics whose dated rows are not tickets (assignment history, ticket relations). <c>TeamId</c> is the
/// report's team filter, set only for a metric that applies it itself (<see cref="ReportMetric.AppliesTeamFilter"/>).
/// </summary>
public sealed record ReportContext(IAppDbContext Db, DateTimeOffset Now, ReportWindow Window, Guid? TeamId = null);

/// <summary>What every metric states about itself, ticket-based or asset-based.</summary>
public interface IReportMetric
{
    string Key { get; }

    ReportName Report { get; }

    ReportDateBasis DateBasis { get; }

    MetricShape Shape { get; }
}

/// <summary>A metric over tickets. <see cref="Rows"/> are the drill-down/export rows (a ratio's numerator rows).</summary>
public abstract class ReportMetric : IReportMetric
{
    public abstract string Key { get; }

    public abstract ReportName Report { get; }

    public abstract ReportDateBasis DateBasis { get; }

    public abstract MetricShape Shape { get; }

    /// <summary>
    /// True when the metric applies the report's team filter to its own rows (<see cref="ReportContext.TeamId"/>); the pipeline then
    /// leaves the team off the filtered tickets. Rating figures use this, because they count under the team copied at rating time.
    /// </summary>
    public virtual bool AppliesTeamFilter => false;

    public abstract IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context);

    /// <summary>The <see cref="Rows"/> of one bucket of a distribution; its count equals the bucket's count. Only distributions have buckets.</summary>
    public virtual IQueryable<Ticket> BucketRows(IQueryable<Ticket> filtered, ReportContext context, string bucket) =>
        throw new NotSupportedException($"Metric '{Key}' has no buckets.");

    /// <summary>Rows behind a ratio's denominator; null for other shapes.</summary>
    public virtual IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) => null;

    public abstract Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken);
}

/// <summary>
/// What an asset metric needs besides the filtered assets: the database (assignments), today in the company zone and the warranty
/// window (<see cref="WarrantyFrom"/>..<see cref="WarrantyTo"/>, inclusive).
/// </summary>
public sealed record AssetReportContext(IAppDbContext Db, DateOnly Today, DateOnly WarrantyFrom, DateOnly WarrantyTo)
{
    /// <summary>
    /// The context of a run at <paramref name="now"/>. The warranty window defaults to today..today + <paramref name="warrantyWindowDays"/>;
    /// its start never lies before today, so "expiring" and "expired" never share an asset.
    /// </summary>
    public static AssetReportContext Create(IAppDbContext db, ReportFilter filter, DateTimeOffset now, TimeZoneInfo zone, int warrantyWindowDays)
    {
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
        var from = filter.From is { } start && start > today ? start : today;
        var to = filter.To ?? today.AddDays(warrantyWindowDays);
        return new AssetReportContext(db, today, from, to);
    }
}

/// <summary>A metric over assets (asset and warranty reports).</summary>
public abstract class AssetReportMetric : IReportMetric
{
    public abstract string Key { get; }

    public abstract ReportName Report { get; }

    public virtual ReportDateBasis DateBasis => ReportDateBasis.Created;

    public abstract MetricShape Shape { get; }

    public abstract IQueryable<Asset> Rows(IQueryable<Asset> filtered, AssetReportContext context);

    /// <summary>The <see cref="Rows"/> of one bucket of a distribution; its count equals the bucket's count. Only distributions have buckets.</summary>
    public virtual IQueryable<Asset> BucketRows(IQueryable<Asset> filtered, AssetReportContext context, string bucket) =>
        throw new NotSupportedException($"Metric '{Key}' has no buckets.");

    public abstract Task<ReportFigure> ComputeAsync(IQueryable<Asset> filtered, AssetReportContext context, CancellationToken cancellationToken);
}

/// <summary>A metric over change requests; same shape as <see cref="ReportMetric"/>, with the viewer's filtered changes as the base rows.</summary>
public abstract class ChangeReportMetric : IReportMetric
{
    public abstract string Key { get; }

    public abstract ReportName Report { get; }

    public abstract ReportDateBasis DateBasis { get; }

    public abstract MetricShape Shape { get; }

    public abstract IQueryable<ChangeRequest> Rows(IQueryable<ChangeRequest> filtered, ReportContext context);

    /// <summary>The <see cref="Rows"/> of one bucket of a distribution; its count equals the bucket's count. Only distributions have buckets.</summary>
    public virtual IQueryable<ChangeRequest> BucketRows(IQueryable<ChangeRequest> filtered, ReportContext context, string bucket) =>
        throw new NotSupportedException($"Metric '{Key}' has no buckets.");

    /// <summary>Rows behind a ratio's denominator; null for other shapes.</summary>
    public virtual IQueryable<ChangeRequest>? DenominatorRows(IQueryable<ChangeRequest> filtered, ReportContext context) => null;

    public abstract Task<ReportFigure> ComputeAsync(IQueryable<ChangeRequest> filtered, ReportContext context, CancellationToken cancellationToken);
}

/// <summary>What a task metric needs besides the filtered tasks: the database, the current instant, the day window and today in the company zone.</summary>
public sealed record TaskReportContext(IAppDbContext Db, DateTimeOffset Now, ReportWindow Window, DateOnly Today);

/// <summary>A metric over work tasks; same shape as <see cref="ChangeReportMetric"/>, with the viewer's filtered tasks as the base rows.</summary>
public abstract class TaskReportMetric : IReportMetric
{
    public abstract string Key { get; }

    public abstract ReportName Report { get; }

    public abstract ReportDateBasis DateBasis { get; }

    public abstract MetricShape Shape { get; }

    public abstract IQueryable<WorkTask> Rows(IQueryable<WorkTask> filtered, TaskReportContext context);

    /// <summary>The <see cref="Rows"/> of one bucket of a distribution; its count equals the bucket's count. Only distributions have buckets.</summary>
    public virtual IQueryable<WorkTask> BucketRows(IQueryable<WorkTask> filtered, TaskReportContext context, string bucket) =>
        throw new NotSupportedException($"Metric '{Key}' has no buckets.");

    /// <summary>Rows behind a ratio's denominator; null for other shapes.</summary>
    public virtual IQueryable<WorkTask>? DenominatorRows(IQueryable<WorkTask> filtered, TaskReportContext context) => null;

    public abstract Task<ReportFigure> ComputeAsync(IQueryable<WorkTask> filtered, TaskReportContext context, CancellationToken cancellationToken);
}

/// <summary>A metric over problems; same shape as <see cref="ChangeReportMetric"/>, with the viewer's filtered problems as the base rows.</summary>
public abstract class ProblemReportMetric : IReportMetric
{
    public abstract string Key { get; }

    public abstract ReportName Report { get; }

    public abstract ReportDateBasis DateBasis { get; }

    public abstract MetricShape Shape { get; }

    public abstract IQueryable<Problem> Rows(IQueryable<Problem> filtered, ReportContext context);

    /// <summary>The <see cref="Rows"/> of one bucket of a distribution; its count equals the bucket's count. Only distributions have buckets.</summary>
    public virtual IQueryable<Problem> BucketRows(IQueryable<Problem> filtered, ReportContext context, string bucket) =>
        throw new NotSupportedException($"Metric '{Key}' has no buckets.");

    /// <summary>Rows behind a ratio's denominator; null for other shapes.</summary>
    public virtual IQueryable<Problem>? DenominatorRows(IQueryable<Problem> filtered, ReportContext context) => null;

    public abstract Task<ReportFigure> ComputeAsync(IQueryable<Problem> filtered, ReportContext context, CancellationToken cancellationToken);
}
