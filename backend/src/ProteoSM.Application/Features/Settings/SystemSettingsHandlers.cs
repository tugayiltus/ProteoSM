using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Settings;

namespace ProteoSM.Application.Features.Settings;

public sealed record SystemSettingsDto(int LicenceNoticeDays, int WarrantyWindowDays, uint Version);

public sealed class GetSystemSettingsHandler(IAppDbContext db)
{
    public async Task<SystemSettingsDto> HandleAsync(CancellationToken cancellationToken) =>
        await db.SystemSettings.AsNoTracking()
            .Select(s => new SystemSettingsDto(s.LicenceNoticeDays, s.WarrantyWindowDays, s.Version))
            .SingleAsync(cancellationToken);
}

public sealed record UpdateSystemSettingsCommand(int LicenceNoticeDays, int WarrantyWindowDays, uint Version);

internal sealed class UpdateSystemSettingsValidator : AbstractValidator<UpdateSystemSettingsCommand>
{
    public UpdateSystemSettingsValidator()
    {
        RuleFor(c => c.LicenceNoticeDays).InclusiveBetween(SystemSettings.MinDays, SystemSettings.MaxDays);
        RuleFor(c => c.WarrantyWindowDays).InclusiveBetween(SystemSettings.MinDays, SystemSettings.MaxDays);
    }
}

public sealed class UpdateSystemSettingsHandler(IAppDbContext db, IValidator<UpdateSystemSettingsCommand> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<SystemSettingsDto> HandleAsync(UpdateSystemSettingsCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var settings = await db.SystemSettings.SingleAsync(cancellationToken);
        db.SetExpectedVersion(settings, command.Version);
        var before = AuditValues(settings);
        settings.Update(command.LicenceNoticeDays, command.WarrantyWindowDays, clock.GetUtcNow());
        audit.Record(AuditActions.SystemSettingsUpdated, AuditEntityTypes.SystemSettings, settings.Id, before, AuditValues(settings));
        await db.SaveChangesAsync(cancellationToken);
        return new SystemSettingsDto(settings.LicenceNoticeDays, settings.WarrantyWindowDays, settings.Version);
    }

    private static object AuditValues(SystemSettings s) => new { s.LicenceNoticeDays, s.WarrantyWindowDays };
}
