using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Proteone.Api.Auth;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Application.Abstractions;
using Proteone.Infrastructure.Identity;

namespace Proteone.Api.IntegrationTests.Auth;

/// <summary>
/// The local OpenID Connect server named by TGYSM_TEST_OIDC_URL and TGYSM_TEST_OIDC_TENANT
/// (tools/directory-contract-test.sh, deploy/test-directory/compose.yaml); null when either is missing.
/// </summary>
public sealed record OidcServer(Uri BaseUrl, string Tenant)
{
    private const string Prefix = "TGYSM_TEST_OIDC_";
    private const string MetadataPath = ".well-known/openid-configuration";

    public static OidcServer? FromEnvironment() =>
        Environment.GetEnvironmentVariable(Prefix + "URL") is { Length: > 0 } url && Environment.GetEnvironmentVariable(Prefix + "TENANT") is { Length: > 0 } tenant
            ? new OidcServer(new Uri(url.TrimEnd('/') + "/"), tenant)
            : null;

    /// <summary>mock-oauth2-server serves one issuer per first path segment; the tenant id is the issuer id.</summary>
    public Uri Metadata => new(BaseUrl, $"{Tenant}/{MetadataPath}");
}

/// <summary>Builds the API host only when a server is configured, so the skipped run never creates a database.</summary>
public sealed class OidcContractFixture : IAsyncLifetime
{
    public OidcServer? Server { get; } = OidcServer.FromEnvironment();

    public OidcContractFactory? Factory { get; private set; }

    public async ValueTask InitializeAsync()
    {
        if (Server is null)
        {
            return;
        }

        Factory = new OidcContractFactory(Server);
        await Factory.InitializeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Factory is not null)
        {
            await Factory.DisposeAsync();
        }
    }
}

/// <summary>
/// The API host with the real OpenID Connect handler pointed at the local server: metadata, endpoints and signing keys
/// come from there instead of login.microsoftonline.com. Only test wiring changes; every production check still runs.
/// </summary>
public sealed class OidcContractFactory(OidcServer server) : ApiFactory
{
    private readonly HttpClient _testerHttp = new(new MicrosoftAuthorityRewrite(server.BaseUrl));

    public OidcServer OidcServer { get; } = server;

    public TamperingConfigurationManager Configuration { get; } = new(server.Metadata);

    /// <summary>Where the last sign-in on this host was refused, so each refusal test can assert its own cause.</summary>
    public OidcOutcome Outcome { get; } = new();

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        _testerHttp.Dispose();
        Configuration.Dispose();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            // Registered after ApiFactory's static endpoints, so this configuration is the one the handler uses.
            // Runs after the production setup (EntraAuthentication), so the recorders wrap its events and then call them
            // unchanged: the production handling of every ticket and failure still decides the response.
            services.PostConfigure<OpenIdConnectOptions>(EntraAuthentication.Scheme, options =>
            {
                options.ConfigurationManager = Configuration;
                var onRemoteFailure = options.Events.OnRemoteFailure;
                options.Events.OnRemoteFailure = context =>
                {
                    Outcome.Failure = context.Failure;
                    return onRemoteFailure(context);
                };
                var onTicketReceived = options.Events.OnTicketReceived;
                options.Events.OnTicketReceived = context =>
                {
                    // The validated token's principal, before the production event swaps in the TGYSM session principal.
                    Outcome.ValidatedPrincipal = context.Principal;
                    return onTicketReceived(context);
                };
            });
            services.RemoveAll<IEntraConnectionTester>();
            services.AddSingleton<IEntraConnectionTester>(new EntraConnectionTester(_testerHttp));
        });
    }
}

/// <summary>
/// What the OpenID Connect handler saw on the last sign-in: a remote failure (token redemption or validation failed) or
/// the principal of a token that passed validation and went on to the TGYSM checks (tenant, domain, guest).
/// </summary>
public sealed class OidcOutcome
{
    public Exception? Failure { get; set; }

    public ClaimsPrincipal? ValidatedPrincipal { get; set; }

    public void Reset()
    {
        Failure = null;
        ValidatedPrincipal = null;
    }
}

/// <summary>What <see cref="TamperingConfigurationManager"/> changes in the served metadata for one test.</summary>
public enum OidcTamper
{
    None,

    /// <summary>The published key is swapped for another key with the same key id: the token signature no longer verifies.</summary>
    ForeignSigningKey,

    /// <summary>The expected issuer is another issuer on the same server: the token's <c>iss</c> no longer matches.</summary>
    ForeignIssuer,
}

/// <summary>
/// The local server's real metadata, optionally tampered with to stand for a token from an untrusted signer or issuer.
/// Not a <see cref="BaseConfigurationManager"/>, so the handler validates against exactly this configuration.
/// </summary>
public sealed class TamperingConfigurationManager(Uri metadata) : IConfigurationManager<OpenIdConnectConfiguration>, IDisposable
{
    private const int ForeignKeyBits = 2048;
    private const string ForeignIssuerPath = "/another-issuer";
    private readonly RSA _foreignKey = RSA.Create(ForeignKeyBits);

    // The local mock speaks plain HTTP on 127.0.0.1. Production keeps RequireHttpsMetadata: its authority is always HTTPS.
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _server =
        new(metadata.AbsoluteUri, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = false });

    public OidcTamper Tamper { get; set; }

    public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)
    {
        var served = await _server.GetConfigurationAsync(cancel);
        var configuration = new OpenIdConnectConfiguration
        {
            AuthorizationEndpoint = served.AuthorizationEndpoint,
            TokenEndpoint = served.TokenEndpoint,
            JwksUri = served.JwksUri,
            Issuer = Tamper == OidcTamper.ForeignIssuer ? new Uri(new Uri(served.Issuer), ForeignIssuerPath).AbsoluteUri : served.Issuer,
        };
        foreach (var key in served.SigningKeys)
        {
            configuration.SigningKeys.Add(Tamper == OidcTamper.ForeignSigningKey ? new RsaSecurityKey(_foreignKey) { KeyId = key.KeyId } : key);
        }

        return configuration;
    }

    public void RequestRefresh() => _server.RequestRefresh();

    public void Dispose() => _foreignKey.Dispose();
}

/// <summary>
/// Sends <see cref="EntraConnectionTester"/>'s requests for login.microsoftonline.com to another server:
/// <c>/{tenant}/v2.0/.well-known/openid-configuration</c> becomes <c>{target}/{tenant}/.well-known/openid-configuration</c>,
/// the path layout of mock-oauth2-server. The token endpoint comes from the served metadata and is left alone.
/// </summary>
public sealed class MicrosoftAuthorityRewrite(Uri target) : DelegatingHandler(new HttpClientHandler())
{
    private const string VersionSegment = "/v2.0";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = request.RequestUri!;
        if (string.Equals(uri.GetLeftPart(UriPartial.Authority), EntraConnectionTester.Authority, StringComparison.OrdinalIgnoreCase))
        {
            request.RequestUri = new Uri(target, uri.AbsolutePath.Replace(VersionSegment, string.Empty, StringComparison.Ordinal).TrimStart('/'));
        }

        return base.SendAsync(request, cancellationToken);
    }
}
