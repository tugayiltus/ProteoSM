using MailKit;
using MailKit.Net.Smtp;
using Proteone.Application.Abstractions;

namespace Proteone.Infrastructure.Email;

/// <summary>The step of an SMTP exchange that failed.</summary>
internal enum SmtpStage
{
    Connect,
    Authenticate,
    Send,
}

/// <summary>
/// A failure of one <see cref="SmtpMailClient.SendAsync"/> step. The message names only the step; the original failure is
/// <see cref="Inner"/> and must be sanitized before anyone stores or shows it.
/// </summary>
internal sealed class SmtpStageException(SmtpStage stage, Exception inner) : Exception($"SMTP {stage} step failed.", inner)
{
    public SmtpStage Stage { get; } = stage;

    public Exception Inner { get; } = inner;
}

/// <summary>The one MailKit SMTP exchange the Worker sender and the admin test send share (ADR-0015).</summary>
internal static class SmtpMailClient
{
    /// <summary>The <see cref="TimeoutException"/> message when the send's own deadline ends the exchange.</summary>
    public static string DeadlineMessage(TimeSpan timeout) => $"SMTP send timed out after {timeout.TotalSeconds:0} seconds.";

    /// <summary>
    /// Connects, authenticates when <see cref="SmtpEndpoint.UserName"/> is set, sends and disconnects. A cancelled
    /// <paramref name="cancellationToken"/> stops the send with <see cref="OperationCanceledException"/>; every other
    /// failure is a <see cref="SmtpStageException"/> (the own deadline as a <see cref="TimeoutException"/> in it).
    /// </summary>
    public static async Task SendAsync(SmtpEndpoint endpoint, EmailMessage message, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var mime = EmailMimeMessageFactory.Build(endpoint.From, message);

        // One deadline for the whole exchange, not MailKit's per-operation default (2 minutes each): the email
        // sender's row lease is validated against this value, so a send must never outlive it.
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var stage = SmtpStage.Connect;
        try
        {
            using var client = new SmtpClient { Timeout = (int)timeout.TotalMilliseconds };
            await client.ConnectAsync(endpoint.Host, endpoint.Port, endpoint.Socket, deadline.Token);

            if (!string.IsNullOrEmpty(endpoint.UserName))
            {
                stage = SmtpStage.Authenticate;
                await client.AuthenticateAsync(endpoint.UserName, endpoint.Password ?? string.Empty, deadline.Token);
            }

            stage = SmtpStage.Send;
            await client.SendAsync(new SendRequest(mime), deadline.Token);
            await client.DisconnectAsync(true, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Our own deadline, not a host shutdown: an ordinary send failure.
            throw new SmtpStageException(stage, new TimeoutException(DeadlineMessage(timeout)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new SmtpStageException(stage, exception);
        }
    }
}
