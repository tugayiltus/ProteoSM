using Microsoft.Extensions.Time.Testing;
using Tgysm.Application.Reports;
using Tgysm.Domain.Problems;
using Tgysm.Domain.Reports;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Reports;

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

    private int _counter;
    private int _incidents;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public ReportWorld World { get; } = world;

    /// <summary>Stores a problem created at <paramref name="createdAt"/> that reaches <paramref name="ending"/> <paramref name="after"/> later, with linked incidents.</summary>
    public async Task<Problem> ProblemAsync(
        ProblemEnding ending, DateTimeOffset createdAt, Level impact = Level.Medium, TimeSpan? after = null, Guid? teamId = null, int linkedIncidents = 0)
    {
        var endedAt = createdAt + (after ?? TimeSpan.FromHours(1));
        var problem = Problem.Create(
            $"PRB-2026-{++_counter:D6}", $"Problem {_counter}", "belirti", impact, teamId ?? World.DeskTeam.Id, null, World.Desk.Id, createdAt);
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

    public async Task<ReportResult> RunAsync(Guid userId, ReportFilter? filter = null, DateTimeOffset? now = null)
    {
        await using var db = World.Db();
        var handler = new GetReportHandler(
            new ReportScope(db, new FixedActor(userId)), new TicketReportSource(db, new TestClock()), db, new FakeTimeProvider(now ?? AssetReportFixture.Now), new TestClock());
        return await handler.HandleAsync(new GetReportQuery(ReportName.Problems, filter ?? new ReportFilter()), Ct);
    }
}
