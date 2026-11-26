using System.Globalization;
using Proteone.Domain.Common;
using Proteone.Domain.Users;

namespace Proteone.Domain.Email;

/// <summary>
/// The one row that holds the outgoing mail settings an admin enters in the panel. The Worker uses them only while they
/// are active; otherwise it uses the environment settings. The settings can be activated only after the current values
/// passed a test send: the test stores a fingerprint of the values, and any change breaks the match. The password is
/// stored only in protected form.
/// </summary>
public sealed class EmailSettings
{
    public static readonly Guid SingletonId = new("00000000-0000-7000-8000-00000000e3a1");
    public const int SenderNameMaxLength = 100;
    public const int SenderAddressMaxLength = User.EmailMaxLength;
    public const int HostMaxLength = 253;
    public const int UserNameMaxLength = User.EmailMaxLength;
    public const int PasswordMaxLength = 512;
    public const int MinPort = 1;
    public const int MaxPort = 65535;
    public const int StartTlsPort = 587;
    public const int SslTlsPort = 465;
    public const int PlainPort = 25;
    public const int LastTestErrorCodeMaxLength = 100;
    public const int TestDetailMaxLength = 500;
    public const string GoogleHost = "smtp.gmail.com";
    public const int GooglePort = StartTlsPort;
    public const EmailSecurity GoogleSecurity = EmailSecurity.StartTls;

    public Guid Id { get; private init; }
    /// <summary>Optimistic concurrency token, mapped to <c>xmin</c> (ADR-0002).</summary>
    public uint Version { get; private set; }
    public EmailProvider Provider { get; private set; }
    public string? SenderName { get; private set; }
    public string SenderAddress { get; private set; } = string.Empty;
    public string Host { get; private set; } = string.Empty;
    public int Port { get; private set; }
    public EmailSecurity Security { get; private set; }
    public string? UserName { get; private set; }
    public string? PasswordProtected { get; private set; }
    public string? TestedFingerprint { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? LastTestedAt { get; private set; }
    public bool? LastTestSucceeded { get; private set; }
    public string? LastTestErrorCode { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>A test send needs at least a sender address and a server.</summary>
    public bool IsConfigured => SenderAddress.Length > 0 && Host.Length > 0;

    /// <summary>True while the current values are the ones that passed the last successful test.</summary>
    public bool IsTested => TestedFingerprint is not null && TestedFingerprint == Fingerprint();

    public static EmailSettings CreateDefault(DateTimeOffset now) => new()
    {
        Id = SingletonId,
        Provider = EmailProvider.Google,
        Host = GoogleHost,
        Port = GooglePort,
        Security = GoogleSecurity,
        UpdatedAt = now,
    };

    public static int DefaultPort(EmailSecurity security) => security switch
    {
        EmailSecurity.SslTls => SslTlsPort,
        EmailSecurity.None => PlainPort,
        _ => StartTlsPort,
    };

    /// <summary>
    /// Google app passwords never contain whitespace, but Google shows them in groups of four, so a pasted value often has
    /// spaces. Other servers get the password exactly as typed.
    /// </summary>
    public static string NormalizePassword(EmailProvider provider, string password) =>
        provider == EmailProvider.Google ? string.Concat(password.Where(c => !char.IsWhiteSpace(c))) : password;

    /// <summary>
    /// A null <paramref name="protectedPassword"/> keeps the stored one (the UI never shows it, so an empty field means
    /// "unchanged"). Without a user name there is no login, so the stored password goes away.
    /// </summary>
    public void Update(
        EmailProvider provider,
        string? senderName,
        string senderAddress,
        string host,
        int port,
        EmailSecurity security,
        string? userName,
        string? protectedPassword,
        DateTimeOffset now)
    {
        if (provider == EmailProvider.Google)
        {
            host = GoogleHost;
            port = GooglePort;
            security = GoogleSecurity;
        }

        var login = BlankToNull(userName);
        if (security == EmailSecurity.None && login is not null)
        {
            throw new DomainException(ErrorCodes.EmailSettingsPlainTextLogin, "A login needs an encrypted connection.");
        }

        Provider = provider;
        SenderName = BlankToNull(senderName);
        SenderAddress = senderAddress.Trim();
        Host = host.Trim();
        Port = port;
        Security = security;
        UserName = login;
        PasswordProtected = login is null ? null : protectedPassword ?? PasswordProtected;
        UpdatedAt = now;
    }

    /// <summary>SHA-256 over every value, the protected password included, so any change needs a new test.</summary>
    public string Fingerprint() => SettingsFingerprint.Compute(
        Provider.ToString(),
        SenderName ?? string.Empty,
        SenderAddress,
        Host,
        Port.ToString(CultureInfo.InvariantCulture),
        Security.ToString(),
        UserName ?? string.Empty,
        PasswordProtected ?? string.Empty);

    /// <summary>
    /// A passing test marks the current values as tested. A failure clears that mark only while the settings are not
    /// active: activation needs the latest test to pass, but a failed test must never stop working mail.
    /// </summary>
    public void RecordTest(bool succeeded, string? errorCode, DateTimeOffset now)
    {
        if (errorCode?.Length > LastTestErrorCodeMaxLength)
        {
            throw new ArgumentException("The error code is too long.", nameof(errorCode));
        }

        LastTestedAt = now;
        LastTestSucceeded = succeeded;
        LastTestErrorCode = succeeded ? null : errorCode;
        if (succeeded)
        {
            TestedFingerprint = Fingerprint();
        }
        else if (!IsActive)
        {
            TestedFingerprint = null;
        }
    }

    public void Activate(DateTimeOffset now)
    {
        if (!IsTested)
        {
            throw new DomainException(ErrorCodes.EmailSettingsNotTested, "The current mail settings have not passed a test send.");
        }

        IsActive = true;
        UpdatedAt = now;
    }

    /// <summary>
    /// Goes back to the environment settings and keeps every panel value for a later activation. When the latest test
    /// failed, the tested mark goes away, because activation needs the latest test to pass.
    /// </summary>
    public void UseEnvironment(DateTimeOffset now)
    {
        IsActive = false;
        if (LastTestSucceeded == false)
        {
            TestedFingerprint = null;
        }

        UpdatedAt = now;
    }

    private static string? BlankToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
