using Microsoft.EntityFrameworkCore;
using Npgsql;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Domain.Common;
using Proteone.Domain.Organization;
using Proteone.Domain.Problems;
using Proteone.Domain.Sla;
using Proteone.Domain.Tickets;
using Proteone.Domain.Users;

namespace Proteone.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class ProblemSlaPersistenceTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string UniqueViolation = "23505";
    private const int WorkingStartHour = 9;
    private const int WorkingEndHour = 17;
    private const int TargetMinutes = 960;
    private const int WarningPercent = 80;
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 9, 0, 0, TimeSpan.Zero);
    private User _creator = null!;
    private Team _team = null!;
    private Problem _problem = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetAsync();
        _creator = User.Create("Ayşe", "Kaya", "ayse@example.com", UserRole.Employee, null, null, "hash", Now);
        _team = Team.Create("Servis Masası");
        _problem = Problem.Create("PRB-2026-000001", "Wi-Fi", "Kopuyor", Level.High, _team.Id, null, _creator.Id, Now);
        await using var db = database.CreateDbContext();
        db.Users.Add(_creator);
        db.Teams.Add(_team);
        db.Problems.Add(_problem);
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static BusinessCalendarRules Rules()
    {
        var hours = new WorkingRange(new TimeOnly(WorkingStartHour, 0), new TimeOnly(WorkingEndHour, 0));
        return new BusinessCalendarRules(
            TimeZoneInfo.Utc,
            new Dictionary<DayOfWeek, WorkingRange> { [DayOfWeek.Friday] = hours },
            new HashSet<DateOnly>());
    }

    private ProblemSlaTimer NewTimer() => ProblemSlaTimer.Start(_problem.Id, TargetMinutes, WarningPercent, Now, Rules());

    [Fact]
    public async Task Targets_are_seeded_for_every_impact()
    {
        await using var db = database.CreateDbContext();

        var rows = await db.ProblemSlaTargets.AsNoTracking().ToListAsync(Ct);

        var actual = rows.Select(r => (r.Impact, r.TargetMinutes, r.WarningPercent)).OrderBy(r => r.Impact).ToList();
        Assert.Equal(ProblemSlaTarget.Defaults.OrderBy(d => d.Impact).ToList(), actual);
    }

    [Fact]
    public async Task Timer_round_trips()
    {
        var timer = NewTimer();
        await using (var db = database.CreateDbContext())
        {
            db.ProblemSlaTimers.Add(timer);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var loaded = await read.ProblemSlaTimers.SingleAsync(t => t.ProblemId == _problem.Id, Ct);
        Assert.Equal((timer.DueAt, timer.WarnAt, timer.RunningSince, timer.ElapsedSeconds), (loaded.DueAt, loaded.WarnAt, loaded.RunningSince, loaded.ElapsedSeconds));
        Assert.Equal(timer.ProblemId, loaded.ProblemId);
    }

    [Fact]
    public async Task Second_timer_for_a_problem_is_rejected()
    {
        await using (var db = database.CreateDbContext())
        {
            db.ProblemSlaTimers.Add(NewTimer());
            await db.SaveChangesAsync(Ct);
        }

        await using var again = database.CreateDbContext();
        again.ProblemSlaTimers.Add(NewTimer());
        var error = await Assert.ThrowsAsync<DomainException>(() => again.SaveChangesAsync(Ct));
        var cause = Assert.IsType<DbUpdateException>(error.InnerException);
        Assert.Equal(UniqueViolation, Assert.IsType<PostgresException>(cause.InnerException).SqlState);
    }
}
