using MailKit.Security;
using MimeKit;
using Proteone.Application.Abstractions;
using Proteone.Domain.Email;

namespace Proteone.Infrastructure.Email;

/// <summary>
/// Where and how <see cref="SmtpMailClient"/> sends. A class, not a record: a generated <c>ToString</c> would print
/// <see cref="Password"/>.
/// </summary>
internal sealed class SmtpEndpoint
{
    public required MailboxAddress From { get; init; }

    public required string Host { get; init; }

    public required int Port { get; init; }

    public required SecureSocketOptions Socket { get; init; }

    public string? UserName { get; init; }

    public string? Password { get; init; }

    /// <summary>
    /// <c>Email:From</c> + <c>Email:Smtp:*</c>, as the Worker has always used them: <c>UseStartTls = false</c> is
    /// <see cref="SecureSocketOptions.Auto"/>, not "no TLS".
    /// </summary>
    public static SmtpEndpoint FromEnvironment(EmailOptions options) => new()
    {
        From = MailboxAddress.Parse(options.From),
        Host = options.Smtp.Host,
        Port = options.Smtp.Port,
        Socket = options.Smtp.UseStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto,
        UserName = options.Smtp.UserName,
        Password = options.Smtp.Password,
    };

    /// <summary>Panel settings: the chosen security is exact, so a test of <see cref="EmailSecurity.None"/> never upgrades on its own.</summary>
    public static SmtpEndpoint FromTransport(SmtpTransport transport) => new()
    {
        From = new MailboxAddress(transport.SenderName ?? string.Empty, transport.SenderAddress),
        Host = transport.Host,
        Port = transport.Port,
        Socket = transport.Security switch
        {
            EmailSecurity.StartTls => SecureSocketOptions.StartTls,
            EmailSecurity.SslTls => SecureSocketOptions.SslOnConnect,
            EmailSecurity.None => SecureSocketOptions.None,
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport.Security, "Unknown email security."),
        },
        UserName = transport.UserName,
        Password = transport.Password,
    };
}
