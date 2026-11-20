using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Tasks;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Problems;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Problems;

/// <param name="IncidentNumber">Incident to link in the same save; not wired yet, so only null is accepted for now.</param>
public sealed record CreateProblemCommand(
    string Title, string Description, Level Impact, Guid OwnerTeamId, Guid? OwnerId, string? IncidentNumber = null);

internal sealed class CreateProblemValidator : AbstractValidator<CreateProblemCommand>
{
    public CreateProblemValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(Problem.TitleMaxLength);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(Problem.TextMaxLength);
        RuleFor(c => c.Impact).IsInEnum();
        RuleFor(c => c.OwnerTeamId).NotEmpty();
        RuleFor(c => c.IncidentNumber).Null();
    }
}

/// <summary>
/// IT staff or admin open a problem. The owner team must be one of the creator's teams (admins: any active team);
/// an optional owner must be an active member of it (ADR-0034).
/// </summary>
public sealed class CreateProblemHandler(
    IAppDbContext db, IValidator<CreateProblemCommand> validator, ITicketNumberGenerator numbers, ProblemScope scope, ProblemWork work,
    IAuditLog audit, TimeProvider clock)
{
    public async Task<ProblemDetail> HandleAsync(CreateProblemCommand command, CancellationToken cancellationToken)
    {
        var viewer = await scope.StaffViewerAsync(cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        if (viewer.Role != UserRole.Admin && !viewer.TeamIds.Contains(command.OwnerTeamId))
        {
            throw new DomainException(ErrorCodes.ProblemOwnerTeamNotAllowed, "The owner team must be one of your teams.");
        }

        await TaskRules.EnsureActiveTeamAsync(db, command.OwnerTeamId, cancellationToken);
        if (command.OwnerId is { } owner)
        {
            await ProblemRules.EnsureOwnerInTeamAsync(db, command.OwnerTeamId, owner, cancellationToken);
        }

        Problem? problem = null;
        await db.RunInTransactionAsync(async ct =>
        {
            var now = clock.GetUtcNow();
            var number = ProblemNumber.Format(now.Year, await numbers.NextAsync(ProblemNumber.CounterKind, now.Year, ct));
            problem = Problem.Create(number, command.Title, command.Description, command.Impact, command.OwnerTeamId, command.OwnerId, viewer.UserId, now);
            db.Problems.Add(problem);
            ProblemHistory.Record(db, problem, ProblemEventKind.Created, viewer.UserId, now, new { problem.OwnerTeamId, problem.OwnerId, problem.Impact });
            audit.Record(
                AuditActions.ProblemCreated, AuditEntityTypes.Problem, problem.Id,
                newValues: new { problem.Number, problem.OwnerTeamId, problem.OwnerId, problem.Impact });
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return await work.DetailAsync(problem!.Id, viewer, cancellationToken);
    }
}
