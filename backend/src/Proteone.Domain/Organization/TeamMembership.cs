using Proteone.Domain.Common;

namespace Proteone.Domain.Organization;

public sealed class TeamMembership : Entity
{
    private TeamMembership()
    {
    }

    public Guid TeamId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static TeamMembership Create(Guid teamId, Guid userId, DateTimeOffset now) =>
        new() { TeamId = teamId, UserId = userId, CreatedAt = now };
}
