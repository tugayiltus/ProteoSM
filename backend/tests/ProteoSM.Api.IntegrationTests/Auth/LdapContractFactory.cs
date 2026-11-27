using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Auth;
using ProteoSM.Infrastructure.Identity;

namespace ProteoSM.Api.IntegrationTests.Auth;

/// <summary>
/// The test directory named by the PROTEONE_TEST_LDAP_* variables of <see cref="NovellLdapDirectoryTests"/>, plus
/// PROTEONE_TEST_LDAP_EMAIL (the mail of PROTEONE_TEST_LDAP_USER). Null when any is missing.
/// </summary>
public sealed record LdapDirectoryServer(
    string Host, int Port, LdapSecurity Security, string BaseDn, string BindDn, string BindPassword, string User, string Password, string Email)
{
    private const string Prefix = "PROTEONE_TEST_LDAP_";

    /// <summary>Set only for the run after the directory container was stopped (tools/directory-contract-test.sh).</summary>
    public const string StoppedVariable = Prefix + "STOPPED";

    /// <summary>Value of <see cref="StoppedVariable"/> that marks the directory as stopped.</summary>
    public const string StoppedValue = "1";

    public static bool IsStopped => Environment.GetEnvironmentVariable(StoppedVariable) == StoppedValue;

    public static LdapDirectoryServer? FromEnvironment() =>
        Env("HOST") is { } host && Env("PORT") is { } port && Env("SECURITY") is { } security && Env("BASE_DN") is { } baseDn
        && Env("BIND_DN") is { } bindDn && Env("BIND_PASSWORD") is { } bindPassword && Env("USER") is { } user
        && Env("PASSWORD") is { } password && Env("EMAIL") is { } email
            ? new LdapDirectoryServer(host, int.Parse(port, System.Globalization.CultureInfo.InvariantCulture),
                Enum.Parse<LdapSecurity>(security, ignoreCase: true), baseDn, bindDn, bindPassword, user, password, email)
            : null;

    private static string? Env(string name) => Environment.GetEnvironmentVariable(Prefix + name) is { Length: > 0 } value ? value : null;
}

/// <summary>Builds the API host only when a directory is configured, so the skipped run never creates a database.</summary>
public sealed class LdapContractFixture : IAsyncLifetime
{
    public LdapDirectoryServer? Server { get; } = LdapDirectoryServer.FromEnvironment();

    public LdapContractFactory? Factory { get; private set; }

    public async ValueTask InitializeAsync()
    {
        if (Server is null)
        {
            return;
        }

        Factory = new LdapContractFactory();
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

/// <summary>The API host with the real LDAP client instead of <see cref="FakeLdapDirectory"/> (ADR-0030).</summary>
public sealed class LdapContractFactory : ApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILdapDirectory>();
            services.AddSingleton<ILdapDirectory, NovellLdapDirectory>();
        });
    }
}
