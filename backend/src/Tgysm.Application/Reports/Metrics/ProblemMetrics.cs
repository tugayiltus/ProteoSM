using Microsoft.EntityFrameworkCore;
using Tgysm.Domain.Problems;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Reports.Metrics;

/// <summary>
/// The figures of the problem report (ADR-0034). <see cref="KnownErrorsOpen"/> is a snapshot and ignores the period;
/// <see cref="Resolved"/> and <see cref="ResolutionTime"/> date problems by their resolution (cancelled problems never count);
/// <see cref="ByImpact"/> and <see cref="IncidentsPerProblem"/> date them by creation.
/// </summary>
public static class ProblemMetrics
{
    public const string KnownErrorsOpen = "known_errors_open";
    public const string Resolved = "resolved";
    public const string ResolutionTime = "resolution_time";
    public const string ByImpact = "by_impact";
    public const string IncidentsPerProblem = "incidents_per_problem";
    public const string ByService = MetricKeys.ByService;

    public const string OneIncident = "1";
    public const string TwoToFiveIncidents = "2_5";
    public const string SixPlusIncidents = "6_plus";

    private const int TwoIncidents = 2;
    private const int FiveIncidents = 5;
    private const int SixIncidents = 6;

    private static readonly Level[] ImpactOrder = [Level.Low, Level.Medium, Level.High];

    /// <summary>The number of linked incidents a problem needs to fall into a bucket; <c>To</c> null is open-ended.</summary>
    private sealed record IncidentBand(string Key, int From, int? To);

    private static readonly IncidentBand[] IncidentBands =
    [
        new(OneIncident, 1, 1),
        new(TwoToFiveIncidents, TwoIncidents, FiveIncidents),
        new(SixPlusIncidents, SixIncidents, null),
    ];

    internal static IReadOnlyList<IReportMetric> All { get; } =
        [new KnownErrorsOpenMetric(), new ResolvedMetric(), new ResolutionTimeMetric(), new ImpactMetric(), new IncidentsPerProblemMetric(), new ServiceMetric()];

    private static ReportFigure CountFigure(string key, long count) => new(key, MetricShape.Count, count, null, null, null, MetricKeys.UnitProblems);

    /// <summary>Problems that are a known error right now.</summary>
    private sealed class KnownErrorsOpenMetric : ProblemReportMetric
    {
        public override string Key => KnownErrorsOpen;

        public override ReportName Report => ReportName.Problems;

        public override ReportDateBasis DateBasis => ReportDateBasis.None;

        public override MetricShape Shape => MetricShape.Count;

        public override IQueryable<Problem> Rows(IQueryable<Problem> filtered, ReportContext context) =>
            filtered.Where(p => p.Status == ProblemStatus.KnownError);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Problem> filtered, ReportContext context, CancellationToken cancellationToken) =>
            CountFigure(Key, await Rows(filtered, context).LongCountAsync(cancellationToken));
    }

    /// <summary>Problems resolved in the period.</summary>
    private sealed class ResolvedMetric : ProblemReportMetric
    {
        public override string Key => Resolved;

        public override ReportName Report => ReportName.Problems;

        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

        public override MetricShape Shape => MetricShape.Count;

        public override IQueryable<Problem> Rows(IQueryable<Problem> filtered, ReportContext context) =>
            filtered.Where(p => p.Status == ProblemStatus.Resolved);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Problem> filtered, ReportContext context, CancellationToken cancellationToken) =>
            CountFigure(Key, await Rows(filtered, context).LongCountAsync(cancellationToken));
    }

    /// <summary>
    /// Creation-to-resolution time of problems resolved in the period. Same shape as the other duration figures: the value is the average
    /// in seconds, the denominator the number of problems, and the <see cref="MetricKeys.Median"/> bucket the median.
    /// </summary>
    private sealed class ResolutionTimeMetric : ProblemReportMetric
    {
        public override string Key => ResolutionTime;

        public override ReportName Report => ReportName.Problems;

        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

        public override MetricShape Shape => MetricShape.Duration;

        public override IQueryable<Problem> Rows(IQueryable<Problem> filtered, ReportContext context) =>
            filtered.Where(p => p.Status == ProblemStatus.Resolved);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Problem> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var seconds = Rows(filtered, context).Select(p => (long)(p.ResolvedAt!.Value - p.CreatedAt).TotalSeconds);
            var (average, count) = await DurationStats.AverageAsync(seconds, cancellationToken);
            var median = await DurationStats.MedianAsync(seconds, cancellationToken);
            ReportBucket[]? buckets = median is { } middle ? [new ReportBucket(MetricKeys.Median, null, (long)Math.Round(middle))] : null;
            return new ReportFigure(Key, Shape, average, null, count, buckets, MetricKeys.UnitSeconds);
        }
    }

    /// <summary>Problems created in the period by impact; every level is a bucket, empty ones included.</summary>
    private sealed class ImpactMetric : ProblemReportMetric
    {
        public override string Key => ByImpact;

        public override ReportName Report => ReportName.Problems;

        public override ReportDateBasis DateBasis => ReportDateBasis.Created;

        public override MetricShape Shape => MetricShape.Distribution;

        public override IQueryable<Problem> Rows(IQueryable<Problem> filtered, ReportContext context) => filtered;

        public override IQueryable<Problem> BucketRows(IQueryable<Problem> filtered, ReportContext context, string bucket)
        {
            var impact = ImpactOrder.Where(l => ReportFilter.EnumName(l) == bucket).Select(l => (Level?)l).FirstOrDefault()
                ?? throw new NotSupportedException($"Unknown bucket '{bucket}'.");
            return filtered.Where(p => p.Impact == impact);
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Problem> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var counts = await filtered.GroupBy(p => p.Impact)
                .Select(g => new { g.Key, Count = g.LongCount() })
                .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);
            var buckets = ImpactOrder.Select(l => new ReportBucket(ReportFilter.EnumName(l), null, counts.GetValueOrDefault(l))).ToList();
            return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitProblems);
        }
    }

    /// <summary>Problems created in the period by service; the bucket key is the service name, or <c>none</c>.</summary>
    private sealed class ServiceMetric : ProblemReportMetric
    {
        public override string Key => ByService;

        public override ReportName Report => ReportName.Problems;

        public override ReportDateBasis DateBasis => ReportDateBasis.Created;

        public override MetricShape Shape => MetricShape.Distribution;

        public override IQueryable<Problem> Rows(IQueryable<Problem> filtered, ReportContext context) => filtered;

        public override IQueryable<Problem> BucketRows(IQueryable<Problem> filtered, ReportContext context, string bucket) =>
            bucket == OperationsMetrics.None
                ? filtered.Where(p => p.ServiceId == null)
                : filtered.Where(p => context.Db.Services.Any(s => s.Id == p.ServiceId && s.Name == bucket));

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Problem> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var buckets = await ServiceBuckets.CountAsync(filtered.Select(p => p.ServiceId), context.Db, cancellationToken);
            return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitProblems);
        }
    }

    /// <summary>
    /// Problems created in the period by how many incidents are linked to them. Problems without a linked incident are left out, so
    /// the rows are the problems of the three buckets, and every bucket is present even when empty.
    /// </summary>
    private sealed class IncidentsPerProblemMetric : ProblemReportMetric
    {
        public override string Key => IncidentsPerProblem;

        public override ReportName Report => ReportName.Problems;

        public override ReportDateBasis DateBasis => ReportDateBasis.Created;

        public override MetricShape Shape => MetricShape.Distribution;

        public override IQueryable<Problem> Rows(IQueryable<Problem> filtered, ReportContext context) =>
            filtered.Where(p => context.Db.ProblemTicketLinks.Any(l => l.ProblemId == p.Id));

        public override IQueryable<Problem> BucketRows(IQueryable<Problem> filtered, ReportContext context, string bucket)
        {
            var band = IncidentBands.FirstOrDefault(b => b.Key == bucket) ?? throw new NotSupportedException($"Unknown bucket '{bucket}'.");
            var (from, to) = (band.From, band.To);
            var linked = filtered.Where(p => context.Db.ProblemTicketLinks.Count(l => l.ProblemId == p.Id) >= from);
            return to is { } upper ? linked.Where(p => context.Db.ProblemTicketLinks.Count(l => l.ProblemId == p.Id) <= upper) : linked;
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Problem> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var buckets = new List<ReportBucket>();
            foreach (var band in IncidentBands)
            {
                buckets.Add(new ReportBucket(band.Key, null, await BucketRows(filtered, context, band.Key).LongCountAsync(cancellationToken)));
            }

            return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitProblems);
        }
    }
}
