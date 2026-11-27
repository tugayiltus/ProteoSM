using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Api.IntegrationTests.Tickets;
using Proteone.Domain.Problems;
using Proteone.Domain.Sla;
using Proteone.Domain.Tickets;

namespace Proteone.Api.IntegrationTests.Problems;

/// <summary>
/// Known-error timer of a problem from creation to stop (spec "Timer"). Each test starts on the next free Monday
/// 10:00 Istanbul, as in <c>SlaMonitorTests</c>, because a fake clock only moves forward.
/// </summary>
public sealed class ProblemSlaLifecycleTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const string HighImpact = "high";
    private const string LowImpact = "low";
    private const int WarningPercent = 80;
    private const int OneHourSeconds = 3600;
    private const int FirstWorkingHours = 1;
    private static readonly DateTimeOffset FirstStart = new(2026, 10, 5, 7, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan OneWeek = TimeSpan.FromDays(7);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();

        // Reset keeps the seeded targets, so every test starts from the defaults.
        await using (var db = factory.Database.CreateDbContext())
        {
            foreach (var target in await db.ProblemSlaTargets.ToListAsync(Ct))
            {
                var (_, minutes, percent) = ProblemSlaTarget.Defaults.Single(d => d.Impact == target.Impact);
                target.Change(minutes, percent);
            }

            await db.SaveChangesAsync(Ct);
        }

        var start = FirstStart;
        while (start <= factory.Clock.GetUtcNow())
        {
            start += OneWeek;
        }

        factory.Clock.SetUtcNow(start);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ProblemSlaTimer?> TimerAsync(JsonElement problem)
    {
        await using var db = factory.Database.CreateDbContext();
        var id = problem.GetProperty("id").GetGuid();
        return await db.ProblemSlaTimers.AsNoTracking().SingleOrDefaultAsync(t => t.ProblemId == id, Ct);
    }

    private static async Task<JsonElement> PostAsync(ApiClient client, JsonElement problem, string action, object? extra = null)
    {
        var body = extra is null
            ? (object)new { version = ProblemWorld.VersionOf(problem) }
            : new { version = ProblemWorld.VersionOf(problem), resolution = "Çözüldü", reason = "Gereksiz" };
        using var response = await client.PostAsync(ProblemWorld.PathOf(problem, action), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static async Task<JsonElement> ToKnownErrorAsync(ApiClient client, JsonElement problem)
    {
        var investigating = await PostAsync(client, problem, "start");
        using var saved = await client.PutAsync(
            ProblemWorld.PathOf(problem), ProblemWorld.EditBody(investigating, rootCause: "Sürücü hatası", workaround: "Eski sürücüye dön"));
        var edited = await client.ReadJsonAsync<JsonElement>(saved);
        return await PostAsync(client, edited, "known-error");
    }

    [Fact]
    public async Task Create_starts_timer_with_impact_target()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, impact: HighImpact);

        var timer = await TimerAsync(problem);
        Assert.NotNull(timer);
        var (_, minutes, percent) = ProblemSlaTarget.Defaults.Single(d => d.Impact == Level.High);
        Assert.Equal((minutes, percent), (timer.TargetMinutes, timer.WarningPercent));
        Assert.Equal(factory.Clock.GetUtcNow(), timer.StartedAt);
        Assert.True(timer.DueAt > timer.WarnAt);
        Assert.False(timer.IsStopped);
    }

    [Fact]
    public async Task Known_error_stops_timer_met()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        await ToKnownErrorAsync(world.Desk, problem);

        var timer = await TimerAsync(problem);
        Assert.Equal(SlaResult.Met, timer!.Result);
        Assert.Equal(factory.Clock.GetUtcNow(), timer.StoppedAt);
    }

    [Fact]
    public async Task Known_error_after_due_stops_timer_breached()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, impact: HighImpact);
        await using (var db = factory.Database.CreateDbContext())
        {
            // The monitor records the breach once the due time passes; a fake clock cannot move that far without expiring the login.
            var timer = await db.ProblemSlaTimers.SingleAsync(t => t.ProblemId == problem.GetProperty("id").GetGuid(), Ct);
            timer.RecordBreach(factory.Clock.GetUtcNow());
            await db.SaveChangesAsync(Ct);
        }

        await ToKnownErrorAsync(world.Desk, problem);

        Assert.Equal(SlaResult.Breached, (await TimerAsync(problem))!.Result);
    }

    [Fact]
    public async Task Resolve_after_known_error_keeps_result()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, impact: HighImpact);
        var known = await ToKnownErrorAsync(world.Desk, problem);
        var stoppedAt = (await TimerAsync(problem))!.StoppedAt;
        factory.Clock.Advance(TimeSpan.FromHours(FirstWorkingHours));

        await PostAsync(world.Desk, known, "resolve", extra: true);

        var timer = await TimerAsync(problem);
        Assert.Equal(SlaResult.Met, timer!.Result);
        Assert.Equal(stoppedAt, timer.StoppedAt);
    }

    [Fact]
    public async Task Resolve_from_open_stops_timer()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        await PostAsync(world.Desk, problem, "resolve", extra: true);

        var timer = await TimerAsync(problem);
        Assert.True(timer!.IsStopped);
        Assert.Equal(SlaResult.Met, timer.Result);
    }

    [Fact]
    public async Task Cancel_sets_cancelled()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        await PostAsync(world.Desk, problem, "cancel", extra: true);

        Assert.Equal(SlaResult.Cancelled, (await TimerAsync(problem))!.Result);
    }

    [Fact]
    public async Task Impact_change_retargets_running_timer()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, impact: HighImpact);
        var before = (await TimerAsync(problem))!;
        factory.Clock.Advance(TimeSpan.FromHours(FirstWorkingHours));

        using var edit = await world.Desk.PutAsync(ProblemWorld.PathOf(problem), new
        {
            version = ProblemWorld.VersionOf(problem),
            title = problem.GetProperty("title").GetString(),
            description = problem.GetProperty("description").GetString(),
            impact = LowImpact,
        });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var after = (await TimerAsync(problem))!;
        var (_, minutes, _) = ProblemSlaTarget.Defaults.Single(d => d.Impact == Level.Low);
        Assert.Equal(minutes, after.TargetMinutes);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.Equal(OneHourSeconds, after.ElapsedSeconds);
        Assert.True(after.DueAt > before.DueAt);
    }

    [Fact]
    public async Task Impact_change_after_warning_resets_warning_mark()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, impact: HighImpact);
        await using (var db = factory.Database.CreateDbContext())
        {
            var timer = await db.ProblemSlaTimers.SingleAsync(t => t.ProblemId == problem.GetProperty("id").GetGuid(), Ct);
            timer.RecordWarning(factory.Clock.GetUtcNow());
            await db.SaveChangesAsync(Ct);
        }

        using var edit = await world.Desk.PutAsync(ProblemWorld.PathOf(problem), new
        {
            version = ProblemWorld.VersionOf(problem),
            title = problem.GetProperty("title").GetString(),
            description = problem.GetProperty("description").GetString(),
            impact = LowImpact,
        });

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Null((await TimerAsync(problem))!.WarnedAt);
    }

    [Fact]
    public async Task Edit_without_impact_change_leaves_timer_alone()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, impact: HighImpact);
        var before = (await TimerAsync(problem))!;
        factory.Clock.Advance(TimeSpan.FromHours(FirstWorkingHours));

        using var edit = await world.Desk.PutAsync(ProblemWorld.PathOf(problem), ProblemWorld.EditBody(problem, title: "Yeni başlık"));

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var after = (await TimerAsync(problem))!;
        Assert.Equal(before.ElapsedSeconds, after.ElapsedSeconds);
        Assert.Equal(before.DueAt, after.DueAt);
    }

    [Fact]
    public async Task Calendar_change_replans_running_problem_timer()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, impact: HighImpact);
        var oldDueAt = (await TimerAsync(problem))!.DueAt;
        factory.Clock.Advance(TimeSpan.FromHours(FirstWorkingHours));

        // Monday 11:00 Istanbul: today becomes a holiday. 1 h already ran; the rest moves to the next working day.
        var istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(factory.Clock.GetUtcNow(), istanbul).DateTime);
        using var add = await world.Admin.PostAsync("/api/v1/admin/sla/holidays",
            new { date = today.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture), name = "Olağanüstü tatil" });
        Assert.Equal(HttpStatusCode.Created, add.StatusCode);

        var timer = (await TimerAsync(problem))!;
        Assert.Equal(OneHourSeconds, timer.ElapsedSeconds);
        Assert.Equal(factory.Clock.GetUtcNow(), timer.RunningSince);
        Assert.True(timer.DueAt > oldDueAt);
    }

    [Fact]
    public async Task Old_problem_without_timer_has_null_sla()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        await using (var db = factory.Database.CreateDbContext())
        {
            await db.ProblemSlaTimers.ExecuteDeleteAsync(Ct);
        }

        using var response = await world.Desk.GetAsync(ProblemWorld.PathOf(problem));
        var detail = await world.Desk.ReadJsonAsync<JsonElement>(response);

        Assert.Equal(JsonValueKind.Null, detail.GetProperty("sla").ValueKind);
    }

    [Fact]
    public async Task Detail_and_list_carry_sla()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, impact: HighImpact);
        factory.Clock.Advance(TimeSpan.FromHours(FirstWorkingHours));
        var timer = (await TimerAsync(problem))!;

        using var detailResponse = await world.Desk.GetAsync(ProblemWorld.PathOf(problem));
        var detail = (await world.Desk.ReadJsonAsync<JsonElement>(detailResponse)).GetProperty("sla");
        using var listResponse = await world.Desk.GetAsync(ProblemWorld.ProblemsPath);
        var item = (await world.Desk.ReadJsonAsync<JsonElement>(listResponse)).GetProperty("items").EnumerateArray().Single().GetProperty("sla");

        foreach (var sla in new[] { detail, item })
        {
            Assert.Equal("known_error", sla.GetProperty("metric").GetString());
            Assert.Equal(timer.DueAt, sla.GetProperty("dueAt").GetDateTimeOffset());
            Assert.Equal(timer.TargetMinutes * 60L - OneHourSeconds, sla.GetProperty("remainingSeconds").GetInt64());
            Assert.False(sla.GetProperty("isPaused").GetBoolean());
            Assert.False(sla.GetProperty("isWarned").GetBoolean());
            Assert.False(sla.GetProperty("isBreached").GetBoolean());
            Assert.Equal(JsonValueKind.Null, sla.GetProperty("result").ValueKind);
        }
    }

    [Fact]
    public async Task Stopped_timer_shows_result_and_no_remaining_time()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        var known = await ToKnownErrorAsync(world.Desk, problem);

        var sla = known.GetProperty("sla");
        Assert.Equal("met", sla.GetProperty("result").GetString());
        Assert.Equal(JsonValueKind.Null, sla.GetProperty("remainingSeconds").ValueKind);
    }
}
