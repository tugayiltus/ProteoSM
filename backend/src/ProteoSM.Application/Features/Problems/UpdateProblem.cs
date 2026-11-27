using FluentValidation;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Features.Problems;

public sealed record UpdateProblemCommand(
    string Number, uint Version, string Title, string Description, Level Impact, string? RootCause, string? Workaround);

internal sealed class UpdateProblemValidator : AbstractValidator<UpdateProblemCommand>
{
    public UpdateProblemValidator()
    {
        RuleFor(c => c.Title).NotEmpty().MaximumLength(Problem.TitleMaxLength);
        RuleFor(c => c.Description).NotEmpty().MaximumLength(Problem.TextMaxLength);
        RuleFor(c => c.Impact).IsInEnum();
        RuleFor(c => c.RootCause).MaximumLength(Problem.TextMaxLength);
        RuleFor(c => c.Workaround).MaximumLength(Problem.TextMaxLength);
    }
}

/// <summary>Edits the descriptive fields of an open problem; a known error keeps its root cause and workaround.</summary>
public sealed class UpdateProblemHandler(IAppDbContext db, IValidator<UpdateProblemCommand> validator, ProblemWork work, ProblemSlaTracker slaTracker, IAuditLog audit)
{
    public async Task<ProblemDetail> HandleAsync(UpdateProblemCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, async (problem, viewer, now) =>
        {
            var impactBefore = problem.Impact;
            var before = new { problem.Title, problem.Description, problem.Impact, problem.RootCause, problem.Workaround };
            problem.Edit(command.Title, command.Description, command.Impact, command.RootCause, command.Workaround, now);
            var after = new { problem.Title, problem.Description, problem.Impact, problem.RootCause, problem.Workaround };
            ProblemHistory.Record(db, problem, ProblemEventKind.Updated, viewer.UserId, now);
            audit.Record(AuditActions.ProblemUpdated, AuditEntityTypes.Problem, problem.Id, before, after);
            if (problem.Impact != impactBefore)
            {
                await slaTracker.RetargetAsync(problem, now, cancellationToken);
            }
        }, cancellationToken);
    }
}
