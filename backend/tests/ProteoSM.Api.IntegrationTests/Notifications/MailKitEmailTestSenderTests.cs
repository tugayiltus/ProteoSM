using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Email;
using ProteoSM.Infrastructure.Email;

namespace ProteoSM.Api.IntegrationTests.Notifications;

/// <summary>
/// The admin panel's test send names the step that failed (connect, TLS, authentication, sender, recipient, timeout)
/// and never shows a credential. Every case runs against a scripted SMTP server on the loopback address.
/// </summary>
public sealed class MailKitEmailTestSenderTests : IDisposable
{
    private const int TimeoutSeconds = 1;
    private const int Rejected = 550;
    private const int AuthRejected = 535;
    private const string SenderAddress = "no-reply@firma.com";
    private const string Recipient = "admin@firma.com";
    private const string UserName = "mailer@firma.com";
    private const string Password = "hunter2";
    private static readonly TimeSpan UpperBound = TimeSpan.FromSeconds(10);
    private static readonly EmailMessage Message = new(Recipient, "Admin", "Proteone test e-postası", "Gövde");

    private readonly string _pickupFolder = Path.Combine(Path.GetTempPath(), "proteone-test-send-" + Guid.NewGuid().ToString("N"));

    private sealed class FixedOptionsMonitor(EmailOptions value) : IOptionsMonitor<EmailOptions>
    {
        public EmailOptions CurrentValue => value;

        public EmailOptions Get(string? name) => value;

        public IDisposable? OnChange(Action<EmailOptions, string?> listener) => null;
    }

    public void Dispose()
    {
        if (Directory.Exists(_pickupFolder))
        {
            Directory.Delete(_pickupFolder, recursive: true);
        }
    }

    private static MailKitEmailTestSender SmtpSender() => new(new FixedOptionsMonitor(new EmailOptions
    {
        Mode = EmailModes.Smtp,
        From = "Env <env@proteone.local>",
        Smtp = new SmtpOptions { TimeoutSeconds = TimeoutSeconds },
    }));

    private static SmtpTransport Transport(int port, EmailSecurity security = EmailSecurity.None, string? userName = null, string? password = null) =>
        new("Firma", SenderAddress, IPAddress.Loopback.ToString(), port, security, userName, password);

    private static Task<EmailTestResult> SendAsync(SmtpTransport transport) =>
        SmtpSender().SendTestAsync(transport, Message, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Happy_server_gets_the_panel_sender_and_recipient()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript());

        var result = await SendAsync(Transport(server.Port));

        Assert.True(result.Ok, result.Detail);
        Assert.Null(result.ErrorCode);
        Assert.Contains(server.Commands, command => command.StartsWith($"MAIL FROM:<{SenderAddress}>", StringComparison.Ordinal));
        Assert.Contains(server.Commands, command => command.StartsWith($"RCPT TO:<{Recipient}>", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:169.254.169.254")]
    public async Task Link_local_and_unspecified_targets_are_not_allowed(string host)
    {
        var result = await SendAsync(Transport(port: 25) with { Host = host });

        Assert.False(result.Ok);
        Assert.Equal(ErrorCodes.EmailSettingsHostNotAllowed, result.ErrorCode);
    }

    [Fact]
    public async Task Loopback_host_name_is_allowed()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript());

        var result = await SendAsync(Transport(server.Port) with { Host = "localhost" });

        Assert.True(result.Ok, result.Detail);
    }

    [Fact]
    public async Task Closed_port_is_connect_failed()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        var result = await SendAsync(Transport(port));

        Assert.False(result.Ok);
        Assert.Equal(ErrorCodes.EmailSettingsConnectFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Ssl_tls_against_a_plain_server_is_tls_failed()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript());

        var result = await SendAsync(Transport(server.Port, EmailSecurity.SslTls));

        Assert.Equal(ErrorCodes.EmailSettingsTlsFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Start_tls_not_offered_is_tls_failed()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript());

        var result = await SendAsync(Transport(server.Port, EmailSecurity.StartTls));

        Assert.Equal(ErrorCodes.EmailSettingsTlsFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Auth_535_is_auth_failed()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript(OfferAuth: true, AuthReply: AuthRejected));

        var result = await SendAsync(Transport(server.Port, userName: UserName, password: Password));

        Assert.Equal(ErrorCodes.EmailSettingsAuthFailed, result.ErrorCode);
    }

    [Fact]
    public async Task No_auth_offered_with_a_user_name_is_auth_failed()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript());

        var result = await SendAsync(Transport(server.Port, userName: UserName, password: Password));

        Assert.Equal(ErrorCodes.EmailSettingsAuthFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Mail_from_550_is_sender_rejected()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript(MailFromReply: Rejected));

        var result = await SendAsync(Transport(server.Port));

        Assert.Equal(ErrorCodes.EmailSettingsSenderRejected, result.ErrorCode);
    }

    [Fact]
    public async Task Rcpt_550_is_recipient_rejected()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript(RcptReply: Rejected));

        var result = await SendAsync(Transport(server.Port));

        Assert.Equal(ErrorCodes.EmailSettingsRecipientRejected, result.ErrorCode);
    }

    [Fact]
    public async Task Silent_server_is_timeout_within_the_configured_seconds()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript(Silent: true));

        var stopwatch = Stopwatch.StartNew();
        var result = await SendAsync(Transport(server.Port));

        Assert.Equal(ErrorCodes.EmailSettingsTimeout, result.ErrorCode);
        Assert.True(stopwatch.Elapsed < UpperBound, $"Test send took {stopwatch.Elapsed} with a {TimeoutSeconds}s timeout.");
    }

    [Fact]
    public async Task Detail_redacts_credential_shaped_text()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript(OfferAuth: true, AuthReply: AuthRejected, AuthReplyText: $"password={Password}"));

        // A different password than the echoed one: the credential-shaped pattern alone must redact it.
        var result = await SendAsync(Transport(server.Port, userName: UserName, password: "other-secret"));

        Assert.Equal(ErrorCodes.EmailSettingsAuthFailed, result.ErrorCode);
        Assert.NotNull(result.Detail);
        Assert.Contains("[redacted]", result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, result.Detail, StringComparison.Ordinal);
        Assert.True(result.Detail.Length <= EmailSettings.TestDetailMaxLength);
    }

    [Fact]
    public async Task Detail_redacts_the_password_value_echoed_by_the_server()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript(OfferAuth: true, AuthReply: AuthRejected, AuthReplyText: $"rejected {Password}"));

        var result = await SendAsync(Transport(server.Port, userName: UserName, password: Password));

        Assert.NotNull(result.Detail);
        Assert.Contains("[redacted]", result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Detail_redacts_the_auth_plain_blob_echoed_by_the_server()
    {
        var plainBlob = Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{UserName}\0{Password}"));
        await using var server = ScriptedSmtpServer.Start(new SmtpScript(OfferAuth: true, AuthReply: AuthRejected, AuthReplyText: $"rejected AUTH PLAIN {plainBlob}"));

        var result = await SendAsync(Transport(server.Port, userName: UserName, password: Password));

        AssertRedacted(result, plainBlob);
    }

    [Fact]
    public async Task Detail_redacts_the_auth_login_password_echoed_by_the_server()
    {
        var loginBlob = Convert.ToBase64String(Encoding.UTF8.GetBytes(Password));
        await using var server = ScriptedSmtpServer.Start(new SmtpScript(OfferAuth: true, AuthReply: AuthRejected, AuthReplyText: $"rejected {loginBlob}"));

        var result = await SendAsync(Transport(server.Port, userName: UserName, password: Password));

        AssertRedacted(result, loginBlob);
    }

    private static void AssertRedacted(EmailTestResult result, string blob)
    {
        Assert.Equal(ErrorCodes.EmailSettingsAuthFailed, result.ErrorCode);
        Assert.NotNull(result.Detail);
        Assert.Contains("[redacted]", result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(blob, result.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, result.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pickup_mode_writes_an_eml_with_the_panel_sender()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript());
        var sender = new MailKitEmailTestSender(new FixedOptionsMonitor(new EmailOptions
        {
            Mode = EmailModes.Pickup,
            From = "Env <env@proteone.local>",
            PickupFolder = _pickupFolder,
        }));

        var result = await sender.SendTestAsync(Transport(server.Port), Message, TestContext.Current.CancellationToken);

        Assert.True(result.Ok, result.Detail);
        var file = Assert.Single(Directory.GetFiles(_pickupFolder, "*.eml"));
        var content = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        Assert.Contains(SenderAddress, content.Split('\n').Single(line => line.StartsWith("From:", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.Equal(0, server.ConnectionCount);
    }

    [Fact]
    public void Smtp_transport_to_string_omits_the_password()
    {
        var transport = Transport(EmailSettings.StartTlsPort, EmailSecurity.StartTls, UserName, Password);

        Assert.Equal($"{IPAddress.Loopback}:{EmailSettings.StartTlsPort}", transport.ToString());
        Assert.DoesNotContain(Password, transport.ToString(), StringComparison.Ordinal);
    }
}
