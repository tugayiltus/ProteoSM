using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace ProteoSM.Api.Security;

/// <summary>
/// The time of sign-in inside the session cookie. Sliding renewal moves the cookie's own times but keeps this one, so a
/// session in constant use still ends (<see cref="SecurityOptions.MaxSessionHours"/>).
/// </summary>
internal static class SessionLifetime
{
    private const string SignedInAtKey = "proteone.signed_in_at";
    private const string RoundTripFormat = "O";

    public static Task StampAsync(CookieSigningInContext context)
    {
        var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
        context.Properties.Items[SignedInAtKey] = now.ToString(RoundTripFormat, CultureInfo.InvariantCulture);
        return Task.CompletedTask;
    }

    public static bool IsOver(AuthenticationProperties properties, IServiceProvider services)
    {
        if (!properties.Items.TryGetValue(SignedInAtKey, out var stamp)
            || !DateTimeOffset.TryParseExact(stamp, RoundTripFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var signedInAt))
        {
            return true;
        }

        var limit = TimeSpan.FromHours(services.GetRequiredService<IOptions<SecurityOptions>>().Value.MaxSessionHours);
        return services.GetRequiredService<TimeProvider>().GetUtcNow() - signedInAt >= limit;
    }
}
