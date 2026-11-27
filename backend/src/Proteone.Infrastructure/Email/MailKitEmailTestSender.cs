using Microsoft.Extensions.Options;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Common;
using Proteone.Domain.Email;

namespace Proteone.Infrastructure.Email;

/// <summary>
/// <see cref="IEmailTestSender"/> over the same SMTP exchange as the Worker (<see cref="SmtpMailClient"/>), with the
/// <c>Email:Smtp:TimeoutSeconds</c> deadline. In Pickup mode the test mail goes to an <c>.eml</c> file with the panel sender
/// and counts as passed, because Pickup ignores the panel settings for every send.
/// </summary>
internal sealed class MailKitEmailTestSender(IOptionsMonitor<EmailOptions> options) : IEmailTestSender
{
    public async Task<EmailTestResult> SendTestAsync(SmtpTransport transport, EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        try
        {
            var endpoint = SmtpEndpoint.FromTransport(transport);
            if (string.Equals(settings.Mode, EmailModes.Pickup, StringComparison.Ordinal))
            {
                await new PickupFolderEmailSender(options).WriteAsync(endpoint.From, message, cancellationToken);
            }
            else
            {
                // An admin chooses this host, so the test send refuses link-local and unspecified targets.
                await SmtpMailClient.SendAsync(endpoint, message, settings.Smtp.EffectiveTimeout, cancellationToken, SmtpTargetPolicy.IsAllowed);
            }

            return new EmailTestResult(true, null, null);
        }
        catch (SmtpStageException failure)
        {
            return new EmailTestResult(false, SmtpFailureClassifier.Classify(failure), Detail(failure.Inner, transport));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new EmailTestResult(false, ErrorCodes.EmailSettingsSendFailed, Detail(exception, transport));
        }
    }

    // Redact at full length, then cut: a cut first could leave part of a secret that no longer matches.
    private static string Detail(Exception exception, SmtpTransport transport)
    {
        var detail = SecretRedactor.Redact(ErrorSanitizer.Sanitize(exception, int.MaxValue), transport.UserName, transport.Password);
        return detail.Length > EmailSettings.TestDetailMaxLength ? detail[..EmailSettings.TestDetailMaxLength] : detail;
    }
}
