using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Domain.Auth;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Users;

namespace ProteoSM.Api.IntegrationTests.Auth;

/// <summary>
/// LDAP sign-in end to end through the API against a real directory (OpenLDAP from deploy/test-directory, run by
/// tools/directory-contract-test.sh over LDAPS and StartTLS): the admin test and switch, sign-in by Windows user name and
/// by email, wrong passwords, the lockout and the local admin break-glass sign-in (ADR-0030). Skipped unless the
/// PROTEONE_TEST_LDAP_* variables (see <see cref="LdapDirectoryServer"/>) are set: CI has no directory.
/// </summary>
public sealed class LdapApiContractTests(LdapContractFixture fixture) : IClassFixture<LdapContractFixture>, IAsyncLifetime
{
    private const string SettingsPath = "/api/v1/admin/auth-settings";
    private const string LdapLoginPath = "/api/v1/auth/login/ldap";
    private const string MethodPath = "/api/v1/auth/method";
    private const string LdapMethod = "ldap";
    private const string AdminEmail = "yonetici@proteone-test.example";
    private const string WrongPassword = "yanlis-sifre";
    private const string NoDirectory = "No test directory configured (tools/directory-contract-test.sh).";
    private const int LockThreshold = 5;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        if (fixture.Factory is { } factory)
        {
            await factory.ResetAsync();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task The_admin_test_passes_and_ldap_can_be_switched_on()
    {
        var (factory, server) = Directory();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, AdminEmail);
        await SaveAsync(admin, server);

        using var test = await admin.PostAsync($"{SettingsPath}/test", new { method = LdapMethod });
        var result = await admin.ReadJsonAsync<JsonElement>(test);
        using var switched = await SwitchAsync(admin);
        using var method = await ApiClient.Create(factory).GetAsync(MethodPath);

        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        Assert.Equal(LdapMethod, (await admin.ReadJsonAsync<JsonElement>(method)).GetProperty("method").GetString());
    }

    [Fact]
    public async Task The_windows_user_name_and_the_email_sign_in_the_same_person()
    {
        var (factory, server) = Directory();
        await ActivateAsync(factory, server);
        var byNameClient = ApiClient.Create(factory);

        using var byName = await LoginAsync(byNameClient, server.User, server.Password);
        using var byEmail = await LoginAsync(ApiClient.Create(factory), server.Email, server.Password);
        using var me = await byNameClient.GetAsync(ApiClient.MePath);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK), (byName.StatusCode, byEmail.StatusCode, me.StatusCode));
        await using var db = factory.Database.CreateDbContext();
        var normalized = User.NormalizeEmail(server.Email);
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == normalized, Ct);
        Assert.NotNull(user.LdapObjectGuid);
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        var (factory, server) = Directory();
        await ActivateAsync(factory, server);
        var client = ApiClient.Create(factory);

        using var wrong = await LoginAsync(client, server.User, WrongPassword);
        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal((HttpStatusCode.Unauthorized, HttpStatusCode.Unauthorized), (wrong.StatusCode, me.StatusCode));
        Assert.Equal(ErrorCodes.InvalidCredentials, await ApiClient.ReadCodeAsync(wrong));
        Assert.Equal(ErrorCodes.Unauthenticated, await ApiClient.ReadCodeAsync(me));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_name()
    {
        var (factory, server) = Directory();
        await ActivateAsync(factory, server);
        var client = ApiClient.Create(factory);
        for (var i = 0; i < LockThreshold; i++)
        {
            using var wrong = await LoginAsync(client, server.User, WrongPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
            Assert.Equal(ErrorCodes.InvalidCredentials, await ApiClient.ReadCodeAsync(wrong));
        }

        using var right = await LoginAsync(client, server.User, server.Password);

        // The lock answers exactly like a wrong password (no enumeration), not with a rate limit or a directory error.
        Assert.Equal(HttpStatusCode.Unauthorized, right.StatusCode);
        Assert.Equal(ErrorCodes.InvalidCredentials, await ApiClient.ReadCodeAsync(right));
    }

    [Fact]
    public async Task The_local_admin_still_signs_in_with_a_password()
    {
        var (factory, server) = Directory();
        await ActivateAsync(factory, server);
        var client = ApiClient.Create(factory);

        await client.LoginAsync(AdminEmail);
        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    private (LdapContractFactory Factory, LdapDirectoryServer Server) Directory()
    {
        Assert.SkipWhen(fixture.Factory is null, NoDirectory);
        return (fixture.Factory!, fixture.Server!);
    }

    /// <summary>What an admin does: save the settings, run the test, switch the method on; the admin keeps a local password.</summary>
    private static async Task ActivateAsync(LdapContractFactory factory, LdapDirectoryServer server)
    {
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, AdminEmail);
        await SaveAsync(admin, server);
        using var test = await admin.PostAsync($"{SettingsPath}/test", new { method = LdapMethod });
        Assert.True((await admin.ReadJsonAsync<JsonElement>(test)).GetProperty("ok").GetBoolean());
        using var switched = await SwitchAsync(admin);
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
    }

    private static async Task SaveAsync(ApiClient admin, LdapDirectoryServer server)
    {
        using var put = await admin.PutAsync($"{SettingsPath}/ldap", new
        {
            host = server.Host,
            port = server.Port,
            security = server.Security,
            baseDn = server.BaseDn,
            bindDn = server.BindDn,
            bindPassword = server.BindPassword,
            userFilter = (string?)null,
            version = await VersionAsync(admin),
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
    }

    private static async Task<HttpResponseMessage> SwitchAsync(ApiClient admin) =>
        await admin.PostAsync($"{SettingsPath}/method", new { method = LdapMethod, version = await VersionAsync(admin) });

    private static async Task<uint> VersionAsync(ApiClient admin)
    {
        using var get = await admin.GetAsync(SettingsPath);
        return (await admin.ReadJsonAsync<JsonElement>(get)).GetProperty("version").GetUInt32();
    }

    private static Task<HttpResponseMessage> LoginAsync(ApiClient client, string identifier, string password) =>
        client.PostAsync(LdapLoginPath, new { identifier, password });
}
