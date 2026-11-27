using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Api.IntegrationTests.Problems;
using ProteoSM.Api.IntegrationTests.Tickets;
using ProteoSM.Application.Notifications;
using ProteoSM.Application.Sla;
using ProteoSM.Domain.Notifications;
using ProteoSM.Domain.Problems;

namespace ProteoSM.Api.IntegrationTests.Sla;

/// <summary>
/// <see cref="MonitorSlaHandler"/> over problem timers (spec "Monitor"). A problem is created through the API on the
/// next free Monday 10:00 Istanbul; the fake clock is then moved straight to a timer moment, because moving it by days
/// through the API would expire the login.
/// </summary>
public sealed class ProblemSlaMonitorTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const int Batch = 50;
    private const int ParallelProblems = 10;
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PastMoment = TimeSpan.FromMinutes(1);
    private static readonly DateTimeOffset FirstStart = new(2026, 10, 5, 7, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan OneWeek = TimeSpan.FromDays(7);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        var start = FirstStart;
        while (start <= factory.Clock.GetUtcNow())
        {
            start += OneWeek;
        }

        factory.Clock.SetUtcNow(start);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task MonitorAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<MonitorSlaHandler>().HandleAsync(Batch, Ct);
    }

    private async Task DispatchAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(Batch, Lease, Ct);
    }

    private async Task<List<OutboxEvent>> OutboxAsync(string eventType)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.OutboxEvents.AsNoTracking().Where(e => e.EventType == eventType).ToListAsync(Ct);
    }

    private async Task<ProblemSlaTimer> TimerAsync(JsonElement problem)
    {
        await using var db = factory.Database.CreateDbContext();
        var id = problem.GetProperty("id").GetGuid();
        return await db.ProblemSlaTimers.AsNoTracking().SingleAsync(t => t.ProblemId == id, Ct);
    }

    private static Guid IdOf(JsonElement problem) => problem.GetProperty("id").GetGuid();

    [Fact]
    public async Task Due_timer_is_breached_once_and_one_event_is_written()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var timer = await TimerAsync(problem);
        factory.Clock.SetUtcNow(timer.DueAt + PastMoment);

        await MonitorAsync();
        await MonitorAsync();

        var breach = Assert.Single(await OutboxAsync(NotificationEventTypes.ProblemSlaBreached));
        Assert.Equal(OutboxSubjectTypes.Problem, breach.SubjectType);
        Assert.Equal(IdOf(problem), breach.SubjectId);
        Assert.Null(breach.TicketId);
        using var payload = JsonDocument.Parse(breach.Payload);
        Assert.Equal(timer.DueAt, payload.RootElement.GetProperty(OutboxEvent.SlaDueAtKey).GetDateTimeOffset());
        // A breach on a timer that never warned warns first, as the ticket monitor does.
        Assert.Single(await OutboxAsync(NotificationEventTypes.ProblemSlaWarning));
        var after = await TimerAsync(problem);
        Assert.NotNull(after.WarnedAt);
        Assert.NotNull(after.BreachedAt);
    }

    [Fact]
    public async Task Two_monitor_runs_at_once_write_one_breach()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problems = new List<JsonElement>();
        for (var i = 0; i < ParallelProblems; i++)
        {
            problems.Add(await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, title: $"Problem {i}"));
        }

        factory.Clock.SetUtcNow((await TimerAsync(problems[0])).DueAt + PastMoment);

        await Task.WhenAll(MonitorAsync(), MonitorAsync());

        Assert.Equal(ParallelProblems, (await OutboxAsync(NotificationEventTypes.ProblemSlaBreached)).Count);
        Assert.Equal(ParallelProblems, (await OutboxAsync(NotificationEventTypes.ProblemSlaWarning)).Count);
        Assert.Equal(ParallelProblems, (await OutboxAsync(NotificationEventTypes.ProblemSlaBreached)).Select(e => e.SubjectId).Distinct().Count());
    }

    [Fact]
    public async Task Stopped_timer_is_ignored()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var timer = await TimerAsync(problem);
        await using (var db = factory.Database.CreateDbContext())
        {
            var tracked = await db.ProblemSlaTimers.SingleAsync(t => t.ProblemId == IdOf(problem), Ct);
            tracked.Cancel(factory.Clock.GetUtcNow());
            await db.SaveChangesAsync(Ct);
        }

        factory.Clock.SetUtcNow(timer.DueAt + PastMoment);
        await MonitorAsync();

        Assert.Empty(await OutboxAsync(NotificationEventTypes.ProblemSlaWarning));
        Assert.Empty(await OutboxAsync(NotificationEventTypes.ProblemSlaBreached));
        Assert.Null((await TimerAsync(problem)).BreachedAt);
    }

    [Fact]
    public async Task Monitor_mark_between_load_and_user_stop_does_not_fail_the_stop()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var timer = await TimerAsync(problem);

        // The user's action loads the timer first; the monitor then marks the warning; the stop saves last.
        await using var userScope = factory.Database.CreateDbContext();
        var tracked = await userScope.ProblemSlaTimers.SingleAsync(t => t.ProblemId == IdOf(problem), Ct);
        factory.Clock.SetUtcNow(timer.WarnAt + PastMoment);
        await MonitorAsync();
        Assert.NotNull((await TimerAsync(problem)).WarnedAt);

        tracked.Stop(factory.Clock.GetUtcNow());
        await userScope.SaveChangesAsync(Ct);

        Assert.NotNull((await TimerAsync(problem)).StoppedAt);
    }

    [Fact]
    public async Task Warn_then_breach_write_two_events()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var timer = await TimerAsync(problem);

        factory.Clock.SetUtcNow(timer.WarnAt + PastMoment);
        await MonitorAsync();
        Assert.Single(await OutboxAsync(NotificationEventTypes.ProblemSlaWarning));
        Assert.Empty(await OutboxAsync(NotificationEventTypes.ProblemSlaBreached));

        factory.Clock.SetUtcNow(timer.DueAt + PastMoment);
        await MonitorAsync();

        Assert.Single(await OutboxAsync(NotificationEventTypes.ProblemSlaWarning));
        Assert.Single(await OutboxAsync(NotificationEventTypes.ProblemSlaBreached));
    }

    [Fact]
    public async Task Breached_timer_mails_the_owner_with_the_problem_number()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, ownerId: world.DeskUser.Id);
        factory.Clock.SetUtcNow((await TimerAsync(problem)).DueAt + PastMoment);

        await MonitorAsync();
        await DispatchAsync();

        await using var db = factory.Database.CreateDbContext();
        var deliveries = await db.EmailDeliveries.AsNoTracking()
            .Where(d => d.Subject.Contains(ProblemWorld.NumberOf(problem))).ToListAsync(Ct);
        var breached = Assert.Single(deliveries, d => d.Subject.Contains("aşıldı", StringComparison.Ordinal));
        Assert.Equal(world.DeskUser.Id, breached.RecipientUserId);
        Assert.Contains(ProblemWorld.NumberOf(problem), breached.Body!, StringComparison.Ordinal);
        Assert.Contains($"/problems/{ProblemWorld.NumberOf(problem)}", breached.Body!, StringComparison.Ordinal);
        Assert.DoesNotContain("{{", breached.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Breached_timer_without_owner_mails_active_team_members()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        factory.Clock.SetUtcNow((await TimerAsync(problem)).DueAt + PastMoment);

        await MonitorAsync();
        await DispatchAsync();

        await using var db = factory.Database.CreateDbContext();
        var memberIds = await db.TeamMemberships.Where(m => m.TeamId == world.ServiceDesk.Id).Select(m => m.UserId).ToListAsync(Ct);
        var recipients = await db.EmailDeliveries.AsNoTracking().Select(d => d.RecipientUserId).Distinct().ToListAsync(Ct);
        Assert.NotEmpty(recipients);
        Assert.All(recipients, id => Assert.Contains(id, memberIds));
    }
}
