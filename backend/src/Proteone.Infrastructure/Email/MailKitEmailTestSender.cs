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
    private const string RedactedValue = "[redacted]";

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
                await SmtpMailClient.SendAsync(endpoint, message, settings.Smtp.EffectiveTimeout, cancellationToken);
            }

            return new EmailTestResult(true, null, null);
        }
        catch (SmtpStageException failure)
        {
            return new EmailTestResult(false, SmtpFailureClassifier.Classify(failure), Detail(failure.Inner, transport.Password));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new EmailTestResult(false, ErrorCodes.EmailSettingsSendFailed, Detail(exception, transport.Password));
        }
    }

    // Some servers echo the AUTH line, so the plain password is replaced too, not only credential-shaped text. It is
    // replaced before the text is shortened, so no part of it can stay at the cut.
    private static string Detail(Exception exception, string? password)
    {
        var detail = ErrorSanitizer.Sanitize(exception, int.MaxValue);
        if (!string.IsNullOrEmpty(password))
        {
            detail = detail.Replace(password, RedactedValue, StringComparison.Ordinal);
        }

        return detail.Length > EmailSettings.TestDetailMaxLength ? detail[..EmailSettings.TestDetailMaxLength] : detail;
    }
}
