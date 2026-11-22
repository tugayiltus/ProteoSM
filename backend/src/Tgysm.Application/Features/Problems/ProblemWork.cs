using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Problems;

namespace Tgysm.Application.Features.Problems;

/// <summary>
/// Common frame of a problem action, as <c>TaskWork</c> for tasks: load for work (403 outside the owner team), check the
/// expected version, run the change, save once (problem, history, audit together) and return the fresh detail.
/// </summary>
public sealed class ProblemWork(IAppDbContext db, ProblemScope scope, TimeProvider clock)
{
    public async Task<ProblemDetail> RunAsync(
        string number, uint version, Func<Problem, ProblemViewer, DateTimeOffset, Task> change, CancellationToken cancellationToken)
    {
        var (problem, viewer) = await ApplyAsync(number, version, change, cancellationToken);
        return await DetailAsync(problem.Id, viewer, cancellationToken);
    }

    /// <summary>
    /// Like <see cref="RunAsync"/>, under <see cref="SerializationLocks.ProblemLink"/>: a state change and a link write
    /// for the problem never interleave, so no link lands on a problem that is being resolved or cancelled.
    /// </summary>
    public async Task<ProblemDetail> RunStateChangeAsync(
        string number, uint version, Func<Problem, ProblemViewer, DateTimeOffset, Task> change, CancellationToken cancellationToken)
    {
        Guid problemId = Guid.Empty;
        ProblemViewer? viewer = null;
        await db.RunSerializedAsync(SerializationLocks.ProblemLink, async ct =>
        {
            var (problem, actor) = await ApplyAsync(number, version, change, ct);
            (problemId, viewer) = (problem.Id, actor);
        }, cancellationToken);
        return await DetailAsync(problemId, viewer!, cancellationToken);
    }

    private async Task<(Problem Problem, ProblemViewer Viewer)> ApplyAsync(
        string number, uint version, Func<Problem, ProblemViewer, DateTimeOffset, Task> change, CancellationToken cancellationToken)
    {
        var (problem, viewer) = await scope.LoadForWorkAsync(number, cancellationToken);
        db.SetExpectedVersion(problem, version);
        await change(problem, viewer, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return (problem, viewer);
    }

    public Task<ProblemDetail> DetailAsync(Guid problemId, ProblemViewer viewer, CancellationToken cancellationToken) =>
        ProblemQueries.LoadDetailAsync(db, problemId, viewer, cancellationToken);
}
