using Microsoft.Extensions.Options;
using MimeKit;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Notifications;

namespace Proteone.Infrastructure.Email;

/// <summary>
/// Development/test <see cref="IEmailSender"/>: writes one <c>.eml</c> file per message under <c>Email:PickupFolder</c>
/// instead of talking to SMTP (ADR-0015) — no live SMTP dependency for local runs or CI.
/// </summary>
internal sealed class PickupFolderEmailSender(IOptionsMonitor<EmailOptions> options) : IEmailSender
{
    private const string FileExtension = ".eml";

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            await WriteAsync(MailboxAddress.Parse(options.CurrentValue.From), message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(exception, EmailDelivery.LastErrorMaxLength));
        }
    }

    /// <summary>Writes one <c>.eml</c> file with <paramref name="from"/> as sender. Failures are not sanitized here; the caller does it.</summary>
    internal async Task WriteAsync(MailboxAddress from, EmailMessage message, CancellationToken cancellationToken)
    {
        var folderSetting = options.CurrentValue.PickupFolder;
        if (string.IsNullOrWhiteSpace(folderSetting))
        {
            throw new InvalidOperationException("Email:PickupFolder must be set when Email:Mode is 'Pickup'.");
        }

        var folder = Path.GetFullPath(folderSetting);
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, Guid.NewGuid().ToString("N") + FileExtension);

        var mime = EmailMimeMessageFactory.Build(from, message);
        await using var stream = File.Create(path);
        await mime.WriteToAsync(stream, cancellationToken);
    }
}
