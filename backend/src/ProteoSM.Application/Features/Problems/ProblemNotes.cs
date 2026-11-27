using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Problems;

namespace ProteoSM.Application.Features.Problems;

public sealed record AddProblemNoteCommand(string Number, string Text);

internal sealed class AddProblemNoteValidator : AbstractValidator<AddProblemNoteCommand>
{
    public AddProblemNoteValidator() => RuleFor(c => c.Text).NotEmpty().MaximumLength(Problem.TextMaxLength);
}

/// <summary>
/// An internal note in the problem history. It needs no version and only touches <see cref="Problem.UpdatedAt"/>, so a
/// note never conflicts with a state change (same as task comments).
/// </summary>
public sealed class AddProblemNoteHandler(IAppDbContext db, IValidator<AddProblemNoteCommand> validator, ProblemScope scope, TimeProvider clock)
{
    public async Task HandleAsync(AddProblemNoteCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var (problem, viewer) = await scope.LoadForWorkAsync(command.Number, cancellationToken);
        var now = clock.GetUtcNow();
        ProblemHistory.Record(db, problem, ProblemEventKind.Commented, viewer.UserId, now, new { text = command.Text.Trim() });
        await db.Problems.Where(p => p.Id == problem.Id).ExecuteUpdateAsync(s => s.SetProperty(p => p.UpdatedAt, now), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
