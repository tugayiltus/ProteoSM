using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Auth;
using ProteoSM.Domain.Common;
using ProteoSM.Infrastructure.Identity;

namespace ProteoSM.Api.IntegrationTests.Auth;

/// <summary>
/// With the directory container stopped, an LDAP sign-in answers 503 <c>auth.directory_unavailable</c> instead of hanging
/// or claiming a wrong password (ADR-0030). Runs only when tools/directory-contract-test.sh has stopped the container and
/// set PROTEOSM_TEST_LDAP_STOPPED=1 next to the PROTEOSM_TEST_LDAP_* variables; skipped otherwise.
/// </summary>
public sealed class LdapOutageContractTests(LdapContractFixture fixture) : IClassFixture<LdapContractFixture>, IAsyncLifetime
{
    private const string LdapLoginPath = "/api/v1/auth/login/ldap";
    private const string NotStopped = "The test directory is not stopped (tools/directory-contract-test.sh sets " + LdapDirectoryServer.StoppedVariable + ").";

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
    public async Task A_stopped_directory_is_reported_as_unavailable()
    {
        Assert.SkipWhen(fixture.Factory is null || !LdapDirectoryServer.IsStopped, NotStopped);
        var (factory, server) = (fixture.Factory!, fixture.Server!);
        // The settings go in directly: the admin test needs a running directory, and it passed in the earlier run.
        await ActivateAsync(factory, server);
        var started = System.Diagnostics.Stopwatch.StartNew();

        using var login = await ApiClient.Create(factory).PostAsync(LdapLoginPath, new { identifier = server.User, password = server.Password });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, login.StatusCode);
        Assert.Equal(ErrorCodes.AuthDirectoryUnavailable, await ApiClient.ReadCodeAsync(login));
        Assert.True(started.Elapsed < NovellLdapDirectory.OperationTimeout * 2, started.Elapsed.ToString());
    }

    private static async Task ActivateAsync(LdapContractFactory factory, LdapDirectoryServer server)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var secrets = scope.ServiceProvider.GetRequiredService<IAuthSecretProtector>();
        await using var db = factory.Database.CreateDbContext();
        var settings = await db.AuthSettings.SingleAsync(Ct);
        var now = DateTimeOffset.UtcNow;
        settings.UpdateLdap(server.Host, server.Port, server.Security, server.BaseDn, server.BindDn, secrets.Protect(server.BindPassword), null, now);
        settings.MarkTested(LoginMethod.Ldap);
        settings.SwitchTo(LoginMethod.Ldap, now);
        await db.SaveChangesAsync(Ct);
    }
}
