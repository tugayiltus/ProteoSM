using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Licences;

namespace ProteoSM.Application.Features.Licences;

public sealed record CreateLicenceCommand(
    Guid SoftwareId, string Name, LicenceKind Kind, int Quantity, Guid OwnerTeamId, DateOnly? ExpiresOn, string? Notes);

internal sealed class CreateLicenceValidator : AbstractValidator<CreateLicenceCommand>
{
    public CreateLicenceValidator()
    {
        RuleFor(c => c.SoftwareId).NotEmpty();
        RuleFor(c => c.Name).ValidLicenceName();
        RuleFor(c => c.Kind).IsInEnum();
        RuleFor(c => c.Quantity).ValidQuantity();
        RuleFor(c => c.OwnerTeamId).NotEmpty();
        RuleFor(c => c.Notes).ValidLicenceNotes();
    }
}

public sealed class CreateLicenceHandler(
    IAppDbContext db, ICurrentActor actor, IValidator<CreateLicenceCommand> validator, IAuditLog audit, ICompanyClock companyClock, TimeProvider clock)
{
    public async Task<LicenceDetail> HandleAsync(CreateLicenceCommand command, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var now = clock.GetUtcNow();
        Guid licenceId = default;
        await db.RunSerializedAsync(SerializationLocks.LicenceSeats, async ct =>
        {
            var software = await db.Software.SingleOrDefaultAsync(s => s.Id == command.SoftwareId, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Software);
            if (!software.IsActive)
            {
                throw new DomainException(ErrorCodes.SoftwareInactive, "An inactive software product gets no new licence.");
            }

            await LicenceQueries.EnsureOwnerTeamActiveAsync(db, command.OwnerTeamId, ct);
            var licence = Licence.Create(
                command.SoftwareId, command.Name, command.Kind, command.Quantity, command.OwnerTeamId, command.ExpiresOn, command.Notes, now);
            db.Licences.Add(licence);
            audit.Record(AuditActions.LicenceCreated, AuditEntityTypes.Licence, licence.Id,
                newValues: new { licence.SoftwareId, licence.Name, licence.Kind, licence.Quantity, licence.OwnerTeamId, licence.ExpiresOn, licence.Notes });
            await db.SaveChangesAsync(ct);
            licenceId = licence.Id;
        }, cancellationToken);
        return await LicenceQueries.LoadDetailAsync(db, licenceId, companyClock.DateOf(now), cancellationToken);
    }
}

public sealed record UpdateLicenceCommand(
    Guid Id, uint Version, string Name, int Quantity, Guid OwnerTeamId, DateOnly? ExpiresOn, string? Notes);

internal sealed class UpdateLicenceValidator : AbstractValidator<UpdateLicenceCommand>
{
    public UpdateLicenceValidator()
    {
        RuleFor(c => c.Name).ValidLicenceName();
        RuleFor(c => c.Quantity).ValidQuantity();
        RuleFor(c => c.OwnerTeamId).NotEmpty();
        RuleFor(c => c.Notes).ValidLicenceNotes();
    }
}

public sealed class UpdateLicenceHandler(
    IAppDbContext db, ICurrentActor actor, IValidator<UpdateLicenceCommand> validator, IAuditLog audit, ICompanyClock companyClock, TimeProvider clock)
{
    public async Task<LicenceDetail> HandleAsync(UpdateLicenceCommand command, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var now = clock.GetUtcNow();
        await db.RunSerializedAsync(SerializationLocks.LicenceSeats, async ct =>
        {
            var licence = await LicenceQueries.LoadAsync(db, command.Id, ct);
            db.SetExpectedVersion(licence, command.Version);

            // Only a new owner is checked: a licence whose team was deactivated later can still be edited.
            if (command.OwnerTeamId != licence.OwnerTeamId)
            {
                await LicenceQueries.EnsureOwnerTeamActiveAsync(db, command.OwnerTeamId, ct);
            }

            var before = new { licence.Name, licence.Quantity, licence.OwnerTeamId, licence.ExpiresOn, licence.Notes };
            var used = await LicenceQueries.UsedSeatsAsync(db, licence.Id, ct);
            licence.Update(command.Name, command.Quantity, command.OwnerTeamId, command.ExpiresOn, command.Notes, used, now);
            audit.Record(AuditActions.LicenceUpdated, AuditEntityTypes.Licence, licence.Id, before,
                new { licence.Name, licence.Quantity, licence.OwnerTeamId, licence.ExpiresOn, licence.Notes });
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return await LicenceQueries.LoadDetailAsync(db, command.Id, companyClock.DateOf(now), cancellationToken);
    }
}
