using ProteoSM.Domain.Email;

namespace ProteoSM.Application.Abstractions;

/// <summary>
/// One set of outgoing mail settings to try, with the password in plain text for this call only.
/// <see cref="ToString"/> prints only the host and port, so a log or exception message never shows the password.
/// </summary>
public sealed record SmtpTransport(
    string? SenderName,
    string SenderAddress,
    string Host,
    int Port,
    EmailSecurity Security,
    string? UserName,
    string? Password)
{
    public override string ToString() => $"{Host}:{Port}";
}

/// <summary>
/// The result of a test send. <see cref="ErrorCode"/> is an <c>email_settings.*</c> error code that names the failed step;
/// <see cref="Detail"/> is the server's message, sanitized (credentials replaced by <c>[redacted]</c>) and shortened.
/// </summary>
public sealed record EmailTestResult(bool Ok, string? ErrorCode, string? Detail);

/// <summary>Sends one test email with settings that are not in use yet. It returns a failure as a result and never throws for it.</summary>
public interface IEmailTestSender
{
    Task<EmailTestResult> SendTestAsync(SmtpTransport transport, EmailMessage message, CancellationToken cancellationToken);
}
