using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Domain.Licences;
using Proteone.Domain.Reports;

namespace Proteone.Application.Reports.Metrics;

/// <summary>What a licence metric needs: the database (seats) and today in the company zone.</summary>
public sealed record LicenceReportContext(IAppDbContext Db, DateOnly Today);

/// <summary>A count over licences, a snapshot of now (ADR-0039).</summary>
public sealed class LicenceReportMetric(string key, Func<IQueryable<Licence>, LicenceReportContext, IQueryable<Licence>> rows) : IReportMetric
{
    public string Key => key;

    public ReportName Report => ReportName.Licences;

    public ReportDateBasis DateBasis => ReportDateBasis.None;

    public MetricShape Shape => MetricShape.Count;

    public IQueryable<Licence> Rows(IQueryable<Licence> filtered, LicenceReportContext context) => rows(filtered, context);

    public async Task<ReportFigure> ComputeAsync(IQueryable<Licence> filtered, LicenceReportContext context, CancellationToken cancellationToken) =>
        new(Key, Shape, await Rows(filtered, context).LongCountAsync(cancellationToken), null, null, null, MetricKeys.UnitLicences);
}

/// <summary>
/// The figures of the licence report. The rows of <see cref="All"/> show the use of every licence. The "ending" windows
/// start today and include each other; a licence that ended earlier is only in <see cref="Expired"/>.
/// </summary>
public static class LicenceMetrics
{
    public const string AllLicences = "licences_all";
    public const string Full = "licences_full";
    public const string Ending30 = "licences_ending_30";
    public const string Ending60 = "licences_ending_60";
    public const string Ending90 = "licences_ending_90";
    public const string Expired = "licences_expired";

    private const int Days30 = 30;
    private const int Days60 = 60;
    private const int Days90 = 90;

    internal static IReadOnlyList<IReportMetric> All { get; } =
    [
        new LicenceReportMetric(AllLicences, (licences, _) => licences),
        new LicenceReportMetric(Full, (licences, context) =>
            licences.Where(l => context.Db.LicenceAssignments.Count(a => a.LicenceId == l.Id && a.ReleasedAt == null) >= l.Quantity)),
        Ending(Ending30, Days30),
        Ending(Ending60, Days60),
        Ending(Ending90, Days90),
        new LicenceReportMetric(Expired, (licences, context) => licences.Where(l => l.ExpiresOn < context.Today)),
    ];

    private static LicenceReportMetric Ending(string key, int days) => new(key, (licences, context) =>
    {
        var to = context.Today.AddDays(days);
        return licences.Where(l => l.ExpiresOn >= context.Today && l.ExpiresOn <= to);
    });
}
