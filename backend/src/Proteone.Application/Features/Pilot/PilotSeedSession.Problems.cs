using Proteone.Application.Features.Problems;
using Proteone.Domain.Problems;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Features.Pilot;

/// <summary>Where a seeded problem ends up.</summary>
internal enum PilotProblemPlan
{
    Open,
    Investigating,
    KnownError,
    Resolved,
    Cancelled,
}

/// <summary>Problems of the pilot seed: every state, linked incidents, a fixing change on resolved ones, an article on known errors.</summary>
internal sealed partial class PilotSeedSession
{
    private const int MaxExtraIncidentsPerProblem = 3;
    private static readonly (int Min, int Max) ProblemAge = (MinutesPerDay, 20 * MinutesPerDay);
    private static readonly (int Min, int Max) ProblemStepDelay = (30, MinutesPerDay);

    // One block per cycle; the first five cover every state at any scale.
    private static readonly PilotProblemPlan[] ProblemPlanBlock =
    [
        PilotProblemPlan.Open, PilotProblemPlan.Investigating, PilotProblemPlan.KnownError, PilotProblemPlan.Resolved, PilotProblemPlan.Cancelled,
        PilotProblemPlan.KnownError, PilotProblemPlan.Investigating, PilotProblemPlan.Open,
    ];

    private int _nextIncident;

    private async Task SeedProblemsAsync()
    {
        var count = PilotSeedVolumes.Scaled(PilotSeedVolumes.Problems, scale);
        for (var index = 0; index < count; index++)
        {
            await SeedProblemAsync(index, ProblemPlanBlock[index % ProblemPlanBlock.Length]);
        }
    }

    private async Task SeedProblemAsync(int index, PilotProblemPlan plan)
    {
        var owner = Pick(_intakeMembers);
        var at = Clamp(realNow.AddMinutes(-_random.Next(ProblemAge.Min, ProblemAge.Max)));
        var title = $"{PilotSeedProblemData.ProblemTitles[index % PilotSeedProblemData.ProblemTitles.Length]} ({index + 1})";
        var create = new CreateProblemCommand(title, PilotSeedProblemData.Description, Pick(Enum.GetValues<Level>()), _intakeTeamId, owner, NextIncident());
        var problem = await ActAsync<CreateProblemHandler, ProblemDetail>(owner, at, (h, ct) => h.HandleAsync(create, ct));
        problem = await MaybeSetProblemServiceAsync(index, problem, owner, at);

        for (var extra = _random.Next(MaxExtraIncidentsPerProblem + 1); extra > 0; extra--)
        {
            var link = new AddProblemTicketCommand(problem.Number, NextIncident());
            await ActAsync<AddProblemTicketHandler, IReadOnlyList<ProblemTicketItem>>(owner, at, (h, ct) => h.HandleAsync(link, ct));
        }

        if (plan == PilotProblemPlan.Open)
        {
            return;
        }

        at = Later(at, ProblemStepDelay);
        if (plan == PilotProblemPlan.Cancelled)
        {
            await ActAsync<CancelProblemHandler, ProblemDetail>(owner, at, (h, ct) => h.HandleAsync(new CancelProblemCommand(problem.Number, problem.Version, PilotSeedProblemData.CancelReason), ct));
            return;
        }

        problem = await ActAsync<StartProblemHandler, ProblemDetail>(owner, at, (h, ct) => h.HandleAsync(new StartProblemCommand(problem.Number, problem.Version), ct));
        if (plan == PilotProblemPlan.Investigating)
        {
            return;
        }

        at = Later(at, ProblemStepDelay);
        var update = new UpdateProblemCommand(
            problem.Number, problem.Version, problem.Title, problem.Description, problem.Impact, PilotSeedProblemData.RootCause, PilotSeedProblemData.Workaround);
        problem = await ActAsync<UpdateProblemHandler, ProblemDetail>(owner, at, (h, ct) => h.HandleAsync(update, ct));
        problem = await ActAsync<MarkKnownErrorHandler, ProblemDetail>(owner, at, (h, ct) => h.HandleAsync(new MarkKnownErrorCommand(problem.Number, problem.Version), ct));
        problem = await ActAsync<CreateProblemArticleHandler, ProblemDetail>(owner, at, (h, ct) => h.HandleAsync(new CreateProblemArticleCommand(problem.Number, problem.Version), ct));
        if (plan == PilotProblemPlan.KnownError)
        {
            return;
        }

        at = Later(at, ProblemStepDelay);
        var change = new AddProblemChangeCommand(problem.Number, Pick(_intakeChangeNumbers));
        await ActAsync<AddProblemChangeHandler, IReadOnlyList<ProblemChangeItem>>(owner, at, (h, ct) => h.HandleAsync(change, ct));
        await ActAsync<ResolveProblemHandler, ProblemDetail>(owner, at, (h, ct) => h.HandleAsync(new ResolveProblemCommand(problem.Number, problem.Version, PilotSeedProblemData.Resolution), ct));
    }

    /// <summary>The next incident no seeded problem uses yet, so every link is unique.</summary>
    private string NextIncident() => _incidentNumbers[_nextIncident++ % _incidentNumbers.Count];
}
