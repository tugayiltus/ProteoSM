using Tgysm.Application.Abstractions;
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
        var (problem, viewer) = await scope.LoadForWorkAsync(number, cancellationToken);
        db.SetExpectedVersion(problem, version);
        await change(problem, viewer, clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return await DetailAsync(problem.Id, viewer, cancellationToken);
    }

    public Task<ProblemDetail> DetailAsync(Guid problemId, ProblemViewer viewer, CancellationToken cancellationToken) =>
        ProblemQueries.LoadDetailAsync(db, problemId, viewer, cancellationToken);
}
