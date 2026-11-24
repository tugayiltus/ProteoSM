using Proteone.Application.Abstractions;
using Proteone.Application.Features.Services;
using Proteone.Domain.Audit;
using Proteone.Domain.Changes;

namespace Proteone.Application.Features.Changes;

public sealed record SetChangeServiceCommand(string Number, Guid? ServiceId, uint Version);

/// <summary>
/// Sets or clears the change's service in any state, for those who may work the change. It skips the lifecycle guards
/// and leaves status, approvals and the plan fingerprint untouched.
/// </summary>
public sealed class SetChangeServiceHandler(IAppDbContext db, ChangeWork work, IAuditLog audit)
{
    public Task<ChangeDetail> HandleAsync(SetChangeServiceCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, async (change, viewer, now, ct) =>
        {
            var result = await ServiceAssignment.ApplyAsync(db, change.ServiceId, command.ServiceId, id => change.SetService(id, now), ct);
            if (result is null)
            {
                return;
            }

            var kind = result.Cleared ? ChangeEventKind.ServiceCleared : ChangeEventKind.ServiceSet;
            ChangeHistory.Record(db, change, kind, viewer.UserId, now, result.Data);
            audit.Record(AuditActions.ChangeServiceSet, AuditEntityTypes.Change, change.Id, newValues: result.Data);
        }, cancellationToken);
}
