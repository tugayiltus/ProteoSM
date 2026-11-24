using System.Net;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Domain.Common;

namespace Proteone.Api.IntegrationTests.Auth;

public sealed class LowRateLimitApiFactory : ApiFactory
{
    public const int Limit = 2;

    protected override int LoginPermitLimitPerMinute => Limit;

    protected override int ForgotPasswordPermitLimitPerMinute => Limit;

    protected override int SetPasswordPermitLimitPerMinute => Limit;

    protected override int MonitoringPermitLimitPerMinute => Limit;
}

public sealed class RateLimitTests(LowRateLimitApiFactory factory) : IClassFixture<LowRateLimitApiFactory>
{
    [Fact]
    public async Task Login_attempts_over_the_limit_get_429()
    {
        var client = ApiClient.Create(factory);
        var credentials = new { email = "nobody@example.com", password = "Wrong-password-1" };

        for (var attempt = 0; attempt < LowRateLimitApiFactory.Limit; attempt++)
        {
            using var allowed = await client.PostAsync(ApiClient.LoginPath, credentials);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        using var limited = await client.PostAsync(ApiClient.LoginPath, credentials);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(ErrorCodes.RateLimited, await ApiClient.ReadCodeAsync(limited));
    }

    [Fact]
    public async Task Monitoring_events_over_the_limit_get_429_before_any_check()
    {
        var path = $"/api/v1/monitoring/{Guid.NewGuid()}/events";
        var client = ApiClient.Create(factory);
        for (var attempt = 0; attempt < LowRateLimitApiFactory.Limit; attempt++)
        {
            using var refused = await client.PostAsync(path, new { }, withCsrf: false);
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
        }

        using var limited = await client.PostAsync(path, new { }, withCsrf: false);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Fact]
    public async Task Forgot_password_requests_over_the_limit_get_429()
    {
        const string forgotPasswordPath = "/api/v1/auth/forgot-password";
        var client = ApiClient.Create(factory);
        var body = new { email = "nobody@example.com" };

        for (var attempt = 0; attempt < LowRateLimitApiFactory.Limit; attempt++)
        {
            using var allowed = await client.PostAsync(forgotPasswordPath, body);
            Assert.Equal(HttpStatusCode.Accepted, allowed.StatusCode);
        }

        using var limited = await client.PostAsync(forgotPasswordPath, body);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(ErrorCodes.RateLimited, await ApiClient.ReadCodeAsync(limited));
    }

    [Fact]
    public async Task Set_password_requests_over_the_limit_get_429()
    {
        const string setPasswordPath = "/api/v1/auth/set-password";
        var client = ApiClient.Create(factory);
        var body = new { token = "not-a-real-token", newPassword = "Brand-new-password-2026" };

        for (var attempt = 0; attempt < LowRateLimitApiFactory.Limit; attempt++)
        {
            using var allowed = await client.PostAsync(setPasswordPath, body);
            Assert.Equal(HttpStatusCode.BadRequest, allowed.StatusCode);
        }

        using var limited = await client.PostAsync(setPasswordPath, body);

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal(ErrorCodes.RateLimited, await ApiClient.ReadCodeAsync(limited));
    }
}
