using System.Net;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Domain.Users;

namespace Proteone.Api.IntegrationTests.Auth;

/// <summary>Use keeps a session alive (sliding), but not longer than a day after sign-in.</summary>
public sealed class SessionLifetimeTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const string MePath = "/api/v1/auth/me";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task A_session_in_constant_use_ends_after_a_day()
    {
        factory.Clock.SetUtcNow(new DateTimeOffset(2027, 1, 4, 8, 0, 0, TimeSpan.Zero));
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "e@example.com");

        for (var hour = 5; hour < 24; hour += 5)
        {
            factory.Clock.Advance(TimeSpan.FromHours(5));
            using var used = await client.GetAsync(MePath);
            Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        }

        factory.Clock.Advance(TimeSpan.FromHours(4));
        using var ended = await client.GetAsync(MePath);

        Assert.Equal(HttpStatusCode.Unauthorized, ended.StatusCode);
    }
}
