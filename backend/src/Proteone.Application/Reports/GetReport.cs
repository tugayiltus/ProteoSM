using Microsoft.EntityFrameworkCore;
using Proteone.Application.Common;
using Proteone.Application.Reports.Metrics;
using Proteone.Domain.Reports;
using Proteone.Domain.Users;

namespace Proteone.Application.Reports;

public sealed record GetReportQuery(ReportName Report, ReportFilter Filter);

/// <summary>The figures of one report. <c>ScopeLabel</c> is <c>all</c> or <c>teams</c> (the viewer's teams only).</summary>
public sealed record ReportResult(ReportName Report, ReportFilter Filter, string ScopeLabel, IReadOnlyList<ReportFigure> Figures);

public sealed class GetReportHandler(ReportScope scope, TicketReportSource source, Abstractions.IAppDbContext db, TimeProvider clock, ICompanyClock companyClock)
{
    public const string AllScope = "all";
    public const string TeamsScope = "teams";

    public async Task<ReportResult> HandleAsync(GetReportQuery query, CancellationToken cancellationToken)
    {
        query.Filter.EnsureValid();
        var viewer = await scope.ViewerAsync(cancellationToken);
        if (query.Report is ReportName.Assets or ReportName.Warranty)
        {
            return await AssetReport(query, viewer, cancellationToken);
        }

        if (query.Report == ReportName.Changes)
        {
            return await ChangeReport(query, viewer, cancellationToken);
        }

        if (query.Report == ReportName.Tasks)
        {
            return await TaskReport(query, viewer, cancellationToken);
        }

        if (query.Report == ReportName.Licences)
        {
            return await LicenceReport(query, viewer, cancellationToken);
        }

        if (query.Report == ReportName.Problems)
        {
            return await ProblemReport(query, viewer, cancellationToken);
        }

        var now = clock.GetUtcNow();
        var effective = ReportCatalog.EffectiveFilter(query.Report, query.Filter);
        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<ReportMetric>())
        {
            var pipeline = ReportPipeline.Tickets(scope, source, db, viewer, query.Report, query.Filter, metric, now);
            figures.Add(await metric.ComputeAsync(pipeline.Filtered, pipeline.Context, cancellationToken));
        }

        return new ReportResult(query.Report, effective, ScopeLabelOf(viewer, query.Report), figures);
    }

    /// <summary><see cref="AllScope"/> for admins, <see cref="TeamsScope"/> for IT staff; asset, problem and licence reports always see everything.</summary>
    public static string ScopeLabelOf(ReportViewer viewer, ReportName report) =>
        viewer.Role == UserRole.Admin || report is ReportName.Assets or ReportName.Warranty or ReportName.Problems or ReportName.Licences ? AllScope : TeamsScope;

    /// <summary>The change report: each metric dates the changes on its own basis, inside the viewer's team scope.</summary>
    private async Task<ReportResult> ChangeReport(GetReportQuery query, ReportViewer viewer, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<ChangeReportMetric>())
        {
            var pipeline = ReportPipeline.Changes(scope, db, companyClock, viewer, query.Filter, metric.DateBasis, now);
            figures.Add(await metric.ComputeAsync(pipeline.Filtered, pipeline.Context, cancellationToken));
        }

        return new ReportResult(query.Report, query.Filter, ScopeLabelOf(viewer, query.Report), figures);
    }

    /// <summary>The problem report: every problem is visible to IT staff and admins, each metric dates them on its own basis.</summary>
    private async Task<ReportResult> ProblemReport(GetReportQuery query, ReportViewer viewer, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<ProblemReportMetric>())
        {
            var pipeline = ReportPipeline.Problems(scope, db, companyClock, viewer, query.Filter, metric.DateBasis, now);
            figures.Add(await metric.ComputeAsync(pipeline.Filtered, pipeline.Context, cancellationToken));
        }

        return new ReportResult(query.Report, query.Filter, ScopeLabelOf(viewer, query.Report), figures);
    }

    /// <summary>The licence report is a snapshot of all licences, for IT staff and admins alike.</summary>
    private async Task<ReportResult> LicenceReport(GetReportQuery query, ReportViewer viewer, CancellationToken cancellationToken)
    {
        var pipeline = ReportPipeline.Licences(scope, db, companyClock, viewer, query.Filter, clock.GetUtcNow());
        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<LicenceReportMetric>())
        {
            figures.Add(await metric.ComputeAsync(pipeline.Filtered, pipeline.Context, cancellationToken));
        }

        return new ReportResult(query.Report, query.Filter, AllScope, figures);
    }

    /// <summary>The task report: each metric dates the tasks on its own basis, inside the viewer's team scope.</summary>
    private async Task<ReportResult> TaskReport(GetReportQuery query, ReportViewer viewer, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<TaskReportMetric>())
        {
            var pipeline = ReportPipeline.Tasks(scope, db, companyClock, viewer, query.Filter, metric.DateBasis, now);
            figures.Add(await metric.ComputeAsync(pipeline.Filtered, pipeline.Context, cancellationToken));
        }

        return new ReportResult(query.Report, query.Filter, ScopeLabelOf(viewer, query.Report), figures);
    }

    /// <summary>Asset and warranty reports are snapshots of all assets, for IT staff and admins alike.</summary>
    private async Task<ReportResult> AssetReport(GetReportQuery query, ReportViewer viewer, CancellationToken cancellationToken)
    {
        var warrantyWindowDays = await db.SystemSettings.Select(s => s.WarrantyWindowDays).SingleAsync(cancellationToken);
        var pipeline = ReportPipeline.Assets(scope, db, companyClock, viewer, query.Filter, clock.GetUtcNow(), warrantyWindowDays);

        var figures = new List<ReportFigure>();
        foreach (var metric in ReportCatalog.Get(query.Report).OfType<AssetReportMetric>())
        {
            figures.Add(await metric.ComputeAsync(pipeline.Filtered, pipeline.Context, cancellationToken));
        }

        return new ReportResult(query.Report, query.Filter, AllScope, figures);
    }
}
