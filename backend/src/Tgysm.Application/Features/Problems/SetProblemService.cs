using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Services;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Problems;

namespace Tgysm.Application.Features.Problems;

public sealed record SetProblemServiceCommand(string Number, Guid? ServiceId, uint Version);

/// <summary>
/// Sets or clears the problem's service in any state, also resolved or cancelled, for admins and owner-team members;
/// it leaves the status untouched.
/// </summary>
public sealed class SetProblemServiceHandler(IAppDbContext db, ProblemWork work, IAuditLog audit)
{
    public Task<ProblemDetail> HandleAsync(SetProblemServiceCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, async (problem, viewer, now) =>
        {
            var change = await ServiceAssignment.ApplyAsync(db, problem.ServiceId, command.ServiceId, id => problem.SetService(id, now), cancellationToken);
            if (change is null)
            {
                return;
            }

            var kind = change.Cleared ? ProblemEventKind.ServiceCleared : ProblemEventKind.ServiceSet;
            ProblemHistory.Record(db, problem, kind, viewer.UserId, now, change.Data);
            audit.Record(AuditActions.ProblemServiceSet, AuditEntityTypes.Problem, problem.Id, newValues: change.Data);
        }, cancellationToken);
}
