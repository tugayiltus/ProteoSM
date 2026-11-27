using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ProteoSM.Domain.Users;

namespace ProteoSM.Infrastructure.Persistence.Converters;

internal static class UserRoleConversion
{
    public const int MaxLength = 20;

    public static readonly ValueConverter<UserRole, string> Converter = new(
        role => UserRoleNames.ToName(role),
        name => Parse(name));

    private static UserRole Parse(string name) =>
        UserRoleNames.TryParse(name, out var role)
            ? role
            : throw new InvalidOperationException($"Unknown role '{name}' in database.");
}
