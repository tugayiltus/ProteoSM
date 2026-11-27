using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Domain.Reports;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Reports.Metrics;

/// <summary>
/// Satisfaction figures of the incident and request reports, dated by completion. They apply the report's team filter themselves:
/// a rated ticket counts under <c>ticket_ratings.team_id</c> (the team when it was rated), so a later move does not shift the score.
/// A rateable ticket without a rating has no such copy and counts under its current team.
/// </summary>
public static class RatingMetrics
{
    public const string Average = "rating_average";
    public const string ResponseRate = "rating_response_rate";
    public const string Distribution = "rating_distribution";

    /// <summary>Bucket keys of <see cref="Distribution"/>: every score as text ("1".."5"), lowest first.</summary>
    public static IReadOnlyList<string> DistributionBuckets { get; } = [.. Enumerable
        .Range(RatingLimits.MinScore, RatingLimits.MaxScore - RatingLimits.MinScore + 1)
        .Select(score => score.ToString(CultureInfo.InvariantCulture))];

    private static readonly IReadOnlyDictionary<string, int> ScoreOfBucket =
        DistributionBuckets.ToDictionary(key => key, key => int.Parse(key, CultureInfo.InvariantCulture), StringComparer.Ordinal);

    internal static IReadOnlyList<IReportMetric> All { get; } = [.. For(ReportName.Incidents), .. For(ReportName.Requests)];

    private static IEnumerable<IReportMetric> For(ReportName report) =>
        [new AverageMetric(report), new ResponseRateMetric(report), new DistributionMetric(report)];

    /// <summary>Completed tickets with a rateable outcome, inside the team filter (rating team if rated, else current team).</summary>
    private static IQueryable<Ticket> Rateable(IQueryable<Ticket> filtered, ReportContext context)
    {
        var ratings = context.Db.TicketRatings;
        var rateable = filtered.Where(t =>
            t.Status == TicketStatus.Completed && t.Outcome != null && TicketRating.RateableOutcomes.Contains(t.Outcome.Value));
        return context.TeamId is { } teamId
            ? rateable.Where(t =>
                ratings.Any(r => r.TicketId == t.Id && r.TeamId == teamId) || (t.TeamId == teamId && !ratings.Any(r => r.TicketId == t.Id)))
            : rateable;
    }

    private static IQueryable<Ticket> Rated(IQueryable<Ticket> filtered, ReportContext context)
    {
        var ratings = context.Db.TicketRatings;
        return Rateable(filtered, context).Where(t => ratings.Any(r => r.TicketId == t.Id));
    }

    private static IQueryable<TicketRating> RatingsOf(IQueryable<Ticket> rows, ReportContext context) =>
        context.Db.TicketRatings.Where(r => rows.Any(t => t.Id == r.TicketId));

    private abstract class RatingMetric(ReportName report) : ReportMetric
    {
        public override ReportName Report => report;

        public override ReportDateBasis DateBasis => ReportDateBasis.Completed;

        public override bool AppliesTeamFilter => true;

        public override IQueryable<Ticket> Rows(IQueryable<Ticket> filtered, ReportContext context) => Rated(filtered, context);
    }

    /// <summary>Sum of scores ÷ rated tickets; <c>Numerator</c> is the score sum, so it is not a row count.</summary>
    private sealed class AverageMetric(ReportName report) : RatingMetric(report)
    {
        public override string Key => Average;

        public override MetricShape Shape => MetricShape.Ratio;

        public override IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) => Rows(filtered, context);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var scores = RatingsOf(Rows(filtered, context), context).Select(r => (long)r.Score);
            var sum = await scores.SumAsync(cancellationToken);
            var count = await scores.LongCountAsync(cancellationToken);
            return ReportFigure.Ratio(Key, sum, count, MetricKeys.UnitScore);
        }
    }

    /// <summary>Rated ÷ rateable tickets completed in the window.</summary>
    private sealed class ResponseRateMetric(ReportName report) : RatingMetric(report)
    {
        public override string Key => ResponseRate;

        public override MetricShape Shape => MetricShape.Ratio;

        public override IQueryable<Ticket>? DenominatorRows(IQueryable<Ticket> filtered, ReportContext context) => Rateable(filtered, context);

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken) =>
            ReportFigure.Ratio(Key, await Rows(filtered, context).LongCountAsync(cancellationToken), await Rateable(filtered, context).LongCountAsync(cancellationToken));
    }

    /// <summary>Rated tickets by score; every score from the lowest to the highest has a bucket, also when it is empty.</summary>
    private sealed class DistributionMetric(ReportName report) : RatingMetric(report)
    {
        public override string Key => Distribution;

        public override MetricShape Shape => MetricShape.Distribution;

        public override IQueryable<Ticket> BucketRows(IQueryable<Ticket> filtered, ReportContext context, string bucket)
        {
            if (!ScoreOfBucket.TryGetValue(bucket, out var score))
            {
                throw new NotSupportedException($"Unknown bucket '{bucket}'.");
            }

            var ratings = context.Db.TicketRatings;
            return Rows(filtered, context).Where(t => ratings.Any(r => r.TicketId == t.Id && r.Score == score));
        }

        public override async Task<ReportFigure> ComputeAsync(IQueryable<Ticket> filtered, ReportContext context, CancellationToken cancellationToken)
        {
            var counts = await RatingsOf(Rows(filtered, context), context)
                .GroupBy(r => r.Score)
                .Select(g => new { Score = g.Key, Count = g.LongCount() })
                .ToDictionaryAsync(g => g.Score, g => g.Count, cancellationToken);
            var buckets = DistributionBuckets.Select(key => new ReportBucket(key, null, counts.GetValueOrDefault(ScoreOfBucket[key]))).ToList();
            return new ReportFigure(Key, Shape, buckets.Sum(b => b.Count), null, null, buckets, MetricKeys.UnitTickets);
        }
    }
}
