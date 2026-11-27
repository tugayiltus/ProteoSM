using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Application.Abstractions;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Users;

namespace Proteone.Api.IntegrationTests.Admin;

/// <summary>Outgoing mail settings in the admin panel: the password is write-only, activation needs a passing test.</summary>
[Collection(ApiCollection.Name)]
public sealed class EmailSettingsTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SettingsPath = "/api/v1/admin/email-settings";
    private const string TestPath = SettingsPath + "/test";
    private const string ActivatePath = SettingsPath + "/activate";
    private const string UseEnvironmentPath = SettingsPath + "/use-environment";
    private const string AdminEmail = "yonetici@example.com";
    private const string Password = "gizli-şifre-123";
    private const string Host = "smtp.sirket.local";
    private const string SenderAddress = "destek@sirket.com";
    private const string MailUser = "destek@sirket.com";
    private const int Port = 587;
    private const int TooLongHostLength = 254;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        factory.EmailTestSender.Reset();
    }

    public ValueTask DisposeAsync()
    {
        factory.EmailTestSender.Reset();
        return ValueTask.CompletedTask;
    }

    private Task<(ApiClient Client, User User)> AdminAsync(string email = AdminEmail) => ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, email);

    private static async Task<JsonElement> GetAsync(ApiClient client)
    {
        using var get = await client.GetAsync(SettingsPath);
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(get);
    }

    private static async Task<uint> VersionAsync(ApiClient client) => (await GetAsync(client)).GetProperty("version").GetUInt32();

    private static string Base64(string text) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(text));

    private static object Smtp(uint version, string? password = Password, int port = Port, string host = Host, string security = "start_tls", string? userName = MailUser, string senderAddress = SenderAddress) =>
        new { provider = "smtp", senderName = "Proteone Destek", senderAddress, host, port, security, userName, password, version };

    private static async Task<HttpResponseMessage> SaveAsync(ApiClient client, string? password = Password, int port = Port, string host = Host) =>
        await client.PutAsync(SettingsPath, Smtp(await VersionAsync(client), password, port, host));

    private static async Task<HttpResponseMessage> ActivateAsync(ApiClient client) =>
        await client.PostAsync(ActivatePath, new { version = await VersionAsync(client) });

    private async Task<List<(string? OldValues, string? NewValues)>> AuditsAsync(string action)
    {
        await using var db = factory.Database.CreateDbContext();
        var rows = await db.AuditEvents.Where(a => a.Action == action).Select(a => new { a.OldValues, a.NewValues }).ToListAsync(Ct);
        return rows.Select(r => (r.OldValues, r.NewValues)).ToList();
    }

    /// <summary>Saves, tests and activates the settings, so a later save of them runs the test first.</summary>
    private async Task ActivateWorkingSettingsAsync(ApiClient admin)
    {
        using var save = await SaveAsync(admin);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        using var test = await admin.PostAsync(TestPath);
        Assert.Equal(HttpStatusCode.OK, test.StatusCode);
        using var activate = await ActivateAsync(admin);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
    }

    [Fact]
    public async Task Password_is_write_only_and_never_in_responses_or_audit()
    {
        var (admin, _) = await AdminAsync();

        using var save = await SaveAsync(admin);
        var saveBody = await save.Content.ReadAsStringAsync(Ct);
        using var get = await admin.GetAsync(SettingsPath);
        var body = await get.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.DoesNotContain(Password, saveBody, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, body, StringComparison.Ordinal);
        Assert.True(JsonDocument.Parse(body).RootElement.GetProperty("hasPassword").GetBoolean());
        await using var db = factory.Database.CreateDbContext();
        var row = await db.EmailSettings.AsNoTracking().SingleAsync(Ct);
        Assert.NotNull(row.PasswordProtected);
        Assert.NotEqual(Password, row.PasswordProtected);
        var audits = await AuditsAsync(AuditActions.EmailSettingsUpdated);
        Assert.Single(audits);
        var auditText = string.Join('\n', audits.Select(a => a.OldValues + a.NewValues));
        Assert.DoesNotContain(Password, auditText, StringComparison.Ordinal);
        Assert.True(JsonDocument.Parse(audits[0].NewValues!).RootElement.GetProperty("passwordChanged").GetBoolean());
    }

    [Fact]
    public async Task Saving_without_a_password_keeps_the_stored_one()
    {
        var (admin, _) = await AdminAsync();
        using var first = await SaveAsync(admin);

        using var second = await SaveAsync(admin, password: null);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True((await admin.ReadJsonAsync<JsonElement>(second)).GetProperty("hasPassword").GetBoolean());
    }

    [Fact]
    public async Task Google_save_stores_the_preset()
    {
        var (admin, _) = await AdminAsync();

        using var save = await admin.PutAsync(SettingsPath, new
        {
            provider = "google",
            senderName = (string?)null,
            senderAddress = SenderAddress,
            host = "x",
            port = 25,
            security = "none",
            userName = MailUser,
            password = "abcd efgh ijkl mnop",
            version = await VersionAsync(admin),
        });
        var json = await GetAsync(admin);

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal("google", json.GetProperty("provider").GetString());
        Assert.Equal("smtp.gmail.com", json.GetProperty("host").GetString());
        Assert.Equal(587, json.GetProperty("port").GetInt32());
        Assert.Equal("start_tls", json.GetProperty("security").GetString());
    }

    [Fact]
    public async Task Invalid_values_are_field_errors()
    {
        var (admin, _) = await AdminAsync();

        using var portZero = await admin.PutAsync(SettingsPath, Smtp(await VersionAsync(admin), port: 0));
        using var portHigh = await admin.PutAsync(SettingsPath, Smtp(await VersionAsync(admin), port: 65536));
        using var longHost = await admin.PutAsync(SettingsPath, Smtp(await VersionAsync(admin), host: new string('a', TooLongHostLength)));
        using var badSender = await admin.PutAsync(SettingsPath, Smtp(await VersionAsync(admin), senderAddress: "no-reply"));
        using var googleNoUser = await admin.PutAsync(SettingsPath, new
        {
            provider = "google",
            senderName = (string?)null,
            senderAddress = SenderAddress,
            host = "",
            port = 0,
            security = "start_tls",
            userName = (string?)null,
            password = (string?)null,
            version = await VersionAsync(admin),
        });

        Assert.All(new[] { portZero, portHigh, longHost, badSender, googleNoUser }, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));
        Assert.Contains("port", (await ApiClient.ReadFieldCodesAsync(portZero)).Keys);
        Assert.Contains("port", (await ApiClient.ReadFieldCodesAsync(portHigh)).Keys);
        Assert.Contains("host", (await ApiClient.ReadFieldCodesAsync(longHost)).Keys);
        Assert.Contains("senderAddress", (await ApiClient.ReadFieldCodesAsync(badSender)).Keys);
        Assert.Contains("userName", (await ApiClient.ReadFieldCodesAsync(googleNoUser)).Keys);
    }

    [Fact]
    public async Task None_security_with_a_user_name_is_plain_text_login()
    {
        var (admin, _) = await AdminAsync();

        using var response = await admin.PutAsync(SettingsPath, Smtp(await VersionAsync(admin), port: 25, security: "none"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var fields = await ApiClient.ReadFieldCodesAsync(response);
        Assert.Contains(ErrorCodes.EmailSettingsPlainTextLogin, fields["userName"]);
    }

    [Fact]
    public async Task Test_sends_to_the_signed_in_admin_with_the_saved_settings()
    {
        var (admin, _) = await AdminAsync();
        using var save = await SaveAsync(admin);

        using var test = await admin.PostAsync(TestPath);

        Assert.Equal(HttpStatusCode.OK, test.StatusCode);
        Assert.True((await admin.ReadJsonAsync<JsonElement>(test)).GetProperty("ok").GetBoolean());
        var (transport, message) = Assert.Single(factory.EmailTestSender.Sent);
        Assert.Equal(AdminEmail, message.To);
        Assert.Equal(Password, transport.Password);
        Assert.Equal((Host, Port, MailUser), (transport.Host, transport.Port, transport.UserName));
        var json = await GetAsync(admin);
        Assert.True(json.GetProperty("isTested").GetBoolean());
        Assert.True(json.GetProperty("lastTestSucceeded").GetBoolean());
        var audit = Assert.Single(await AuditsAsync(AuditActions.EmailSettingsTested));
        Assert.True(JsonDocument.Parse(audit.NewValues!).RootElement.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Test_of_unconfigured_settings_is_not_configured()
    {
        var (admin, _) = await AdminAsync();

        using var test = await admin.PostAsync(TestPath);

        Assert.Equal(HttpStatusCode.OK, test.StatusCode);
        var json = await admin.ReadJsonAsync<JsonElement>(test);
        Assert.False(json.GetProperty("ok").GetBoolean());
        Assert.Equal(ErrorCodes.EmailSettingsNotConfigured, json.GetProperty("errorCode").GetString());
        Assert.Empty(factory.EmailTestSender.Sent);
    }

    [Fact]
    public async Task Failed_test_returns_code_and_redacted_detail_and_activation_is_409()
    {
        var (admin, _) = await AdminAsync();
        using var save = await SaveAsync(admin);
        factory.EmailTestSender.Result = new EmailTestResult(false, ErrorCodes.EmailSettingsAuthFailed, $"535 {Password} rejected");

        using var test = await admin.PostAsync(TestPath);
        var body = await test.Content.ReadAsStringAsync(Ct);
        using var activate = await ActivateAsync(admin);

        Assert.Equal(HttpStatusCode.OK, test.StatusCode);
        var json = JsonDocument.Parse(body).RootElement;
        Assert.False(json.GetProperty("ok").GetBoolean());
        Assert.Equal(ErrorCodes.EmailSettingsAuthFailed, json.GetProperty("errorCode").GetString());
        Assert.Contains("[redacted]", json.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(Password, body, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
        Assert.Equal(ErrorCodes.EmailSettingsNotTested, await ApiClient.ReadCodeAsync(activate));
        var audit = Assert.Single(await AuditsAsync(AuditActions.EmailSettingsTested));
        Assert.DoesNotContain(Password, audit.NewValues!, StringComparison.Ordinal);
        Assert.Equal(ErrorCodes.EmailSettingsAuthFailed, JsonDocument.Parse(audit.NewValues!).RootElement.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task A_change_after_a_passing_test_needs_a_new_test()
    {
        var (admin, _) = await AdminAsync();
        using var save = await SaveAsync(admin);
        using var test = await admin.PostAsync(TestPath);

        using var change = await SaveAsync(admin, port: 2525);
        using var activate = await ActivateAsync(admin);

        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.False((await admin.ReadJsonAsync<JsonElement>(change)).GetProperty("isTested").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, activate.StatusCode);
        Assert.Equal(ErrorCodes.EmailSettingsNotTested, await ApiClient.ReadCodeAsync(activate));
    }

    [Fact]
    public async Task Activate_then_use_environment_keeps_the_values()
    {
        var (admin, _) = await AdminAsync();
        await ActivateWorkingSettingsAsync(admin);
        Assert.True((await GetAsync(admin)).GetProperty("isActive").GetBoolean());

        using var restore = await admin.PostAsync(UseEnvironmentPath, new { version = await VersionAsync(admin) });

        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        var json = await GetAsync(admin);
        Assert.False(json.GetProperty("isActive").GetBoolean());
        Assert.Equal(Host, json.GetProperty("host").GetString());
        Assert.True(json.GetProperty("hasPassword").GetBoolean());
        Assert.Single(await AuditsAsync(AuditActions.EmailSettingsActivated));
        Assert.Single(await AuditsAsync(AuditActions.EmailSettingsEnvironmentRestored));
    }

    [Fact]
    public async Task Saving_active_settings_tests_them_first()
    {
        var (admin, _) = await AdminAsync();
        await ActivateWorkingSettingsAsync(admin);
        factory.EmailTestSender.Reset();

        using var save = await SaveAsync(admin, port: 2525);

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var (transport, _) = Assert.Single(factory.EmailTestSender.Sent);
        Assert.Equal(2525, transport.Port);
        var json = await admin.ReadJsonAsync<JsonElement>(save);
        Assert.True(json.GetProperty("isActive").GetBoolean());
        Assert.True(json.GetProperty("isTested").GetBoolean());
    }

    [Fact]
    public async Task Saving_active_settings_with_a_failing_test_keeps_the_old_ones()
    {
        const string wrongPassword = "yanlış-şifre";
        var (admin, _) = await AdminAsync();
        await ActivateWorkingSettingsAsync(admin);
        // Some relays echo the AUTH line, so the plain password and its AUTH LOGIN / AUTH PLAIN base64 forms all appear.
        var loginForm = Base64(wrongPassword);
        var plainForm = Base64($"\0{MailUser}\0{wrongPassword}");
        factory.EmailTestSender.Result = new EmailTestResult(false, ErrorCodes.EmailSettingsAuthFailed, $"535 {wrongPassword} {loginForm} {plainForm} rejected");

        using var save = await SaveAsync(admin, password: wrongPassword, host: "smtp.baska.local");
        var body = await save.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.Equal(ErrorCodes.EmailSettingsAuthFailed, problem.GetProperty("code").GetString());
        var detail = problem.GetProperty("testDetail").GetString();
        Assert.StartsWith("535 [redacted]", detail, StringComparison.Ordinal);
        Assert.Contains("rejected", detail, StringComparison.Ordinal);
        foreach (var secret in new[] { wrongPassword, loginForm, plainForm })
        {
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
        }

        var json = await GetAsync(admin);
        Assert.Equal(Host, json.GetProperty("host").GetString());
        Assert.True(json.GetProperty("isActive").GetBoolean());
        Assert.True(json.GetProperty("isTested").GetBoolean());
        // Only the first save is in the audit; the refused one left nothing behind.
        Assert.Single(await AuditsAsync(AuditActions.EmailSettingsUpdated));
    }

    [Fact]
    public async Task Test_racing_a_save_is_409_and_does_not_mark_tested()
    {
        var (admin, _) = await AdminAsync();
        var (other, _) = await AdminAsync("ikinci-yonetici@example.com");
        using var save = await SaveAsync(admin);
        factory.EmailTestSender.BeforeSend = async () =>
        {
            factory.EmailTestSender.BeforeSend = null;
            using var racing = await SaveAsync(other, port: 2525);
            Assert.Equal(HttpStatusCode.OK, racing.StatusCode);
        };

        using var test = await admin.PostAsync(TestPath);

        Assert.Equal(HttpStatusCode.Conflict, test.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(test));
        var json = await GetAsync(admin);
        Assert.Equal(2525, json.GetProperty("port").GetInt32());
        Assert.False(json.GetProperty("isTested").GetBoolean());
    }

    [Fact]
    public async Task Stale_version_is_409()
    {
        var (admin, _) = await AdminAsync();
        var version = await VersionAsync(admin);
        using var first = await admin.PutAsync(SettingsPath, Smtp(version));

        using var stale = await admin.PutAsync(SettingsPath, Smtp(version));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(stale));
    }

    [Fact]
    public async Task Stale_version_on_activate_and_use_environment_is_409()
    {
        var (admin, _) = await AdminAsync();
        await ActivateWorkingSettingsAsync(admin);
        var version = await VersionAsync(admin);
        using var restore = await admin.PostAsync(UseEnvironmentPath, new { version });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

        // The row is still tested, so only the version can refuse these.
        using var staleActivate = await admin.PostAsync(ActivatePath, new { version });
        using var staleRestore = await admin.PostAsync(UseEnvironmentPath, new { version });

        Assert.Equal(HttpStatusCode.Conflict, staleActivate.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(staleActivate));
        Assert.Equal(HttpStatusCode.Conflict, staleRestore.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(staleRestore));
        Assert.True((await GetAsync(admin)).GetProperty("isTested").GetBoolean());
    }

    [Fact]
    public async Task Stale_save_of_active_settings_is_409_before_any_test_send()
    {
        var (admin, _) = await AdminAsync();
        await ActivateWorkingSettingsAsync(admin);
        var version = await VersionAsync(admin);
        using var fresh = await admin.PutAsync(SettingsPath, Smtp(version));
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        factory.EmailTestSender.Reset();

        using var stale = await admin.PutAsync(SettingsPath, Smtp(version, port: 2525));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(stale));
        Assert.Empty(factory.EmailTestSender.Sent);
    }

    [Fact]
    public async Task Changing_the_server_without_a_new_password_is_refused()
    {
        var (admin, _) = await AdminAsync();
        using var save = await SaveAsync(admin);
        string? storedPassword;
        await using (var db = factory.Database.CreateDbContext())
        {
            storedPassword = (await db.EmailSettings.AsNoTracking().SingleAsync(Ct)).PasswordProtected;
        }

        using var response = await SaveAsync(admin, password: null, host: "smtp.baska.local");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.EmailSettingsPasswordMissing, (await ApiClient.ReadFieldCodesAsync(response))["password"]);
        await using var check = factory.Database.CreateDbContext();
        var row = await check.EmailSettings.AsNoTracking().SingleAsync(Ct);
        Assert.Equal((Host, storedPassword), (row.Host, row.PasswordProtected));
        Assert.Single(await AuditsAsync(AuditActions.EmailSettingsUpdated));
    }

    [Theory]
    [InlineData("port")]
    [InlineData("security")]
    [InlineData("userName")]
    [InlineData("provider")]
    public async Task Any_login_or_endpoint_change_without_a_new_password_is_refused(string field)
    {
        var (admin, _) = await AdminAsync();
        using var save = await SaveAsync(admin);
        var version = await VersionAsync(admin);
        object body = field switch
        {
            "port" => Smtp(version, password: null, port: 2525),
            "security" => Smtp(version, password: null, port: 465, security: "ssl_tls"),
            "userName" => Smtp(version, password: null, userName: "baska@sirket.com"),
            _ => new { provider = "google", senderName = (string?)null, senderAddress = SenderAddress, host = "", port = 0, security = "start_tls", userName = MailUser, password = (string?)null, version },
        };

        using var response = await admin.PutAsync(SettingsPath, body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(ErrorCodes.EmailSettingsPasswordMissing, (await ApiClient.ReadFieldCodesAsync(response))["password"]);
    }

    [Fact]
    public async Task Changing_the_server_with_a_new_password_is_saved()
    {
        var (admin, _) = await AdminAsync();
        using var save = await SaveAsync(admin);

        using var response = await SaveAsync(admin, password: "yeni-şifre-456", host: "smtp.baska.local");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("smtp.baska.local", (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("host").GetString());
    }

    [Fact]
    public async Task Changing_only_the_sender_name_keeps_the_password()
    {
        var (admin, _) = await AdminAsync();
        using var save = await SaveAsync(admin);
        await using var db = factory.Database.CreateDbContext();
        var storedPassword = (await db.EmailSettings.AsNoTracking().SingleAsync(Ct)).PasswordProtected;

        using var response = await admin.PutAsync(SettingsPath, new
        {
            provider = "smtp",
            senderName = "Başka Ad",
            senderAddress = SenderAddress,
            host = Host,
            port = Port,
            security = "start_tls",
            userName = MailUser,
            password = (string?)null,
            version = await VersionAsync(admin),
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("hasPassword").GetBoolean());
        db.ChangeTracker.Clear();
        Assert.Equal(storedPassword, (await db.EmailSettings.AsNoTracking().SingleAsync(Ct)).PasswordProtected);
    }

    [Theory]
    [InlineData(UserRole.Employee)]
    [InlineData(UserRole.ItStaff)]
    public async Task Only_admins_reach_email_settings(UserRole role)
    {
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, role, "kisi@example.com");

        using var get = await client.GetAsync(SettingsPath);
        using var test = await client.PostAsync(TestPath);
        // A non-admin cannot read the version, so any version will do: the policy refuses before the handler runs.
        using var save = await client.PutAsync(SettingsPath, Smtp(version: 0));
        using var activate = await client.PostAsync(ActivatePath, new { version = 0 });
        using var useEnvironment = await client.PostAsync(UseEnvironmentPath, new { version = 0 });

        Assert.All([get, test, save, activate, useEnvironment], response => Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode));
    }
}
