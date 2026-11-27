using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using ProteoSM.Application.Features.Auth;
using ProteoSM.Domain.Users;

namespace ProteoSM.Api.Security;

internal static class SessionClaims
{
    public const string SecurityStamp = "proteone:security_stamp";
    public const string MustChangePassword = "proteone:must_change_password";

    public static ClaimsPrincipal CreatePrincipal(SessionUser session)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new(ClaimTypes.Role, UserRoleNames.ToName(session.Role)),
            new(SecurityStamp, session.SecurityStamp),
            new(MustChangePassword, session.MustChangePassword ? bool.TrueString : bool.FalseString),
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
