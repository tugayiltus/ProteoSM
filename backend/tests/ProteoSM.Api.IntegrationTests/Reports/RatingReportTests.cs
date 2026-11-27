using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application.Common.Paging;
using ProteoSM.Application.Reports;
using ProteoSM.Application.Reports.Metrics;
using ProteoSM.Domain.Reports;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Api.IntegrationTests.Reports;

[Collection(ApiCollection.Name)]
public sealed class RatingReportTests(ApiFactory factory) : IAsyncLifetime
{
    private const double Tolerance = 1e-9;
    private const int PageSize = 50;

    private static readonly DateTimeOffset T0 = ReportFixture.T0;
    private static readonly DateTimeOffset Now = new(2026, 3, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Done = T0.AddHours(2);
    private static readonly ReportFilter March10 = new() { From = new DateOnly(2026, 3, 10), To = new DateOnly(2026, 3, 10) };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static ReportFigure Figure(ReportResult result, string metric) => Assert.Single(result.Figures, f => f.Metric == metric);

    private static long Bucket(ReportFigure figure, string key) => Assert.Single(figure.Buckets!, b => b.Key == key).Count;

    private static Action<Ticket> Resolved(DateTimeOffset at) => t => t.Complete("cozuldu", TicketOutcome.Resolved, false, false, at);

    private static async Task RateAsync(ReportFixture fx, Ticket ticket, int score)
    {
        await using var db = fx.World.Db();
        db.TicketRatings.Add(TicketRating.Create(ticket, fx.World.Employee.Id, score, null, ticket.CompletedAt!.Value.AddHours(1)));
        await db.SaveChangesAsync(Ct);
    }

    private async Task<(ReportFixture Fx, Ticket Five)> SeedAsync()
    {
        var fx = new ReportFixture(await ReportWorld.CreateAsync(factory));
        var five = await fx.IncidentAsync(T0, Resolved(Done));
        await RateAsync(fx, five, 5);
        await RateAsync(fx, await fx.IncidentAsync(T0, Resolved(Done)), 4);
        await RateAsync(fx, await fx.IncidentAsync(T0, Resolved(Done)), 2);
        await fx.IncidentAsync(T0, Resolved(Done)); // rateable, not rated
        await fx.IncidentAsync(T0, t => t.Cancel("vazgecildi", Done)); // not rateable
        await RateAsync(fx, await fx.IncidentAsync(T0, Resolved(T0.AddDays(2))), 1); // completed outside the window
        await RateAsync(fx, await fx.RequestAsync(T0, change: t => t.Complete("verildi", null, false, false, Done)), 3); // a request, not an incident
        return (fx, five);
    }

    [Fact]
    public async Task Average_rate_and_distribution_for_the_period()
    {
        var (fx, _) = await SeedAsync();

        var result = await fx.RunAsync(ReportName.Incidents, fx.World.Admin.Id, March10, Now);

        var average = Figure(result, RatingMetrics.Average);
        Assert.Equal(MetricShape.Ratio, average.Shape);
        Assert.Equal(11, average.Numerator);
        Assert.Equal(3, average.Denominator);
        Assert.Equal(11d / 3, average.Value!.Value, Tolerance);
        var rate = Figure(result, RatingMetrics.ResponseRate);
        Assert.Equal(MetricShape.Ratio, rate.Shape);
        Assert.Equal(3, rate.Numerator);
        Assert.Equal(4, rate.Denominator);
        Assert.Equal(0.75, rate.Value!.Value, Tolerance);
        var distribution = Figure(result, RatingMetrics.Distribution);
        Assert.Equal(MetricShape.Distribution, distribution.Shape);
        Assert.Equal(["1", "2", "3", "4", "5"], distribution.Buckets!.Select(b => b.Key));
        Assert.Equal([0L, 1L, 0L, 1L, 1L], distribution.Buckets!.Select(b => b.Count));
        Assert.Equal(3, distribution.Value);

        var requests = await fx.RunAsync(ReportName.Requests, fx.World.Admin.Id, March10, Now);
        Assert.Equal(3, Figure(requests, RatingMetrics.Average).Value);
        Assert.Equal(1, Figure(requests, RatingMetrics.ResponseRate).Value);
        Assert.Equal(1, Bucket(Figure(requests, RatingMetrics.Distribution), "3"));
        await fx.AssertFiguresMatchRowsAsync(ReportName.Incidents, March10, Now);
    }

    [Fact]
    public async Task Drilldown_of_bucket_five_lists_that_ticket()
    {
        var (fx, five) = await SeedAsync();
        await using var db = fx.World.Db();
        var scope = new ReportScope(db, new FixedActor(fx.World.Admin.Id));
        var rows = new ReportRowSource(scope, new TicketReportSource(db, new TestClock()), db, new FakeTimeProvider(Now), new TestClock());
        var handler = new GetDrilldownHandler(scope, rows);

        var bucket = await handler.HandleAsync(
            new GetDrilldownQuery(ReportName.Incidents, RatingMetrics.Distribution, "5", ReportDrillPart.Numerator, March10, new PageRequest(1, PageSize)), Ct);
        var rated = await handler.HandleAsync(
            new GetDrilldownQuery(ReportName.Incidents, RatingMetrics.ResponseRate, null, ReportDrillPart.Numerator, March10, new PageRequest(1, PageSize)), Ct);
        var rateable = await handler.HandleAsync(
            new GetDrilldownQuery(ReportName.Incidents, RatingMetrics.ResponseRate, null, ReportDrillPart.Denominator, March10, new PageRequest(1, PageSize)), Ct);

        Assert.Equal(five.Number, Assert.Single(bucket.Tickets!.Items).Number);
        Assert.Equal(3, rated.Tickets!.TotalCount);
        Assert.Equal(4, rateable.Tickets!.TotalCount);
    }

    [Fact]
    public async Task Team_filter_uses_team_at_completion()
    {
        var fx = new ReportFixture(await ReportWorld.CreateAsync(factory));
        var desk = fx.World.DeskTeam.Id;
        var field = fx.World.FieldTeam.Id;
        var moved = await fx.IncidentAsync(T0, Resolved(Done), desk);
        await RateAsync(fx, moved, 5);
        await fx.IncidentAsync(T0, Resolved(Done), desk); // rateable, not rated: counts under its own team
        await using (var db = fx.World.Db())
        {
            await db.Tickets.Where(t => t.Id == moved.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.TeamId, field), Ct);
        }

        var deskResult = await fx.RunAsync(ReportName.Incidents, fx.World.Admin.Id, March10 with { TeamId = desk }, Now);
        var fieldResult = await fx.RunAsync(ReportName.Incidents, fx.World.Admin.Id, March10 with { TeamId = field }, Now);

        Assert.Equal(5, Figure(deskResult, RatingMetrics.Average).Value);
        Assert.Equal(1, Figure(deskResult, RatingMetrics.ResponseRate).Numerator);
        Assert.Equal(2, Figure(deskResult, RatingMetrics.ResponseRate).Denominator);
        Assert.Equal(1, Bucket(Figure(deskResult, RatingMetrics.Distribution), "5"));
        Assert.Null(Figure(fieldResult, RatingMetrics.Average).Value);
        Assert.Equal(0, Figure(fieldResult, RatingMetrics.ResponseRate).Denominator);
        Assert.Equal(0, Figure(fieldResult, RatingMetrics.Distribution).Value);
        Assert.Equal(1, Figure(fieldResult, MetricKeys.Volume).Value); // the other figures still follow the current team
        await fx.AssertFiguresMatchRowsAsync(ReportName.Incidents, March10 with { TeamId = desk }, Now);
    }
}
