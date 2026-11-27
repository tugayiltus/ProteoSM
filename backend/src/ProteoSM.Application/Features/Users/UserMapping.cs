using ProteoSM.Domain.Users;

namespace ProteoSM.Application.Features.Users;

internal static class UserMapping
{
    public static UserDetail ToDetail(User user) => new(
        user.Id,
        user.FirstName,
        user.LastName,
        user.Email,
        user.Role,
        user.IsActive,
        user.MustChangePassword,
        user.DepartmentId,
        user.LocationId,
        user.CreatedAt,
        user.Version);
}
