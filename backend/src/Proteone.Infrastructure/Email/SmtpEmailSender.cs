using System.Net;
using Microsoft.Extensions.Options;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Notifications;

namespace Proteone.Infrastructure.Email;

/// <summary>
/// Production <see cref="IEmailSender"/>: MailKit over SMTP (ADR-0015), with the active panel settings or else the
/// <c>Email:From</c> + <c>Email:Smtp</c> environment settings (<see cref="ActiveSmtpEndpointProvider"/>).
/// </summary>
internal sealed class SmtpEmailSender(IOptionsMonitor<EmailOptions> options, ActiveSmtpEndpointProvider endpoints) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var timeout = options.CurrentValue.Smtp.EffectiveTimeout;

        // Outside the try: an unreadable panel password already has its own safe message.
        var endpoint = await endpoints.GetAsync(cancellationToken);

        // The environment host is the operator's own choice and connects as it always has; an admin-chosen panel host
        // connects only to addresses SmtpTargetPolicy allows, checked again at each send.
        Func<IPAddress, bool>? isAllowedTarget = endpoint.CheckTarget ? SmtpTargetPolicy.IsAllowed : null;
        try
        {
            await SmtpMailClient.SendAsync(endpoint, message, timeout, cancellationToken, isAllowedTarget);
        }
        catch (SmtpStageException failure) when (failure.Inner is TimeoutException && failure.Inner.Message == SmtpMailClient.DeadlineMessage(timeout))
        {
            // Our own deadline, not a host shutdown: an ordinary, retryable send failure.
            throw new InvalidOperationException(failure.Inner.Message);
        }
        catch (SmtpStageException failure)
        {
            // No inner exception kept: a caller that logs this (e.g. ILogger.LogError(exception, ...)) must never
            // be able to print the original, unsanitized message (which could echo the password) via
            // Exception.ToString()'s inner-exception chain.
            throw new InvalidOperationException(LastError(failure.Inner, endpoint));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(LastError(exception, endpoint));
        }
    }

    // Redact at full length, then cut: a cut first could leave part of a secret that no longer matches.
    private static string LastError(Exception exception, SmtpEndpoint endpoint)
    {
        var text = SecretRedactor.Redact(ErrorSanitizer.Sanitize(exception, int.MaxValue), endpoint.UserName, endpoint.Password);
        return text.Length > EmailDelivery.LastErrorMaxLength ? text[..EmailDelivery.LastErrorMaxLength] : text;
    }
}
