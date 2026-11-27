using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Email;

namespace ProteoSM.Application.Features.Email.Settings;

public sealed record ActivateEmailSettingsCommand(uint Version);

public sealed record UseEnvironmentEmailCommand(uint Version);

/// <summary>
/// Switches outgoing mail between the panel settings ("Kullanıma al", needs a passing test of the current values) and
/// the environment settings (".env ayarlarına dön", keeps every panel value).
/// </summary>
public sealed class SwitchEmailSourceHandler(IAppDbContext db, IEmailEnvironment environment, IAuditLog audit, TimeProvider clock)
{
    public Task<EmailSettingsDto> ActivateAsync(ActivateEmailSettingsCommand command, CancellationToken cancellationToken) =>
        SwitchAsync(command.Version, AuditActions.EmailSettingsActivated, (settings, now) => settings.Activate(now), cancellationToken);

    public Task<EmailSettingsDto> UseEnvironmentAsync(UseEnvironmentEmailCommand command, CancellationToken cancellationToken) =>
        SwitchAsync(command.Version, AuditActions.EmailSettingsEnvironmentRestored, (settings, now) => settings.UseEnvironment(now), cancellationToken);

    private async Task<EmailSettingsDto> SwitchAsync(uint version, string auditAction, Action<EmailSettings, DateTimeOffset> change, CancellationToken cancellationToken)
    {
        var settings = await db.EmailSettings.SingleAsync(cancellationToken);
        db.SetExpectedVersion(settings, version);
        var wasActive = settings.IsActive;
        change(settings, clock.GetUtcNow());
        audit.Record(auditAction, AuditEntityTypes.EmailSettings, settings.Id,
            oldValues: new { IsActive = wasActive }, newValues: new { settings.IsActive });
        await db.SaveChangesAsync(cancellationToken);
        return EmailSettingsMapping.ToDto(await db.EmailSettings.AsNoTracking().SingleAsync(cancellationToken), environment.Current);
    }
}
