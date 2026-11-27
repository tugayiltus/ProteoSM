using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Changes;

namespace ProteoSM.Application.Features.Changes;

/// <summary>
/// Common frame of a change action: inside the change lock, load for work (404 outside), check the expected version,
/// run the change, save once (change, history, audit, outbox together) and return the fresh detail.
/// </summary>
public sealed class ChangeWork(IAppDbContext db, ChangeScope scope, ChangeConflicts conflicts, TimeProvider clock)
{
    public async Task<ChangeDetail> RunAsync(
        string number, uint version, Func<ChangeRequest, ChangeViewer, DateTimeOffset, CancellationToken, Task> change, CancellationToken cancellationToken)
    {
        ChangeDetail? detail = null;
        await db.RunSerializedAsync(SerializationLocks.ChangeWork, async ct =>
        {
            var (loaded, viewer) = await scope.LoadForWorkAsync(number, ct);
            db.SetExpectedVersion(loaded, version);
            await change(loaded, viewer, clock.GetUtcNow(), ct);
            await db.SaveChangesAsync(ct);
            detail = await DetailAsync(loaded.Id, viewer, ct);
        }, cancellationToken);
        return detail!;
    }

    public Task<ChangeDetail> DetailAsync(Guid changeId, ChangeViewer viewer, CancellationToken cancellationToken) =>
        ChangeQueries.LoadDetailAsync(db, conflicts, changeId, viewer, cancellationToken);
}
