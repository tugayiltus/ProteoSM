using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Sla;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Features.Sla.Admin;

public sealed record ProblemTargetDto(Level Impact, int TargetMinutes, int WarningPercent);

/// <summary><paramref name="Version"/> moves whenever any of the three rows changes.</summary>
public sealed record ProblemTargetsDto(IReadOnlyList<ProblemTargetDto> Targets, uint Version);

public sealed record UpdateProblemTargetsCommand(IReadOnlyList<ProblemTargetDto> Targets, uint Version);

internal static class ProblemTargetQueries
{
    public static async Task<ProblemTargetsDto> LoadAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var rows = await db.ProblemSlaTargets.AsNoTracking().ToListAsync(cancellationToken);
        return ToDto(rows);
    }

    public static ProblemTargetsDto ToDto(IReadOnlyCollection<ProblemSlaTarget> rows) =>
        new(
            rows.OrderByDescending(r => r.Impact).Select(r => new ProblemTargetDto(r.Impact, r.TargetMinutes, r.WarningPercent)).ToList(),
            SetVersion(rows));

    // A row whose values stay the same sends no UPDATE, so its xmin does not move. The sum of all three
    // xmin values moves whenever any row is written, which is what editors need to detect.
    public static uint SetVersion(IEnumerable<ProblemSlaTarget> rows)
    {
        unchecked
        {
            return rows.Aggregate(0u, (sum, row) => sum + row.Version);
        }
    }
}

/// <summary>The problem SLA targets, one per impact level.</summary>
public sealed class GetProblemTargetsHandler(IAppDbContext db)
{
    public Task<ProblemTargetsDto> HandleAsync(CancellationToken cancellationToken) => ProblemTargetQueries.LoadAsync(db, cancellationToken);
}

internal sealed class UpdateProblemTargetsValidator : AbstractValidator<UpdateProblemTargetsCommand>
{
    public UpdateProblemTargetsValidator()
    {
        RuleFor(c => c.Targets).NotNull();
        RuleFor(c => c.Targets).Custom(Validate).When(c => c.Targets is not null);
    }

    private static void Validate(IReadOnlyList<ProblemTargetDto> targets, ValidationContext<UpdateProblemTargetsCommand> context)
    {
        if (targets.Any(t => t is null))
        {
            context.AddFailure(new ValidationFailure(nameof(UpdateProblemTargetsCommand.Targets), ErrorCodes.InvalidValue) { ErrorCode = ErrorCodes.InvalidValue });
            return;
        }

        var impacts = targets.Select(t => t.Impact).Order().ToList();
        var complete = impacts.SequenceEqual(Enum.GetValues<Level>().Order());
        var inRange = targets.All(t => t.TargetMinutes is > 0 and <= SlaTarget.MaxTargetMinutes
            && t.WarningPercent is >= SlaTarget.MinWarningPercent and <= SlaTarget.MaxWarningPercent);
        if (!complete || !inRange)
        {
            context.AddFailure(new ValidationFailure(nameof(UpdateProblemTargetsCommand.Targets), ErrorCodes.InvalidValue) { ErrorCode = ErrorCodes.InvalidValue });
        }
    }
}

/// <summary>Replaces all three targets in one save. Running problem timers keep their own copy (spec: problem SLA).</summary>
public sealed class UpdateProblemTargetsHandler(IAppDbContext db, IValidator<UpdateProblemTargetsCommand> validator, IAuditLog audit)
{
    public async Task<ProblemTargetsDto> HandleAsync(UpdateProblemTargetsCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        await db.RunSerializedAsync(SerializationLocks.SlaPolicy, async ct =>
        {
            var rows = await db.ProblemSlaTargets.ToListAsync(ct);
            if (ProblemTargetQueries.SetVersion(rows) != command.Version)
            {
                throw new ConcurrencyException();
            }

            var before = ProblemTargetQueries.ToDto(rows).Targets;
            foreach (var wanted in command.Targets)
            {
                rows.Single(r => r.Impact == wanted.Impact).Change(wanted.TargetMinutes, wanted.WarningPercent);
            }

            var after = ProblemTargetQueries.ToDto(rows).Targets;
            if (before.SequenceEqual(after))
            {
                return;
            }

            audit.Record(AuditActions.ProblemSlaTargetsUpdated, AuditEntityTypes.ProblemSlaTargets, null, new { targets = before }, new { targets = after });
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return await ProblemTargetQueries.LoadAsync(db, cancellationToken);
    }
}
