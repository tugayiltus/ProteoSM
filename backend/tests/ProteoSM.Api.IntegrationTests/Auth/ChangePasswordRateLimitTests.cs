using System.Net;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Users;

namespace ProteoSM.Api.IntegrationTests.Auth;

/// <summary>A stolen session must not allow unlimited guesses of the current password. Own host: the limit counts per client address.</summary>
public sealed class ChangePasswordRateLimitTests(LowRateLimitApiFactory factory) : IClassFixture<LowRateLimitApiFactory>
{
    [Fact]
    public async Task Wrong_current_passwords_over_the_limit_get_429()
    {
        // Signing in takes one permit of the shared per-client limit.
        var (client, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "e@example.com");
        var body = new { currentPassword = "Wrong-password-1", newPassword = "Another-password-2" };

        for (var attempt = 1; attempt < LowRateLimitApiFactory.Limit; attempt++)
        {
            using var allowed = await client.PostAsync("/api/v1/auth/change-password", body);
            Assert.Equal(HttpStatusCode.BadRequest, allowed.StatusCode);
        }

        using var limited = await client.PostAsync("/api/v1/auth/change-password", body);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(ErrorCodes.RateLimited, await ApiClient.ReadCodeAsync(limited));
    }
}
