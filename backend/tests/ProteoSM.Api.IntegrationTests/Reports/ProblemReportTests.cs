using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application.Common;
using ProteoSM.Application.Reports;
using ProteoSM.Application.Reports.Metrics;
using ProteoSM.Domain.Sla;
using ProteoSM.Domain.Tickets;
using ProteoSM.Domain.Users;

namespace ProteoSM.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class ProblemReportTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ReportsPath = "/api/v1/reports/problems";
    private const string March = "from=2026-03-01&to=2026-03-31";
    private const string RecordsSheet = "Kayıtlar";
    private const double SecondsPerHour = 3600;

    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly ReportFilter MarchFilter = new() { From = new DateOnly(2026, 3, 1), To = new DateOnly(2026, 3, 31) };
    private static readonly string[] ExpectedHeaders = ["Numara", "Başlık", "Durum", "Etki", "Ekip", "Oluşturulma", "Çözülme", "Hizmet"];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ReportFigure Figure(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric);

    private static long CountOf(ReportFigure figure, string key) => Assert.Single(figure.Buckets!, b => b.Key == key).Count;

    /// <summary>
    /// Desk team: open (no incidents), investigating (1), known error (3), resolved after 4 h (7 incidents), resolved after 8 h,
    /// resolved after 30 h, cancelled. Field team: one known error.
    /// </summary>
    private async Task<ProblemReportFixture> SeededAsync()
    {
        var fx = new ProblemReportFixture(await ReportWorld.CreateAsync(factory));
        await fx.ProblemAsync(ProblemEnding.Open, T0, Level.Low);
        await fx.ProblemAsync(ProblemEnding.Investigating, T0, Level.Medium, linkedIncidents: 1);
        await fx.ProblemAsync(ProblemEnding.KnownError, T0, Level.High, linkedIncidents: 3);
        await fx.ProblemAsync(ProblemEnding.KnownError, T0, Level.High, teamId: fx.World.FieldTeam.Id);
        await fx.ProblemAsync(ProblemEnding.Resolved, T0, Level.High, TimeSpan.FromHours(4), linkedIncidents: 7);
        await fx.ProblemAsync(ProblemEnding.Resolved, T0.AddDays(1), Level.Medium, TimeSpan.FromHours(8));
        await fx.ProblemAsync(ProblemEnding.Resolved, T0.AddDays(-3), Level.High, TimeSpan.FromHours(30));
        await fx.ProblemAsync(ProblemEnding.Cancelled, T0, Level.Low);
        return fx;
    }

    [Fact]
    public async Task Known_errors_open_is_a_snapshot_that_ignores_the_period()
    {
        var fx = await SeededAsync();

        var march = await fx.RunAsync(fx.World.Admin.Id, MarchFilter);
        var elsewhere = await fx.RunAsync(fx.World.Admin.Id, new ReportFilter { From = new DateOnly(2025, 1, 1), To = new DateOnly(2025, 1, 31) });

        Assert.Equal(2, Figure(march, ProblemMetrics.KnownErrorsOpen).Value);
        Assert.Equal(2, Figure(elsewhere, ProblemMetrics.KnownErrorsOpen).Value);
    }

    [Fact]
    public async Task Resolved_counts_problems_resolved_in_the_period_and_cancelled_ones_do_not_count()
    {
        var fx = await SeededAsync();

        var march = await fx.RunAsync(fx.World.Admin.Id, MarchFilter);
        var early = await fx.RunAsync(fx.World.Admin.Id, MarchFilter with { To = new DateOnly(2026, 3, 8) });

        Assert.Equal(3, Figure(march, ProblemMetrics.Resolved).Value);
        Assert.Equal(1, Figure(early, ProblemMetrics.Resolved).Value); // only the 30 h one closed by the 8th
    }

    [Fact]
    public async Task Resolution_time_is_the_average_and_median_of_resolved_problems()
    {
        var fx = await SeededAsync();

        var time = Figure(await fx.RunAsync(fx.World.Admin.Id, MarchFilter), ProblemMetrics.ResolutionTime);
        var even = Figure(await fx.RunAsync(fx.World.Admin.Id, MarchFilter with { To = new DateOnly(2026, 3, 10) }), ProblemMetrics.ResolutionTime);

        Assert.Equal(14 * SecondsPerHour, time.Value); // (4 + 8 + 30) / 3
        Assert.Equal(3, time.Denominator);
        Assert.Null(time.Numerator);
        Assert.Equal((long)(8 * SecondsPerHour), CountOf(time, MetricKeys.Median));
        Assert.Equal((17 * SecondsPerHour, 2L), (even.Value, even.Denominator)); // the 4 h and the 30 h problems resolved by the 10th
        Assert.Equal((long)(17 * SecondsPerHour), CountOf(even, MetricKeys.Median));
    }

    [Fact]
    public async Task By_impact_counts_problems_created_in_the_period_with_every_level_present()
    {
        var fx = await SeededAsync();

        var result = await fx.RunAsync(fx.World.Admin.Id, MarchFilter);
        var empty = await fx.RunAsync(fx.World.Admin.Id, new ReportFilter { From = new DateOnly(2025, 1, 1), To = new DateOnly(2025, 1, 31) });

        var byImpact = Figure(result, ProblemMetrics.ByImpact);
        Assert.Equal(["low", "medium", "high"], byImpact.Buckets!.Select(b => b.Key));
        Assert.Equal([2L, 2L, 4L], byImpact.Buckets!.Select(b => b.Count));
        Assert.Equal(8, byImpact.Value);
        Assert.Equal([0L, 0L, 0L], Figure(empty, ProblemMetrics.ByImpact).Buckets!.Select(b => b.Count));
    }

    [Fact]
    public async Task Incidents_per_problem_buckets_linked_problems_and_leaves_out_problems_without_incidents()
    {
        var fx = await SeededAsync();

        var figure = Figure(await fx.RunAsync(fx.World.Admin.Id, MarchFilter), ProblemMetrics.IncidentsPerProblem);

        Assert.Equal(["1", "2_5", "6_plus"], figure.Buckets!.Select(b => b.Key));
        Assert.Equal([1L, 1L, 1L], figure.Buckets!.Select(b => b.Count)); // 1, 3 and 7 incidents; the problem with none is absent
        Assert.Equal(3, figure.Value);
    }

    [Fact]
    public async Task Staff_of_any_team_sees_all_problems_and_the_scope_label_is_all()
    {
        var fx = await SeededAsync();

        var desk = await fx.RunAsync(fx.World.Desk.Id, MarchFilter);
        var field = await fx.RunAsync(fx.World.Field.Id, MarchFilter);
        var admin = await fx.RunAsync(fx.World.Admin.Id, MarchFilter);

        foreach (var result in new[] { desk, field, admin })
        {
            Assert.Equal(GetReportHandler.AllScope, result.ScopeLabel);
            Assert.Equal(8, Figure(result, ProblemMetrics.ByImpact).Value);
            Assert.Equal(2, Figure(result, ProblemMetrics.KnownErrorsOpen).Value);
        }
    }

    [Fact]
    public async Task Team_filter_narrows_to_the_owner_team()
    {
        var fx = await SeededAsync();

        var field = await fx.RunAsync(fx.World.Desk.Id, MarchFilter with { TeamId = fx.World.FieldTeam.Id });

        Assert.Equal(1, Figure(field, ProblemMetrics.KnownErrorsOpen).Value);
        Assert.Equal(1, Figure(field, ProblemMetrics.ByImpact).Value);
        Assert.Equal(0, Figure(field, ProblemMetrics.Resolved).Value);
        Assert.Null(Figure(field, ProblemMetrics.ResolutionTime).Value);
        Assert.Equal(0, Figure(field, ProblemMetrics.IncidentsPerProblem).Value);
    }

    [Fact]
    public async Task Employee_is_forbidden_by_handler_and_endpoint()
    {
        var fx = await SeededAsync();
        await Assert.ThrowsAsync<ForbiddenException>(() => fx.RunAsync(fx.World.Employee.Id));

        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "visitor@example.com");
        using var response = await employee.GetAsync($"{ReportsPath}?{March}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Known_error_drilldown_lists_the_problems_and_the_export_equals_those_rows()
    {
        var fx = await SeededAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");
        var query = $"metric={ProblemMetrics.KnownErrorsOpen}&{March}";

        using var drill = await admin.GetAsync($"{ReportsPath}/drilldown?{query}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, drill.StatusCode);
        var body = await admin.ReadJsonAsync<JsonElement>(drill);
        Assert.Equal("problems", body.GetProperty("kind").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("tasks").ValueKind);
        var rows = body.GetProperty("problems").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["PRB-2026-000003", "PRB-2026-000004"], rows.Select(r => r.GetProperty("number").GetString()));
        Assert.All(rows, r => Assert.Equal("known_error", r.GetProperty("status").GetString()));
        Assert.Equal([fx.World.DeskTeam.Name, fx.World.FieldTeam.Name], rows.Select(r => r.GetProperty("ownerTeamName").GetString()));

        using var export = await admin.GetAsync($"{ReportsPath}/export?{query}");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var workbook = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync(Ct)));
        var sheet = workbook.Worksheet(RecordsSheet);
        Assert.Equal(ExpectedHeaders, sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        Assert.Equal(rows.Count + 1, sheet.LastRowUsed()!.RowNumber());
        Assert.Equal(rows.Select(r => r.GetProperty("number").GetString()), [sheet.Cell(2, 1).GetString(), sheet.Cell(3, 1).GetString()]);
        Assert.Equal(["Bilinen hata", "Bilinen hata"], [sheet.Cell(2, 3).GetString(), sheet.Cell(3, 3).GetString()]);
        Assert.Equal(["Yüksek", "Yüksek"], [sheet.Cell(2, 4).GetString(), sheet.Cell(3, 4).GetString()]);
    }

    [Fact]
    public async Task By_service_counts_problems_created_in_the_period_and_its_drilldown_and_export_carry_the_service()
    {
        var fx = new ProblemReportFixture(await ReportWorld.CreateAsync(factory));
        var sap = await fx.World.ServiceAsync("SAP");
        var vpn = await fx.World.ServiceAsync("VPN");
        await fx.ProblemAsync(ProblemEnding.Open, T0, serviceId: sap.Id);
        await fx.ProblemAsync(ProblemEnding.Investigating, T0, serviceId: sap.Id);
        await fx.ProblemAsync(ProblemEnding.Open, T0);
        await fx.ProblemAsync(ProblemEnding.Open, T0.AddDays(-30), serviceId: vpn.Id); // created before the period

        var result = await fx.RunAsync(fx.World.Admin.Id, MarchFilter);

        Assert.Equal(3, Figure(result, ProblemMetrics.ByService).Value);
        Assert.Equal([(sap.Id.ToString(), "SAP", 2L), (OperationsMetrics.None, null, 1L)],
            Figure(result, ProblemMetrics.ByService).Buckets!.Select(b => (b.Key, b.Label, b.Count)));

        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");
        var query = $"metric={ProblemMetrics.ByService}&bucket={sap.Id}&{March}";
        using var drill = await admin.GetAsync($"{ReportsPath}/drilldown?{query}&pageSize=100");
        var body = await admin.ReadJsonAsync<JsonElement>(drill);
        var rows = body.GetProperty("problems").GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("SAP", r.GetProperty("serviceName").GetString()));

        using var export = await admin.GetAsync($"{ReportsPath}/export?{query}");
        using var workbook = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync(Ct)));
        var sheet = workbook.Worksheet(RecordsSheet);
        Assert.Equal("Hizmet", sheet.Cell(1, ExpectedHeaders.Length).GetString());
        Assert.Equal("SAP", sheet.Cell(2, ExpectedHeaders.Length).GetString());

        using var none = await admin.GetAsync($"{ReportsPath}/drilldown?metric={ProblemMetrics.ByService}&bucket={OperationsMetrics.None}&{March}");
        var noneRow = Assert.Single((await admin.ReadJsonAsync<JsonElement>(none)).GetProperty("problems").GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, noneRow.GetProperty("serviceName").ValueKind);
    }

    [Fact]
    public async Task Incident_bucket_drilldown_matches_the_bucket_count()
    {
        await SeededAsync();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");

        foreach (var (bucket, number) in new[] { ("1", "PRB-2026-000002"), ("2_5", "PRB-2026-000003"), ("6_plus", "PRB-2026-000005") })
        {
            using var response = await admin.GetAsync($"{ReportsPath}/drilldown?metric={ProblemMetrics.IncidentsPerProblem}&bucket={bucket}&{March}");
            var body = await admin.ReadJsonAsync<JsonElement>(response);
            var items = body.GetProperty("problems").GetProperty("items").EnumerateArray().ToList();
            Assert.Equal(1, body.GetProperty("problems").GetProperty("totalCount").GetInt64());
            Assert.Equal(number, items[0].GetProperty("number").GetString());
        }
    }

    [Fact]
    public async Task Known_error_sla_compliance_counts_met_over_stopped_excluding_cancelled()
    {
        var fx = new ProblemReportFixture(await ReportWorld.CreateAsync(factory));
        var stoppedInMarch = T0.AddDays(2);
        var met = await fx.ProblemAsync(ProblemEnding.KnownError, T0);
        var breached = await fx.ProblemAsync(ProblemEnding.KnownError, T0);
        var cancelled = await fx.ProblemAsync(ProblemEnding.Cancelled, T0);
        var running = await fx.ProblemAsync(ProblemEnding.Open, T0);
        var stoppedEarlier = await fx.ProblemAsync(ProblemEnding.KnownError, T0.AddDays(-60));
        await fx.ProblemAsync(ProblemEnding.KnownError, T0); // no timer: a problem from before the feature
        await fx.TimerAsync(met, SlaResult.Met, stoppedInMarch);
        await fx.TimerAsync(breached, SlaResult.Breached, stoppedInMarch);
        await fx.TimerAsync(cancelled, SlaResult.Cancelled, stoppedInMarch);
        await fx.TimerAsync(running, null, stoppedInMarch);
        await fx.TimerAsync(stoppedEarlier, SlaResult.Met, T0.AddDays(-50));
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "boss@example.com");

        var figure = Figure(await fx.RunAsync(fx.World.Admin.Id, MarchFilter), ProblemMetrics.KnownErrorSlaCompliance);

        Assert.Equal((1L, 2L, 0.5), (figure.Numerator, figure.Denominator, figure.Value));
        var query = $"metric={ProblemMetrics.KnownErrorSlaCompliance}&{March}&pageSize=100";
        Assert.Equal([met.Number], await DrilledNumbersAsync(admin, query));
        Assert.Equal([met.Number, breached.Number], await DrilledNumbersAsync(admin, $"{query}&part=denominator"));

        using var export = await admin.GetAsync($"{ReportsPath}/export?{query}");
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        using var workbook = new XLWorkbook(new MemoryStream(await export.Content.ReadAsByteArrayAsync(Ct)));
        var sheet = workbook.Worksheet(RecordsSheet);
        Assert.Equal(ExpectedHeaders, sheet.Row(1).CellsUsed().Select(c => c.GetString()));
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber()); // header plus the one met problem
        Assert.Equal(met.Number, sheet.Cell(2, 1).GetString());
    }

    private async Task<List<string?>> DrilledNumbersAsync(ApiClient admin, string query)
    {
        using var drill = await admin.GetAsync($"{ReportsPath}/drilldown?{query}");
        Assert.Equal(HttpStatusCode.OK, drill.StatusCode);
        var body = await admin.ReadJsonAsync<JsonElement>(drill);
        return body.GetProperty("problems").GetProperty("items").EnumerateArray().Select(r => r.GetProperty("number").GetString()).ToList();
    }
}
