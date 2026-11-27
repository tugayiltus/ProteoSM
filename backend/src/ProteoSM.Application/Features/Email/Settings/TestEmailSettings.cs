using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Email;

namespace ProteoSM.Application.Features.Email.Settings;

/// <summary>
/// The test email. Its Turkish text is product content (like the email template defaults), so it lives here and not in
/// the locale files.
/// </summary>
public static class EmailTestMessageText
{
    public const string Subject = "Proteone test e-postası";
    public const string Body = "Bu e-posta, yönetim panelindeki giden posta ayarlarını denemek için gönderildi. Yanıt vermeniz gerekmez.";
}

/// <summary>
/// Sends one test email with the given settings to the signed-in admin. Shared by the test button and by saves of
/// active settings. A failure comes back as a result, and its detail never holds the password.
/// </summary>
public sealed class EmailSettingsTester(IAppDbContext db, ICurrentActor actor, IEmailSecretProtector secrets, IEmailTestSender sender)
{
    public async Task<EmailTestResult> TestAsync(EmailSettings settings, CancellationToken cancellationToken)
    {
        if (!settings.IsConfigured)
        {
            return new EmailTestResult(false, ErrorCodes.EmailSettingsNotConfigured, null);
        }

        string? password = null;
        if (settings.UserName is not null)
        {
            password = Unprotect(settings.PasswordProtected);
            if (password is null)
            {
                return new EmailTestResult(false, ErrorCodes.EmailSettingsPasswordMissing, null);
            }
        }

        var transport = new SmtpTransport(settings.SenderName, settings.SenderAddress, settings.Host, settings.Port, settings.Security, settings.UserName, password);
        var result = await sender.SendTestAsync(transport, await MessageAsync(cancellationToken), cancellationToken);
        return result.Detail is null ? result : result with { Detail = SecretRedactor.Redact(result.Detail, settings.UserName, password) };
    }

    /// <summary>A password the key ring can no longer read (keys lost or rotated) counts as missing: the admin types it again.</summary>
    private string? Unprotect(string? protectedPassword)
    {
        if (protectedPassword is null)
        {
            return null;
        }

        try
        {
            return secrets.Unprotect(protectedPassword);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private async Task<EmailMessage> MessageAsync(CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new InvalidOperationException("A test send needs a signed-in user as the recipient.");
        var recipient = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.Email, u.FirstName, u.LastName })
            .SingleAsync(cancellationToken);
        return new EmailMessage(recipient.Email, $"{recipient.FirstName} {recipient.LastName}", EmailTestMessageText.Subject, EmailTestMessageText.Body);
    }
}

/// <summary>
/// "Test maili gönder": tries the stored settings. A passing test records a fingerprint of these settings; only then can
/// they be activated. The settings load without an expected version, but the save still checks <c>xmin</c>, so a save
/// by another admin during the send makes this a conflict and the new values stay untested.
/// </summary>
public sealed class TestEmailSettingsHandler(IAppDbContext db, EmailSettingsTester tester, IAuditLog audit, TimeProvider clock)
{
    public async Task<EmailTestResult> HandleAsync(CancellationToken cancellationToken)
    {
        var settings = await db.EmailSettings.SingleAsync(cancellationToken);
        var result = await tester.TestAsync(settings, cancellationToken);
        settings.RecordTest(result.Ok, result.ErrorCode, clock.GetUtcNow());
        // The detail is server text: only the outcome and the code go into the audit.
        audit.Record(AuditActions.EmailSettingsTested, AuditEntityTypes.EmailSettings, settings.Id, newValues: new { result.Ok, result.ErrorCode });
        await db.SaveChangesAsync(cancellationToken);
        return result;
    }
}
