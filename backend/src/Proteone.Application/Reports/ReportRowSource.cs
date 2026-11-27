using Microsoft.EntityFrameworkCore;
using FluentValidation;
using FluentValidation.Results;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Application.Reports.Metrics;
using Proteone.Domain.Assets;
using Proteone.Domain.Changes;
using Proteone.Domain.Common;
using Proteone.Domain.Licences;
using Proteone.Domain.Problems;
using Proteone.Domain.Reports;
using Proteone.Domain.Tasks;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Reports;

public enum DrilldownKind
{
    Tickets,
    Assets,
    Changes,
    Tasks,
    Problems,
    Licences,
}

public sealed record DrilldownTicketRow(
    string Number, TicketKind Kind, string Subject, TicketStatus Status, Priority Priority, string TeamName, string? AssigneeName,
    DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, string? ServiceName);

public sealed record DrilldownAssetRow(
    Guid Id, string AssetTag, AssetType Type, string Brand, string Model, AssetStatus Status, string LocationName, DateOnly? WarrantyEndsOn);

public sealed record DrilldownChangeRow(
    string Number, string Title, ChangeStatus Status, ChangeOutcome? Outcome, Level Risk, string OwnerTeamName, DateTimeOffset? WindowStart, DateTimeOffset? ActualEnd, string? ServiceName);

public sealed record DrilldownTaskRow(
    string Number, string Title, WorkTaskStatus Status, Priority Priority, string TeamName, string? AssigneeName, DateOnly? DueDate,
    DateTimeOffset CreatedAt, DateTimeOffset? ClosedAt);

public sealed record DrilldownProblemRow(
    string Number, string Title, ProblemStatus Status, Level Impact, string OwnerTeamName, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt, string? ServiceName);

public sealed record DrilldownLicenceRow(
    Guid Id, string Vendor, string SoftwareName, string Name, LicenceKind Kind, string OwnerTeamName, int Used, int Quantity, DateOnly? ExpiresOn);

/// <summary>The ordered rows behind one figure: exactly one list is set, the one matching <see cref="Kind"/>.</summary>
public sealed record DrilldownRows(
    DrilldownKind Kind, IQueryable<DrilldownTicketRow>? Tickets, IQueryable<DrilldownAssetRow>? Assets,
    IQueryable<DrilldownChangeRow>? Changes, IQueryable<DrilldownTaskRow>? Tasks, IQueryable<DrilldownProblemRow>? Problems)
{
    public IQueryable<DrilldownLicenceRow>? Licences { get; init; }
}

/// <summary>
/// The rows behind a report figure, ordered so any page of them is stable. The drill-down endpoint pages this query; the Excel
/// export reads the same one, so a figure, its drill-down and its export always list the same rows.
/// </summary>
public sealed class ReportRowSource(ReportScope scope, TicketReportSource source, IAppDbContext db, TimeProvider clock, ICompanyClock companyClock)
{
    private const string MetricField = "metric";
    private const string PartField = "part";
    private const string BucketField = "bucket";
    private const string NameSeparator = " ";

    /// <summary>
    /// The rows of <paramref name="metricKey"/> of <paramref name="report"/> for <paramref name="viewer"/>: the metric's numerator rows,
    /// a ratio's denominator rows, or the rows of one distribution <paramref name="bucket"/>. An unknown metric, a denominator on a
    /// non-ratio, or an unknown or unsupported bucket is a 400 field error.
    /// </summary>
    public async Task<DrilldownRows> OpenAsync(
        ReportViewer viewer, ReportName report, string metricKey, string? bucket, ReportDrillPart part, ReportFilter filter, CancellationToken cancellationToken)
    {
        filter.EnsureValid();
        var metric = ReportCatalog.Find(report, metricKey);
        var now = clock.GetUtcNow();
        if (metric is AssetReportMetric assetMetric)
        {
            return OpenAssets(viewer, assetMetric, bucket, part, filter, now, await db.SystemSettings.Select(s => s.WarrantyWindowDays).SingleAsync(cancellationToken));
        }

        if (metric is ChangeReportMetric changeMetric)
        {
            return OpenChanges(viewer, changeMetric, bucket, part, filter, now);
        }

        if (metric is TaskReportMetric taskMetric)
        {
            return OpenTasks(viewer, taskMetric, bucket, part, filter, now);
        }

        if (metric is LicenceReportMetric licenceMetric)
        {
            return OpenLicences(viewer, licenceMetric, bucket, part, filter, now);
        }

        if (metric is ProblemReportMetric problemMetric)
        {
            return OpenProblems(viewer, problemMetric, bucket, part, filter, now);
        }

        var ticketMetric = (ReportMetric)metric;
        var pipeline = ReportPipeline.Tickets(scope, source, db, viewer, report, filter, ticketMetric, now);
        var rows = TicketRows(ticketMetric, pipeline.Filtered, pipeline.Context, bucket, part);
        return new DrilldownRows(DrilldownKind.Tickets, ProjectTickets(rows), null, null, null, null);
    }

    private static IQueryable<Ticket> TicketRows(ReportMetric metric, IQueryable<Ticket> filtered, ReportContext context, string? bucket, ReportDrillPart part)
    {
        if (part == ReportDrillPart.Denominator)
        {
            if (bucket is not null)
            {
                throw Invalid(BucketField);
            }

            return metric.DenominatorRows(filtered, context) ?? throw Invalid(PartField);
        }

        if (bucket is null)
        {
            return metric.Rows(filtered, context);
        }

        try
        {
            return metric.BucketRows(filtered, context, bucket);
        }
        catch (NotSupportedException)
        {
            throw Invalid(BucketField);
        }
    }

    // Order by creation then id; transfer rows repeat a ticket once per history row, and those repeats are identical, so any page split is the same.
    private IQueryable<DrilldownTicketRow> ProjectTickets(IQueryable<Ticket> rows) =>
        rows.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => new DrilldownTicketRow(
                t.Number, t.Kind, t.Subject, t.Status, t.Priority,
                db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                t.CreatedAt, t.CompletedAt,
                db.Services.Where(s => s.Id == t.ServiceId).Select(s => s.Name).FirstOrDefault()));

    private DrilldownRows OpenAssets(ReportViewer viewer, AssetReportMetric metric, string? bucket, ReportDrillPart part, ReportFilter filter, DateTimeOffset now, int warrantyWindowDays)
    {
        if (part == ReportDrillPart.Denominator)
        {
            throw Invalid(PartField);
        }

        var pipeline = ReportPipeline.Assets(scope, db, companyClock, viewer, filter, now, warrantyWindowDays);
        IQueryable<Asset> rows;
        try
        {
            rows = bucket is null ? metric.Rows(pipeline.Filtered, pipeline.Context) : metric.BucketRows(pipeline.Filtered, pipeline.Context, bucket);
        }
        catch (NotSupportedException)
        {
            throw Invalid(BucketField);
        }

        return new DrilldownRows(DrilldownKind.Assets, null, rows.OrderBy(a => a.AssetTag).ThenBy(a => a.Id)
            .Select(a => new DrilldownAssetRow(
                a.Id, a.AssetTag, a.Type, a.Brand, a.Model, a.Status,
                db.Locations.Where(l => l.Id == a.LocationId).Select(l => l.Name).First(), a.WarrantyEndsOn)),
            null, null, null);
    }

    private DrilldownRows OpenChanges(ReportViewer viewer, ChangeReportMetric metric, string? bucket, ReportDrillPart part, ReportFilter filter, DateTimeOffset now)
    {
        var pipeline = ReportPipeline.Changes(scope, db, companyClock, viewer, filter, metric.DateBasis, now);
        var rows = ChangeRows(metric, pipeline.Filtered, pipeline.Context, bucket, part);
        return new DrilldownRows(DrilldownKind.Changes, null, null, rows.OrderBy(c => c.Number).ThenBy(c => c.Id)
            .Select(c => new DrilldownChangeRow(
                c.Number, c.Title, c.Status, c.Outcome, c.Risk,
                db.Teams.Where(t => t.Id == c.OwnerTeamId).Select(t => t.Name).First(), c.WindowStart, c.ActualEnd,
                db.Services.Where(s => s.Id == c.ServiceId).Select(s => s.Name).FirstOrDefault())),
            null, null);
    }

    private DrilldownRows OpenTasks(ReportViewer viewer, TaskReportMetric metric, string? bucket, ReportDrillPart part, ReportFilter filter, DateTimeOffset now)
    {
        if (part == ReportDrillPart.Denominator)
        {
            throw Invalid(PartField);
        }

        var pipeline = ReportPipeline.Tasks(scope, db, companyClock, viewer, filter, metric.DateBasis, now);
        IQueryable<WorkTask> rows;
        try
        {
            rows = bucket is null ? metric.Rows(pipeline.Filtered, pipeline.Context) : metric.BucketRows(pipeline.Filtered, pipeline.Context, bucket);
        }
        catch (NotSupportedException)
        {
            throw Invalid(BucketField);
        }

        return new DrilldownRows(DrilldownKind.Tasks, null, null, null, rows.OrderBy(t => t.Number).ThenBy(t => t.Id)
            .Select(t => new DrilldownTaskRow(
                t.Number, t.Title, t.Status, t.Priority,
                db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                t.DueDate, t.CreatedAt, t.ClosedAt)),
            null);
    }

    private DrilldownRows OpenLicences(ReportViewer viewer, LicenceReportMetric metric, string? bucket, ReportDrillPart part, ReportFilter filter, DateTimeOffset now)
    {
        if (part == ReportDrillPart.Denominator)
        {
            throw Invalid(PartField);
        }

        if (bucket is not null)
        {
            throw Invalid(BucketField);
        }

        var pipeline = ReportPipeline.Licences(scope, db, companyClock, viewer, filter, now);
        var rows =
            from licence in metric.Rows(pipeline.Filtered, pipeline.Context)
            join software in db.Software on licence.SoftwareId equals software.Id
            orderby software.SearchText, licence.SearchText, licence.Id
            select new DrilldownLicenceRow(
                licence.Id, software.Vendor, software.Name, licence.Name, licence.Kind,
                db.Teams.Where(t => t.Id == licence.OwnerTeamId).Select(t => t.Name).First(),
                db.LicenceAssignments.Count(a => a.LicenceId == licence.Id && a.ReleasedAt == null), licence.Quantity, licence.ExpiresOn);
        return new DrilldownRows(DrilldownKind.Licences, null, null, null, null, null) { Licences = rows };
    }

    private DrilldownRows OpenProblems(ReportViewer viewer, ProblemReportMetric metric, string? bucket, ReportDrillPart part, ReportFilter filter, DateTimeOffset now)
    {
        var pipeline = ReportPipeline.Problems(scope, db, companyClock, viewer, filter, metric.DateBasis, now);
        IQueryable<Problem> rows;
        try
        {
            if (part == ReportDrillPart.Denominator)
            {
                rows = metric.DenominatorRows(pipeline.Filtered, pipeline.Context) ?? throw Invalid(PartField);
                if (bucket is not null)
                {
                    throw Invalid(BucketField);
                }
            }
            else
            {
                rows = bucket is null ? metric.Rows(pipeline.Filtered, pipeline.Context) : metric.BucketRows(pipeline.Filtered, pipeline.Context, bucket);
            }
        }
        catch (NotSupportedException)
        {
            throw Invalid(BucketField);
        }

        return new DrilldownRows(DrilldownKind.Problems, null, null, null, null, rows.OrderBy(p => p.Number).ThenBy(p => p.Id)
            .Select(p => new DrilldownProblemRow(
                p.Number, p.Title, p.Status, p.Impact,
                db.Teams.Where(t => t.Id == p.OwnerTeamId).Select(t => t.Name).First(), p.CreatedAt, p.ResolvedAt,
                db.Services.Where(s => s.Id == p.ServiceId).Select(s => s.Name).FirstOrDefault())));
    }

    private static IQueryable<ChangeRequest> ChangeRows(ChangeReportMetric metric, IQueryable<ChangeRequest> filtered, ReportContext context, string? bucket, ReportDrillPart part)
    {
        if (part == ReportDrillPart.Denominator)
        {
            if (bucket is not null)
            {
                throw Invalid(BucketField);
            }

            return metric.DenominatorRows(filtered, context) ?? throw Invalid(PartField);
        }

        try
        {
            return bucket is null ? metric.Rows(filtered, context) : metric.BucketRows(filtered, context, bucket);
        }
        catch (NotSupportedException)
        {
            throw Invalid(BucketField);
        }
    }

    private static ValidationException Invalid(string field) =>
        new([new ValidationFailure(field, "Not available for this metric.") { ErrorCode = ErrorCodes.ReportsInvalidFilter }]);
}
