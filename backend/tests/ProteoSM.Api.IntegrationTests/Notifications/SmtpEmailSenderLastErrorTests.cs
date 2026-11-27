using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Notifications;
using ProteoSM.Infrastructure.Email;

namespace ProteoSM.Api.IntegrationTests.Notifications;

/// <summary>
/// The Worker stores the message of a failed send as the delivery's <c>last_error</c>. When the SMTP server echoes the
/// password in its reply (as plain text or as the base64 AUTH LOGIN / AUTH PLAIN form), that message shows
/// <c>[redacted]</c> instead, for the environment settings and for the panel settings.
/// </summary>
public sealed class SmtpEmailSenderLastErrorTests(ApiFactory factory) : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private const int TimeoutSeconds = 1;
    private const int AuthRejected = 535;
    private const string UserName = "mailer@firma.com";
    private const string Password = "hunter2-env";
    private const string PanelUser = "panel@firma.com";
    private const string PanelPassword = "hunter2-panel";
    private const string Redacted = "[redacted]";
    private static readonly EmailMessage Message = new("user@example.com", "Ayşe Yılmaz", "Konu", "Gövde");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static EmailOptions Environment(int port) => new()
    {
        Mode = EmailModes.Smtp,
        From = "ProteoSM <no-reply@proteosm.local>",
        Smtp = new SmtpOptions
        {
            Host = IPAddress.Loopback.ToString(),
            Port = port,
            UseStartTls = false,
            UserName = UserName,
            Password = Password,
            TimeoutSeconds = TimeoutSeconds,
        },
    };

    private static string PlainBlob(string userName, string password) => Convert.ToBase64String(Encoding.UTF8.GetBytes($"\0{userName}\0{password}"));

    private static string LoginBlob(string password) => Convert.ToBase64String(Encoding.UTF8.GetBytes(password));

    private async Task<string> LastErrorAsync(string echoed, int? panelPort = null)
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript(OfferAuth: true, AuthReply: AuthRejected, AuthReplyText: $"rejected {echoed}"));
        if (panelPort is not null)
        {
            await ActivatePanelWithLoginAsync(server.Port);
        }

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => SmtpSenders.Sender(factory, Environment(server.Port)).SendAsync(Message, Ct));
        Assert.Null(failure.InnerException);
        Assert.True(failure.Message.Length <= EmailDelivery.LastErrorMaxLength);
        return failure.Message;
    }

    // The domain refuses a login without encryption, and the scripted server speaks only plain text, so the row is
    // changed in SQL. The Worker uses an active row as it is.
    private async Task ActivatePanelWithLoginAsync(int port)
    {
        var secrets = factory.Services.GetRequiredService<IEmailSecretProtector>();
        await using var db = factory.Database.CreateDbContext();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE email_settings SET provider = 'smtp', sender_address = 'panel@firma.com', host = {IPAddress.Loopback.ToString()}, port = {port}, security = 'none', user_name = {PanelUser}, password_protected = {secrets.Protect(PanelPassword)}, is_active = true",
            Ct);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("login")]
    [InlineData("auth-plain")]
    public async Task Environment_password_echoed_by_the_server_is_redacted(string form)
    {
        var echoed = form switch
        {
            "login" => LoginBlob(Password),
            "auth-plain" => PlainBlob(UserName, Password),
            _ => Password,
        };

        var lastError = await LastErrorAsync(echoed);

        Assert.Contains(Redacted, lastError, StringComparison.Ordinal);
        Assert.DoesNotContain(echoed, lastError, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, lastError, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("plain")]
    [InlineData("login")]
    [InlineData("auth-plain")]
    public async Task Panel_password_echoed_by_the_server_is_redacted(string form)
    {
        var echoed = form switch
        {
            "login" => LoginBlob(PanelPassword),
            "auth-plain" => PlainBlob(PanelUser, PanelPassword),
            _ => PanelPassword,
        };

        var lastError = await LastErrorAsync(echoed, panelPort: 0);

        Assert.Contains(Redacted, lastError, StringComparison.Ordinal);
        Assert.DoesNotContain(echoed, lastError, StringComparison.Ordinal);
        Assert.DoesNotContain(PanelPassword, lastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Password_across_the_length_cut_is_redacted_before_the_cut()
    {
        // Learn where the server's reply starts in the error, then pad it so the password starts before the cut and
        // ends after it. Cut first, redact second would leave the first half of the password in the error.
        const string Marker = "marker";
        var offset = (await LastErrorAsync(Marker)).IndexOf(Marker, StringComparison.Ordinal);
        Assert.True(offset >= 0);
        var padding = new string('x', EmailDelivery.LastErrorMaxLength - offset - (Password.Length / 2));

        var lastError = await LastErrorAsync(padding + Password);

        Assert.Equal(EmailDelivery.LastErrorMaxLength, lastError.Length);
        Assert.DoesNotContain(Password[..(Password.Length / 2)], lastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_rejected_login_keeps_the_server_reply_in_the_error()
    {
        var lastError = await LastErrorAsync("go away");

        Assert.Contains("go away", lastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bad_environment_sender_address_fails_as_a_sanitized_send_error()
    {
        var options = Environment(port: 1);
        options.From = "not a mailbox <<";

        // The endpoint lookup parses Email:From; its failure must come out like any other send failure.
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => SmtpSenders.Sender(factory, options).SendAsync(Message, Ct));

        Assert.Null(failure.InnerException);
        Assert.False(string.IsNullOrWhiteSpace(failure.Message));
        Assert.True(failure.Message.Length <= EmailDelivery.LastErrorMaxLength);
    }
}
