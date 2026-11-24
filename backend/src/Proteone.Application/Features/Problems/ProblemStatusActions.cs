using FluentValidation;
using Proteone.Application.Abstractions;
using Proteone.Domain.Audit;
using Proteone.Domain.Problems;

namespace Proteone.Application.Features.Problems;

public sealed record StartProblemCommand(string Number, uint Version);

public sealed record MarkKnownErrorCommand(string Number, uint Version);

public sealed record ResolveProblemCommand(string Number, uint Version, string Resolution);

public sealed record CancelProblemCommand(string Number, uint Version, string Reason);

internal sealed class ResolveProblemValidator : AbstractValidator<ResolveProblemCommand>
{
    public ResolveProblemValidator() => RuleFor(c => c.Resolution).NotEmpty().MaximumLength(Problem.TextMaxLength);
}

internal sealed class CancelProblemValidator : AbstractValidator<CancelProblemCommand>
{
    public CancelProblemValidator() => RuleFor(c => c.Reason).NotEmpty().MaximumLength(Problem.ReasonMaxLength);
}

/// <summary>One status change: history row and audit entry in the same save.</summary>
internal static class ProblemStatusChange
{
    public static void Record(IAppDbContext db, IAuditLog audit, Problem problem, ProblemStatus before, ProblemEventKind kind, Guid actorId, DateTimeOffset now, object? data = null)
    {
        ProblemHistory.Record(db, problem, kind, actorId, now, data);
        audit.Record(AuditActions.ProblemStatusChanged, AuditEntityTypes.Problem, problem.Id, new { Status = before }, new { problem.Status });
    }
}

public sealed class StartProblemHandler(IAppDbContext db, ProblemWork work, IAuditLog audit)
{
    public Task<ProblemDetail> HandleAsync(StartProblemCommand command, CancellationToken cancellationToken) =>
        work.RunStateChangeAsync(command.Number, command.Version, (problem, viewer, now) =>
        {
            var before = problem.Status;
            problem.Start(now);
            ProblemStatusChange.Record(db, audit, problem, before, ProblemEventKind.Started, viewer.UserId, now);
            return Task.CompletedTask;
        }, cancellationToken);
}

/// <summary>Needs both a root cause and a workaround already saved on the problem.</summary>
public sealed class MarkKnownErrorHandler(IAppDbContext db, ProblemWork work, IAuditLog audit)
{
    public Task<ProblemDetail> HandleAsync(MarkKnownErrorCommand command, CancellationToken cancellationToken) =>
        work.RunStateChangeAsync(command.Number, command.Version, (problem, viewer, now) =>
        {
            var before = problem.Status;
            problem.MarkKnownError(now);
            ProblemStatusChange.Record(db, audit, problem, before, ProblemEventKind.KnownError, viewer.UserId, now);
            return Task.CompletedTask;
        }, cancellationToken);
}

public sealed class ResolveProblemHandler(IAppDbContext db, IValidator<ResolveProblemCommand> validator, ProblemWork work, IAuditLog audit)
{
    public async Task<ProblemDetail> HandleAsync(ResolveProblemCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunStateChangeAsync(command.Number, command.Version, (problem, viewer, now) =>
        {
            var before = problem.Status;
            problem.Resolve(command.Resolution, now);
            ProblemStatusChange.Record(db, audit, problem, before, ProblemEventKind.Resolved, viewer.UserId, now, new { resolution = problem.Resolution });
            return Task.CompletedTask;
        }, cancellationToken);
    }
}

public sealed class CancelProblemHandler(IAppDbContext db, IValidator<CancelProblemCommand> validator, ProblemWork work, IAuditLog audit)
{
    public async Task<ProblemDetail> HandleAsync(CancelProblemCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunStateChangeAsync(command.Number, command.Version, (problem, viewer, now) =>
        {
            var before = problem.Status;
            problem.Cancel(command.Reason, now);
            ProblemStatusChange.Record(db, audit, problem, before, ProblemEventKind.Cancelled, viewer.UserId, now, new { reason = problem.CancelReason });
            return Task.CompletedTask;
        }, cancellationToken);
    }
}
