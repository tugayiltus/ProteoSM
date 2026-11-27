using System.Net;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using ProteoSM.Api.Auth;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Features.Auth.Directory;
using ProteoSM.Domain.Auth;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Users;
using ProteoSM.Infrastructure.Identity;

namespace ProteoSM.Api.IntegrationTests.Auth;

/// <summary>
/// Microsoft Entra sign-in end to end against a local OpenID Connect server (mock-oauth2-server,
/// deploy/test-directory/compose.yaml): the real handler runs the code flow with PKCE, redeems the code over the network
/// and validates the signed token; refusals must end at the login page (ADR-0030). Skipped unless PROTEOSM_TEST_OIDC_URL
/// and PROTEOSM_TEST_OIDC_TENANT are set (tools/directory-contract-test.sh): CI has no such server.
/// </summary>
public sealed partial class OidcContractTests(OidcContractFixture fixture) : IClassFixture<OidcContractFixture>, IAsyncLifetime
{
    private const string StartPath = "/api/v1/auth/oidc/start";
    private const string TestSettingsPath = "/api/v1/admin/auth-settings/test";
    private const string AfterSignIn = "/";
    private const string PkceChallengeParameter = "code_challenge=";
    private const string PkceMethodParameter = "code_challenge_method=S256";
    private const string EmailProperty = "email";
    private const string OkProperty = "ok";
    private const string NoServer = "No local OIDC server configured (tools/directory-contract-test.sh).";

    // Client ids and identities shaped by the oidc service's JSON_CONFIG in deploy/test-directory/compose.yaml.
    private const string MemberClient = "proteone-member";
    private const string OtherTenantClient = "proteone-other-tenant";
    private const string ForeignDomainClient = "proteone-foreign-domain";
    private const string MemberEmail = "deniz.test@proteone-test.example";
    private const string ForeignDomainEmail = "deniz.test@baska-alan.example";
    private const string AllowedDomain = "proteone-test.example";
    private const string ForeignDomain = "baska-alan.example";
    private const string ClientSecret = "test-client-secret";
    private const string AdminEmail = "yonetici@proteone-test.example";

    /// <summary>Nothing listens on port 1 (tcpmux) on a developer machine: the connection is refused at once.</summary>
    private static readonly Uri WrongAuthority = new("http://127.0.0.1:1/");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        if (fixture.Factory is { } factory)
        {
            await factory.ResetAsync();
            factory.Configuration.Tamper = OidcTamper.None;
            factory.Outcome.Reset();
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_member_signs_in_with_the_code_flow_and_pkce_and_holds_a_proteone_session()
    {
        var factory = Factory();
        await UseEntraAsync(factory, MemberClient);
        var client = ApiClient.Create(factory);

        using var callback = await SignInAsync(client);
        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal((HttpStatusCode.Redirect, AfterSignIn), (callback.StatusCode, callback.Headers.Location?.OriginalString));
        Assert.Null(factory.Outcome.Failure);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(MemberEmail, (await client.ReadJsonAsync<JsonElement>(me)).GetProperty(EmailProperty).GetString());
    }

    [Fact]
    public async Task A_token_of_another_tenant_is_refused_for_its_tenant()
    {
        var factory = Factory();
        await UseEntraAsync(factory, OtherTenantClient);

        await AssertRefusedAsync(factory);

        var token = AssertValidTokenRefusedByProteone(factory);
        var tenant = token.FindFirst(EntraIdentityMapper.TenantClaim)?.Value;
        Assert.NotNull(tenant);
        Assert.NotEqual(factory.OidcServer.Tenant, tenant, StringComparer.OrdinalIgnoreCase);
        // The tenant is the only fault: with its own tenant configured, the same token would be accepted.
        Assert.Null(EntraIdentityMapper.Map(token, factory.OidcServer.Tenant, [AllowedDomain]));
        Assert.NotNull(EntraIdentityMapper.Map(token, tenant, [AllowedDomain]));
        Assert.False(await UserExistsAsync(factory, MemberEmail));
    }

    [Fact]
    public async Task A_sign_in_name_outside_the_allowed_domains_is_refused_for_its_domain()
    {
        var factory = Factory();
        await UseEntraAsync(factory, ForeignDomainClient);

        await AssertRefusedAsync(factory);

        var token = AssertValidTokenRefusedByProteone(factory);
        Assert.Equal(factory.OidcServer.Tenant, token.FindFirst(EntraIdentityMapper.TenantClaim)?.Value, StringComparer.OrdinalIgnoreCase);
        // The domain is the only fault: with that domain allowed, the same token would be accepted.
        Assert.Null(EntraIdentityMapper.Map(token, factory.OidcServer.Tenant, [AllowedDomain]));
        Assert.NotNull(EntraIdentityMapper.Map(token, factory.OidcServer.Tenant, [ForeignDomain]));
        Assert.False(await UserExistsAsync(factory, ForeignDomainEmail));
    }

    [Fact]
    public async Task A_token_with_a_bad_signature_is_refused_for_its_signature()
    {
        var factory = Factory();
        await UseEntraAsync(factory, MemberClient);
        factory.Configuration.Tamper = OidcTamper.ForeignSigningKey;

        await AssertRefusedAsync(factory);

        // SecurityTokenSignatureKeyNotFoundException derives from this one, so both signature outcomes count.
        AssertTokenRejectedFor<SecurityTokenInvalidSignatureException>(factory);
        Assert.False(await UserExistsAsync(factory, MemberEmail));
    }

    [Fact]
    public async Task A_token_from_another_issuer_is_refused_for_its_issuer()
    {
        var factory = Factory();
        await UseEntraAsync(factory, MemberClient);
        factory.Configuration.Tamper = OidcTamper.ForeignIssuer;

        await AssertRefusedAsync(factory);

        AssertTokenRejectedFor<SecurityTokenInvalidIssuerException>(factory);
        Assert.False(await UserExistsAsync(factory, MemberEmail));
    }

    [Fact]
    public async Task The_connection_test_passes_against_the_server()
    {
        var factory = Factory();
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, AdminEmail);
        await UseEntraAsync(factory, MemberClient, activate: false);

        using var response = await admin.PostAsync(TestSettingsPath, new { method = LoginMethod.Entra });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await admin.ReadJsonAsync<JsonElement>(response);
        Assert.True(body.GetProperty(OkProperty).GetBoolean(), body.ToString());
    }

    [Fact]
    public async Task The_connection_test_fails_cleanly_against_a_wrong_authority()
    {
        var server = Factory().OidcServer;
        using var http = new HttpClient(new MicrosoftAuthorityRewrite(WrongAuthority));

        var result = await new EntraConnectionTester(http).TestAsync(server.Tenant, MemberClient, ClientSecret, Ct);

        Assert.Equal((false, (string?)ErrorCodes.AuthDirectoryUnavailable), result);
    }

    private OidcContractFactory Factory()
    {
        Assert.SkipWhen(fixture.Factory is null, NoServer);
        return fixture.Factory!;
    }

    /// <summary>The login settings row as an admin would leave it: Entra configured for the local tenant and this client.</summary>
    private static async Task UseEntraAsync(OidcContractFactory factory, string clientId, bool activate = true)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var secrets = scope.ServiceProvider.GetRequiredService<IAuthSecretProtector>();
        await using var db = factory.Database.CreateDbContext();
        var settings = await db.AuthSettings.SingleAsync(Ct);
        var now = DateTimeOffset.UtcNow;
        settings.UpdateEntra(factory.OidcServer.Tenant, clientId, secrets.Protect(ClientSecret), [AllowedDomain], now);
        if (activate)
        {
            settings.MarkTested(LoginMethod.Entra);
            settings.SwitchTo(LoginMethod.Entra, now);
        }

        await db.SaveChangesAsync(Ct);
        // A direct write must drop the cached handler options, like the settings handler does.
        scope.ServiceProvider.GetRequiredService<ILoginProviderCache>().Invalidate();
    }

    /// <summary>
    /// The browser's part of the code flow: the start redirect (with a PKCE challenge), the server's authorize answer,
    /// then the callback on the API. The handler asks for form_post; a plain redirect answer is followed as well.
    /// </summary>
    private static async Task<HttpResponseMessage> SignInAsync(ApiClient client)
    {
        using var start = await client.GetAsync(StartPath);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        var authorize = start.Headers.Location!;
        Assert.Contains(PkceChallengeParameter, authorize.Query, StringComparison.Ordinal);
        Assert.Contains(PkceMethodParameter, authorize.Query, StringComparison.Ordinal);

        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
        using var answer = await browser.GetAsync(authorize, Ct);
        if (answer.Headers.Location is { } redirect)
        {
            return await client.GetAsync(redirect.PathAndQuery);
        }

        answer.EnsureSuccessStatusCode();
        var html = await answer.Content.ReadAsStringAsync(Ct);
        var action = new Uri(WebUtility.HtmlDecode(FormAction().Match(html).Groups["action"].Value));
        var fields = FormField().Matches(html).ToDictionary(m => m.Groups["name"].Value, m => WebUtility.HtmlDecode(m.Groups["value"].Value));
        using var form = new FormUrlEncodedContent(fields);
        return await client.Http.PostAsync(action.PathAndQuery, form, Ct);
    }

    private static async Task AssertRefusedAsync(OidcContractFactory factory)
    {
        var client = ApiClient.Create(factory);

        using var callback = await SignInAsync(client);
        using var me = await client.GetAsync(ApiClient.MePath);

        Assert.Equal((HttpStatusCode.Redirect, EntraAuthentication.ErrorRedirect), (callback.StatusCode, callback.Headers.Location?.OriginalString));
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    /// <summary>
    /// The token passed the handler's validation (signature, issuer, audience, nonce, PKCE redemption) and ProteoSM's own
    /// checks refused it. Any earlier failure, such as a broken token exchange, fails here instead of passing as a refusal.
    /// </summary>
    private static ClaimsPrincipal AssertValidTokenRefusedByProteone(OidcContractFactory factory)
    {
        Assert.Null(factory.Outcome.Failure);
        return Assert.IsType<ClaimsPrincipal>(factory.Outcome.ValidatedPrincipal);
    }

    /// <summary>The handler rejected the token itself, for exactly this reason; ProteoSM's checks never saw it.</summary>
    private static void AssertTokenRejectedFor<TException>(OidcContractFactory factory)
        where TException : Exception
    {
        Assert.Null(factory.Outcome.ValidatedPrincipal);
        var failure = factory.Outcome.Failure;
        Assert.True(Causes(failure).OfType<TException>().Any(), $"Expected {typeof(TException).Name}, got: {failure}");
    }

    private static IEnumerable<Exception> Causes(Exception? failure)
    {
        for (var current = failure; current is not null; current = current.InnerException)
        {
            yield return current;
            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Causes))
                {
                    yield return inner;
                }
            }
        }
    }

    private static async Task<bool> UserExistsAsync(OidcContractFactory factory, string email)
    {
        await using var db = factory.Database.CreateDbContext();
        var normalized = User.NormalizeEmail(email);
        return await db.Users.AnyAsync(u => u.NormalizedEmail == normalized, Ct);
    }

    [GeneratedRegex("<form[^>]*\\saction=\"(?<action>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex FormAction();

    [GeneratedRegex("<input[^>]*\\sname=\"(?<name>[^\"]+)\"[^>]*\\svalue=\"(?<value>[^\"]*)\"", RegexOptions.IgnoreCase)]
    private static partial Regex FormField();
}
