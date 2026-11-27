using System.Net;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Email;
using ProteoSM.Domain.Users;
using ProteoSM.Infrastructure;
using ProteoSM.Infrastructure.Email;

namespace ProteoSM.Api.IntegrationTests.Notifications;

/// <summary>
/// The Worker sends with the panel settings while they are active, else with <c>Email:From</c> + <c>Email:Smtp:*</c>
/// exactly as before. The active row is cached for <see cref="ActiveSmtpEndpointProvider.CacheLifetime"/>; Pickup mode
/// never looks at it.
/// </summary>
public sealed class ActiveSmtpEndpointProviderTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const string EnvironmentHost = "env.example";
    private const int EnvironmentPort = 2525;
    private const string EnvironmentFrom = "Env <env@proteone.local>";
    private const string PanelHost = "smtp.sirket.local";
    private const string PanelAddress = "destek@sirket.com";
    private const string PanelName = "ProteoSM Destek";
    private const string PanelUser = "destek@sirket.com";
    private const string PanelPassword = "panel-şifre-123";
    private const string BrokenProtectedPassword = "broken";
    private const string SettingsPath = "/api/v1/admin/email-settings";
    private const int TimeoutSeconds = 1;
    private static readonly EmailMessage Message = new("user@example.com", "Ayşe Yılmaz", "Konu", "Gövde");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private IEmailSecretProtector Secrets => factory.Services.GetRequiredService<IEmailSecretProtector>();

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static EmailOptions Environment(string mode = EmailModes.Smtp) => new()
    {
        Mode = mode,
        From = EnvironmentFrom,
        Smtp = new SmtpOptions { Host = EnvironmentHost, Port = EnvironmentPort, UseStartTls = false, TimeoutSeconds = TimeoutSeconds },
    };

    private ActiveSmtpEndpointProvider Provider(IEmailSecretProtector? secrets = null) =>
        SmtpSenders.Endpoints(factory, Environment(), factory.Clock, secrets);

    private async Task ActivatePanelAsync(
        string host = PanelHost,
        int port = EmailSettings.StartTlsPort,
        EmailSecurity security = EmailSecurity.StartTls,
        string? userName = PanelUser,
        string? password = PanelPassword)
    {
        await using var db = factory.Database.CreateDbContext();
        var settings = await db.EmailSettings.SingleAsync(Ct);
        var now = factory.Clock.GetUtcNow();
        settings.Update(EmailProvider.Smtp, PanelName, PanelAddress, host, port, security, userName, password is null ? null : Secrets.Protect(password), now);
        settings.RecordTest(true, null, now);
        settings.Activate(now);
        await db.SaveChangesAsync(Ct);
    }

    private async Task ChangeAsync(Action<EmailSettings, DateTimeOffset> change)
    {
        await using var db = factory.Database.CreateDbContext();
        var settings = await db.EmailSettings.SingleAsync(Ct);
        change(settings, factory.Clock.GetUtcNow());
        await db.SaveChangesAsync(Ct);
    }

    private async Task SetProtectedPasswordAsync(string protectedPassword)
    {
        await using var db = factory.Database.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"UPDATE email_settings SET password_protected = {protectedPassword}", Ct);
    }

    [Fact]
    public async Task No_active_row_uses_the_environment_values()
    {
        var endpoint = await Provider().GetAsync(Ct);

        Assert.Equal(EnvironmentHost, endpoint.Host);
        Assert.Equal(EnvironmentPort, endpoint.Port);
        Assert.Equal(SecureSocketOptions.Auto, endpoint.Socket);
        Assert.Equal("env@proteone.local", endpoint.From.Address);
        Assert.False(endpoint.CheckTarget);
    }

    [Fact]
    public async Task Activated_settings_are_used_after_the_cache_lifetime()
    {
        var provider = Provider();
        Assert.Equal(EnvironmentHost, (await provider.GetAsync(Ct)).Host);

        await ActivatePanelAsync();
        Assert.Equal(EnvironmentHost, (await provider.GetAsync(Ct)).Host);

        factory.Clock.Advance(ActiveSmtpEndpointProvider.CacheLifetime);
        var endpoint = await provider.GetAsync(Ct);

        Assert.Equal(PanelHost, endpoint.Host);
        Assert.Equal(EmailSettings.StartTlsPort, endpoint.Port);
        Assert.Equal(SecureSocketOptions.StartTls, endpoint.Socket);
        Assert.Equal(PanelAddress, endpoint.From.Address);
        Assert.Equal(PanelName, endpoint.From.Name);
        Assert.Equal(PanelUser, endpoint.UserName);
        Assert.Equal(PanelPassword, endpoint.Password);
        Assert.True(endpoint.CheckTarget);
    }

    [Fact]
    public async Task Use_environment_switches_back_after_the_cache_lifetime()
    {
        await ActivatePanelAsync();
        var provider = Provider();
        Assert.Equal(PanelHost, (await provider.GetAsync(Ct)).Host);

        await ChangeAsync((settings, now) => settings.UseEnvironment(now));
        Assert.Equal(PanelHost, (await provider.GetAsync(Ct)).Host);

        factory.Clock.Advance(ActiveSmtpEndpointProvider.CacheLifetime);
        Assert.Equal(EnvironmentHost, (await provider.GetAsync(Ct)).Host);
    }

    [Fact]
    public async Task Failed_test_on_active_settings_keeps_the_panel_sender()
    {
        await ActivatePanelAsync();
        var provider = Provider();
        Assert.Equal(PanelHost, (await provider.GetAsync(Ct)).Host);

        await ChangeAsync((settings, now) => settings.RecordTest(false, "email_settings.auth_failed", now));
        factory.Clock.Advance(ActiveSmtpEndpointProvider.CacheLifetime);

        Assert.Equal(PanelHost, (await provider.GetAsync(Ct)).Host);
    }

    [Fact]
    public async Task Unchanged_version_reuses_the_cached_endpoint_without_unprotecting()
    {
        await ActivatePanelAsync();
        var counting = new CountingSecretProtector(Secrets);
        var provider = Provider(counting);

        await provider.GetAsync(Ct);
        factory.Clock.Advance(ActiveSmtpEndpointProvider.CacheLifetime);
        await provider.GetAsync(Ct);
        factory.Clock.Advance(ActiveSmtpEndpointProvider.CacheLifetime);
        var endpoint = await provider.GetAsync(Ct);

        Assert.Equal(PanelPassword, endpoint.Password);
        Assert.Equal(1, counting.UnprotectCount);
    }

    [Fact]
    public async Task Unreadable_password_fails_each_send_without_caching()
    {
        await ActivatePanelAsync();
        var provider = Provider();
        await SetProtectedPasswordAsync(BrokenProtectedPassword);

        var first = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(Ct).AsTask());
        var second = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(Ct).AsTask());

        Assert.Equal("The stored SMTP password of the panel settings cannot be read.", first.Message);
        Assert.DoesNotContain(BrokenProtectedPassword, first.ToString(), StringComparison.Ordinal);
        Assert.Null(first.InnerException);
        Assert.Equal(first.Message, second.Message);

        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "yonetici@example.com");
        using var get = await admin.GetAsync(SettingsPath);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        await SetProtectedPasswordAsync(Secrets.Protect(PanelPassword));
        factory.Clock.Advance(ActiveSmtpEndpointProvider.CacheLifetime);

        Assert.Equal(PanelPassword, (await provider.GetAsync(Ct)).Password);
    }

    [Fact]
    public async Task Each_call_builds_a_new_panel_sender_address()
    {
        // MimeKit subscribes every message to its sender address and never unsubscribes: a shared, cached address
        // would keep each sent message in memory.
        await ActivatePanelAsync();
        var provider = Provider();

        var first = await provider.GetAsync(Ct);
        var second = await provider.GetAsync(Ct);

        Assert.NotSame(first, second);
        Assert.NotSame(first.From, second.From);
        Assert.Equal(first.From.Address, second.From.Address);
    }

    [Fact]
    public async Task Unreadable_password_after_a_good_one_never_reuses_the_cached_password()
    {
        await ActivatePanelAsync();
        var provider = Provider();
        Assert.Equal(PanelPassword, (await provider.GetAsync(Ct)).Password);

        await SetProtectedPasswordAsync(BrokenProtectedPassword);
        factory.Clock.Advance(ActiveSmtpEndpointProvider.CacheLifetime);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(Ct).AsTask());
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(Ct).AsTask());
        factory.Clock.Advance(ActiveSmtpEndpointProvider.CacheLifetime);
        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetAsync(Ct).AsTask());
    }

    [Fact]
    public async Task Smtp_sender_uses_the_panel_sender_address()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript());
        await ActivatePanelAsync(IPAddress.Loopback.ToString(), server.Port, EmailSecurity.None, userName: null, password: null);

        await SmtpSenders.Sender(factory, Environment(), factory.Clock).SendAsync(Message, Ct);

        Assert.Contains(server.Commands, command => command.StartsWith($"MAIL FROM:<{PanelAddress}>", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Smtp_sender_refuses_a_panel_host_on_a_refused_address()
    {
        await ActivatePanelAsync("169.254.169.254", EmailSettings.PlainPort, EmailSecurity.None, userName: null, password: null);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => SmtpSenders.Sender(factory, Environment(), factory.Clock).SendAsync(Message, Ct));

        Assert.Contains("not allowed", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pickup_mode_ignores_active_panel_settings()
    {
        await using var server = ScriptedSmtpServer.Start(new SmtpScript());
        await ActivatePanelAsync(IPAddress.Loopback.ToString(), server.Port, EmailSecurity.None, userName: null, password: null);
        await using var scope = factory.Services.CreateAsyncScope();

        var sender = DependencyInjection.CreateEmailSender(scope.ServiceProvider);
        await sender.SendAsync(Message, Ct);

        Assert.IsType<PickupFolderEmailSender>(sender);
        var eml = Assert.Single(Directory.GetFiles(factory.EmailPickupFolder, "*.eml"));
        var text = await File.ReadAllTextAsync(eml, Ct);
        Assert.Contains("no-reply@proteosm.local", text, StringComparison.Ordinal);
        Assert.DoesNotContain(PanelAddress, text, StringComparison.Ordinal);
        Assert.Equal(0, server.ConnectionCount);
    }

    private sealed class CountingSecretProtector(IEmailSecretProtector inner) : IEmailSecretProtector
    {
        public int UnprotectCount { get; private set; }

        public string Protect(string secret) => inner.Protect(secret);

        public string Unprotect(string protectedSecret)
        {
            UnprotectCount++;
            return inner.Unprotect(protectedSecret);
        }
    }
}
