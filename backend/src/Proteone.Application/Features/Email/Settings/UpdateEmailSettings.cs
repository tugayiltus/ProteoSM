using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Features.Users;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Email;

namespace Proteone.Application.Features.Email.Settings;

/// <summary>A null or empty password keeps the stored one: the page never shows it, so an empty field means "unchanged".</summary>
public sealed record UpdateEmailSettingsCommand(
    EmailProvider Provider,
    string? SenderName,
    string SenderAddress,
    string Host,
    int Port,
    EmailSecurity Security,
    string? UserName,
    string? Password,
    uint Version);

internal sealed class UpdateEmailSettingsValidator : AbstractValidator<UpdateEmailSettingsCommand>
{
    public UpdateEmailSettingsValidator()
    {
        RuleFor(c => c.Provider).IsInEnum();
        RuleFor(c => c.Security).IsInEnum();
        RuleFor(c => c.SenderAddress).ValidEmail();
        RuleFor(c => c.SenderName).MaximumLength(EmailSettings.SenderNameMaxLength);
        RuleFor(c => c.UserName).MaximumLength(EmailSettings.UserNameMaxLength);
        RuleFor(c => c.Password).MaximumLength(EmailSettings.PasswordMaxLength);
        // Google is a preset: its server, port and security replace what was sent, so only the login is checked.
        When(c => c.Provider == EmailProvider.Smtp, () =>
        {
            RuleFor(c => c.Host).NotEmpty().MaximumLength(EmailSettings.HostMaxLength);
            RuleFor(c => c.Port).InclusiveBetween(EmailSettings.MinPort, EmailSettings.MaxPort);
            // A login over an unencrypted connection would send the password in clear text.
            RuleFor(c => c.UserName)
                .Must(string.IsNullOrWhiteSpace)
                .When(c => c.Security == EmailSecurity.None)
                .WithErrorCode(ErrorCodes.EmailSettingsPlainTextLogin);
        });
        When(c => c.Provider == EmailProvider.Google, () => RuleFor(c => c.UserName!).ValidEmail());
    }
}

/// <summary>
/// Saves the outgoing mail settings. Active settings are in use at once, so they must pass a test send first; a failing
/// test refuses the save and the working settings stay (the ADR-0030 rule). Inactive settings are saved untested.
/// </summary>
public sealed class UpdateEmailSettingsHandler(
    IAppDbContext db,
    IEmailSecretProtector secrets,
    IEmailEnvironment environment,
    EmailSettingsTester tester,
    IAuditLog audit,
    IValidator<UpdateEmailSettingsCommand> validator,
    TimeProvider clock)
{
    public async Task<EmailSettingsDto> HandleAsync(UpdateEmailSettingsCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var settings = await db.EmailSettings.SingleAsync(cancellationToken);
        db.SetExpectedVersion(settings, command.Version);
        var passwordChanged = !string.IsNullOrEmpty(command.Password);
        var now = clock.GetUtcNow();
        settings.Update(command.Provider, command.SenderName, command.SenderAddress, command.Host, command.Port, command.Security, command.UserName,
            passwordChanged ? secrets.Protect(EmailSettings.NormalizePassword(command.Provider, command.Password!)) : null, now);
        // The password never goes into the audit: only whether one was sent.
        audit.Record(AuditActions.EmailSettingsUpdated, AuditEntityTypes.EmailSettings, settings.Id, newValues: new
        {
            settings.Provider,
            settings.SenderName,
            settings.SenderAddress,
            settings.Host,
            settings.Port,
            settings.Security,
            settings.UserName,
            PasswordChanged = passwordChanged,
        });

        if (settings.IsActive)
        {
            var test = await tester.TestAsync(settings, cancellationToken);
            if (!test.Ok)
            {
                throw new DomainException(test.ErrorCode ?? ErrorCodes.EmailSettingsSendFailed, "The new values of the active mail settings failed the test send.");
            }

            settings.RecordTest(succeeded: true, errorCode: null, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return EmailSettingsMapping.ToDto(await db.EmailSettings.AsNoTracking().SingleAsync(cancellationToken), environment.Current);
    }
}
