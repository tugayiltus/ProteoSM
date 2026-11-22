using FluentValidation;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Features.Tasks;
using Tgysm.Application.Features.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Problems;
using Tgysm.Domain.Tickets;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Problems;

/// <param name="IncidentNumber">Incident to link in the same save ("Problem oluştur" from an incident).</param>
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
        RuleFor(c => c.IncidentNumber).NotEmpty().MaximumLength(TicketNumber.MaxLength).When(c => c.IncidentNumber is not null);
    }
}

/// <summary>
/// IT staff or admin open a problem. The owner team must be one of the creator's teams (admins: any active team);
/// an optional owner must be an active member of it (ADR-0034). An incident number links that incident in the same
/// save under the link lock; if the link fails, no problem is saved either.
/// </summary>
public sealed class CreateProblemHandler(
    IAppDbContext db, IValidator<CreateProblemCommand> validator, ITicketNumberGenerator numbers, ProblemScope scope, ProblemWork work,
    TicketScope tickets, IAuditLog audit, TimeProvider clock)
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
        async Task CreateAsync(CancellationToken ct)
        {
            var incident = command.IncidentNumber is { } incidentNumber
                ? (await ProblemLinkRules.FindVisibleTicketAsync(tickets, viewer, incidentNumber, ct)).Ticket
                : null;
            var now = clock.GetUtcNow();
            var number = ProblemNumber.Format(now.Year, await numbers.NextAsync(ProblemNumber.CounterKind, now.Year, ct));
            problem = Problem.Create(number, command.Title, command.Description, command.Impact, command.OwnerTeamId, command.OwnerId, viewer.UserId, now);
            db.Problems.Add(problem);
            ProblemHistory.Record(db, problem, ProblemEventKind.Created, viewer.UserId, now, new { problem.OwnerTeamId, problem.OwnerId, problem.Impact });
            audit.Record(
                AuditActions.ProblemCreated, AuditEntityTypes.Problem, problem.Id,
                newValues: new { problem.Number, problem.OwnerTeamId, problem.OwnerId, problem.Impact });
            if (incident is null)
            {
                await db.SaveChangesAsync(ct);
                return;
            }

            // The creator is in the owner team (checked above), so the problem-side link rule always admits them.
            await ProblemLinkRules.AddIncidentAsync(db, audit, problem, incident, viewer.UserId, now, ct);
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.ProblemTicketAlreadyLinked, ProblemLinkRules.AlreadyLinkedMessage, ct);
            await TicketActivity.TouchAsync(db, incident.Id, now, ct);
        }

        await (command.IncidentNumber is null
            ? db.RunInTransactionAsync(CreateAsync, cancellationToken)
            : db.RunSerializedAsync(SerializationLocks.ProblemLink, CreateAsync, cancellationToken));
        return await work.DetailAsync(problem!.Id, viewer, cancellationToken);
    }
}
