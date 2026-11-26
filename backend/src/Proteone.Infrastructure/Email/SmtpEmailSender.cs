using Microsoft.Extensions.Options;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Notifications;

namespace Proteone.Infrastructure.Email;

/// <summary>Production <see cref="IEmailSender"/>: MailKit over SMTP (ADR-0015), configured under <c>Email:Smtp</c>.</summary>
internal sealed class SmtpEmailSender(IOptionsMonitor<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.CurrentValue;
        var timeout = settings.Smtp.EffectiveTimeout;
        try
        {
            await SmtpMailClient.SendAsync(SmtpEndpoint.FromEnvironment(settings), message, timeout, cancellationToken);
        }
        catch (SmtpStageException failure) when (failure.Inner is TimeoutException && failure.Inner.Message == SmtpMailClient.DeadlineMessage(timeout))
        {
            // Our own deadline, not a host shutdown: an ordinary, retryable send failure.
            throw new InvalidOperationException(failure.Inner.Message);
        }
        catch (SmtpStageException failure)
        {
            // No inner exception kept: a caller that logs this (e.g. ILogger.LogError(exception, ...)) must never
            // be able to print the original, unsanitized message (which could echo Email:Smtp:Password) via
            // Exception.ToString()'s inner-exception chain.
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(failure.Inner, EmailDelivery.LastErrorMaxLength));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(ErrorSanitizer.Sanitize(exception, EmailDelivery.LastErrorMaxLength));
        }
    }
}
