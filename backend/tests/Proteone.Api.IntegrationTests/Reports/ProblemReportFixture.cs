using Microsoft.Extensions.Time.Testing;
using Proteone.Application.Reports;
using Proteone.Domain.Common;
using Proteone.Domain.Problems;
using Proteone.Domain.Reports;
using Proteone.Domain.Sla;
using Proteone.Domain.Tickets;

namespace Proteone.Api.IntegrationTests.Reports;

/// <summary>How a seeded problem ends: left open, under investigation, a known error, resolved or cancelled.</summary>
internal enum ProblemEnding
{
    Open,
    Investigating,
    KnownError,
    Resolved,
    Cancelled,
}

/// <summary>Problem seeding and report running for the problem report tests; problems go through the real domain transitions.</summary>
internal sealed class ProblemReportFixture(ReportWorld world)
{
    // Linked incidents get numbers far above the ones other fixtures hand out in the same world.
    private const int IncidentNumberBase = 900_000;

    private const int TimerCloseHour = 23;
    private const int MetTargetMinutes = 100_000;
    private const int BreachedTargetMinutes = 1;
    private const int TimerWarningPercent = 80;

    private int _counter;
    private int _incidents;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ReportWorld World { get; } = world;

    /// <summary>Stores a problem created at <paramref name="createdAt"/> that reaches <paramref name="ending"/> <paramref name="after"/> later, with linked incidents.</summary>
    public async Task<Problem> ProblemAsync(
        ProblemEnding ending, DateTimeOffset createdAt, Level impact = Level.Medium, TimeSpan? after = null, Guid? teamId = null, int linkedIncidents = 0, Guid? serviceId = null)
    {
        var endedAt = createdAt + (after ?? TimeSpan.FromHours(1));
        var problem = Problem.Create(
            $"PRB-2026-{++_counter:D6}", $"Problem {_counter}", "belirti", impact, teamId ?? World.DeskTeam.Id, null, World.Desk.Id, createdAt);
        problem.SetService(serviceId, createdAt);
        switch (ending)
        {
            case ProblemEnding.Investigating:
                problem.Start(endedAt);
                break;
            case ProblemEnding.KnownError:
                problem.Edit(problem.Title, problem.Description, impact, "kok neden", "gecici cozum", createdAt);
                problem.MarkKnownError(endedAt);
                break;
            case ProblemEnding.Resolved:
                problem.Resolve("cozuldu", endedAt);
                break;
            case ProblemEnding.Cancelled:
                problem.Cancel("vazgecildi", endedAt);
                break;
        }

        var incidents = new List<Ticket>();
        var links = new List<ProblemTicketLink>();
        for (var i = 0; i < linkedIncidents; i++)
        {
            var incident = Ticket.CreateIncident(
                $"INC-2026-{IncidentNumberBase + ++_incidents:D6}", "Konu", "Aciklama", null, null, World.Employee.Id, World.DeskTeam.Id, createdAt);
            incidents.Add(incident);
            links.Add(ProblemTicketLink.Create(problem.Id, incident.Id, incident.Kind, World.Desk.Id, createdAt));
        }

        await using var db = World.Db();
        db.Tickets.AddRange(incidents);
        db.Problems.Add(problem);
        db.ProblemTicketLinks.AddRange(links);
        await db.SaveChangesAsync(Ct);
        return problem;
    }

    /// <summary>
    /// Stores a known-error timer for <paramref name="problem"/> that ends at <paramref name="stoppedAt"/> with <paramref name="result"/>
    /// (met or breached through the real stop rule, cancelled through cancel); a null result leaves the timer running.
    /// </summary>
    public async Task TimerAsync(Problem problem, SlaResult? result, DateTimeOffset stoppedAt)
    {
        var hours = new WorkingRange(new TimeOnly(0, 0), new TimeOnly(TimerCloseHour, 0));
        var rules = new BusinessCalendarRules(
            TimeZoneInfo.Utc, Enum.GetValues<DayOfWeek>().ToDictionary(d => d, _ => hours), new HashSet<DateOnly>());
        var target = result == SlaResult.Breached ? BreachedTargetMinutes : MetTargetMinutes;
        var timer = ProblemSlaTimer.Start(problem.Id, target, TimerWarningPercent, problem.CreatedAt, rules);
        switch (result)
        {
            case SlaResult.Cancelled:
                timer.Cancel(stoppedAt);
                break;
            case not null:
                timer.Stop(stoppedAt);
                break;
        }

        await using var db = World.Db();
        db.ProblemSlaTimers.Add(timer);
        await db.SaveChangesAsync(Ct);
    }

    public async Task<ReportResult> RunAsync(Guid userId, ReportFilter? filter = null, DateTimeOffset? now = null)
    {
        await using var db = World.Db();
        var handler = new GetReportHandler(
            new ReportScope(db, new FixedActor(userId)), new TicketReportSource(db, new TestClock()), db, new FakeTimeProvider(now ?? AssetReportFixture.Now), new TestClock());
        return await handler.HandleAsync(new GetReportQuery(ReportName.Problems, filter ?? new ReportFilter()), Ct);
    }
}
