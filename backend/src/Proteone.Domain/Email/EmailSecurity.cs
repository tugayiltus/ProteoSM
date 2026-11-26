namespace Proteone.Domain.Email;

/// <summary>Transport security of the SMTP connection. None is allowed only without a login, so no password goes in clear text.</summary>
public enum EmailSecurity
{
    StartTls,
    SslTls,
    None,
}
