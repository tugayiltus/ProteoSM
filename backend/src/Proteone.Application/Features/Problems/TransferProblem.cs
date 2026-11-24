using FluentValidation;
using Proteone.Application.Abstractions;
using Proteone.Application.Features.Tasks;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Problems;

namespace Proteone.Application.Features.Problems;

public sealed record TransferProblemCommand(string Number, uint Version, Guid TeamId, string Reason);

public sealed record SetProblemOwnerCommand(string Number, uint Version, Guid? OwnerId);

internal sealed class TransferProblemValidator : AbstractValidator<TransferProblemCommand>
{
    public TransferProblemValidator()
    {
        RuleFor(c => c.TeamId).NotEmpty();
        RuleFor(c => c.Reason).NotEmpty().MaximumLength(Problem.ReasonMaxLength);
    }
}

/// <summary>
/// Moves the problem to another active team with a reason; the new team picks its own owner, so the owner is cleared.
/// Moving it to the team that already owns it is refused, so the owner is not cleared by accident.
/// </summary>
public sealed class TransferProblemHandler(IAppDbContext db, IValidator<TransferProblemCommand> validator, ProblemWork work, IAuditLog audit)
{
    public async Task<ProblemDetail> HandleAsync(TransferProblemCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, async (problem, viewer, now) =>
        {
            if (problem.OwnerTeamId == command.TeamId)
            {
                throw new DomainException(ErrorCodes.ProblemTransferSameTeam, "The problem already belongs to this team.");
            }

            await TaskRules.EnsureActiveTeamAsync(db, command.TeamId, cancellationToken);
            var before = new { problem.OwnerTeamId, problem.OwnerId };
            problem.Transfer(command.TeamId, now);
            ProblemHistory.Record(db, problem, ProblemEventKind.Transferred, viewer.UserId, now, new { command.TeamId, reason = command.Reason.Trim() });
            audit.Record(AuditActions.ProblemTransferred, AuditEntityTypes.Problem, problem.Id, before, new { command.TeamId, Reason = command.Reason.Trim() });
        }, cancellationToken);
    }
}

/// <summary>Sets or clears the owner inside the owner team.</summary>
public sealed class SetProblemOwnerHandler(IAppDbContext db, ProblemWork work, IAuditLog audit)
{
    public Task<ProblemDetail> HandleAsync(SetProblemOwnerCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, async (problem, viewer, now) =>
        {
            if (command.OwnerId is { } owner)
            {
                await ProblemRules.EnsureOwnerInTeamAsync(db, problem.OwnerTeamId, owner, cancellationToken);
            }

            var before = problem.OwnerId;
            problem.SetOwner(command.OwnerId, now);
            ProblemHistory.Record(db, problem, ProblemEventKind.OwnerChanged, viewer.UserId, now, new { command.OwnerId });
            audit.Record(AuditActions.ProblemOwnerChanged, AuditEntityTypes.Problem, problem.Id, new { OwnerId = before }, new { command.OwnerId });
        }, cancellationToken);
}
