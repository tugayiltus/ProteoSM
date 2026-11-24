using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;

namespace Proteone.Application.Features.Licences;

public sealed record AssignLicenceCommand(Guid LicenceId, Guid? UserId, Guid? AssetId);

internal sealed class AssignLicenceValidator : AbstractValidator<AssignLicenceCommand>
{
    public AssignLicenceValidator()
    {
        RuleFor(c => c.UserId).NotEmpty().When(c => c.AssetId is null);
        RuleFor(c => c.AssetId).Empty().When(c => c.UserId is not null);
    }
}

/// <summary>Gives one seat under the seat lock, so parallel assignments never exceed the quantity.</summary>
public sealed class AssignLicenceHandler(
    IAppDbContext db, ICurrentActor actor, IValidator<AssignLicenceCommand> validator, IAuditLog audit, ICompanyClock companyClock, TimeProvider clock)
{
    public async Task<LicenceDetail> HandleAsync(AssignLicenceCommand command, CancellationToken cancellationToken)
    {
        var (actorId, _) = await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var now = clock.GetUtcNow();
        var today = companyClock.DateOf(now);
        await db.RunSerializedAsync(SerializationLocks.LicenceSeats, async ct =>
        {
            var licence = await LicenceQueries.LoadAsync(db, command.LicenceId, ct);
            if (await db.LicenceAssignments.AnyAsync(a => a.LicenceId == licence.Id && a.ReleasedAt == null
                && (command.UserId != null ? a.UserId == command.UserId : a.AssetId == command.AssetId), ct))
            {
                throw new DomainException(ErrorCodes.LicenceAlreadyAssigned, LicenceQueries.AlreadyAssignedMessage);
            }

            var used = await LicenceQueries.UsedSeatsAsync(db, licence.Id, ct);
            var assignment = command.UserId is { } userId
                ? licence.AssignTo(
                    await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException(AuditEntityTypes.User),
                    used, today, actorId, now)
                : licence.AssignTo(
                    await db.Assets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == command.AssetId, ct) ?? throw new NotFoundException(AuditEntityTypes.Asset),
                    used, today, actorId, now);
            db.LicenceAssignments.Add(assignment);
            audit.Record(AuditActions.LicenceAssigned, AuditEntityTypes.Licence, licence.Id, newValues: new { assignment.UserId, assignment.AssetId });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.LicenceAlreadyAssigned, LicenceQueries.AlreadyAssignedMessage, ct);
        }, cancellationToken);
        return await LicenceQueries.LoadDetailAsync(db, command.LicenceId, today, cancellationToken);
    }
}

public sealed record ReleaseLicenceCommand(Guid LicenceId, Guid AssignmentId);

/// <summary>Frees a seat; an unknown or already released assignment is not found.</summary>
public sealed class ReleaseLicenceHandler(IAppDbContext db, ICurrentActor actor, IAuditLog audit, TimeProvider clock)
{
    public async Task HandleAsync(ReleaseLicenceCommand command, CancellationToken cancellationToken)
    {
        var (actorId, _) = await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        await db.RunSerializedAsync(SerializationLocks.LicenceSeats, async ct =>
        {
            var assignment = await db.LicenceAssignments
                .SingleOrDefaultAsync(a => a.Id == command.AssignmentId && a.LicenceId == command.LicenceId && a.ReleasedAt == null, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Licence);
            assignment.Release(actorId, clock.GetUtcNow());
            audit.Record(AuditActions.LicenceReleased, AuditEntityTypes.Licence, command.LicenceId, oldValues: new { assignment.UserId, assignment.AssetId });
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
    }
}
