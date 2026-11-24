using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Domain.Common;
using Proteone.Domain.Users;

namespace Proteone.Application.Features.Problems;

/// <summary>Reference checks shared by problem creation, transfer and owner changes.</summary>
internal static class ProblemRules
{
    /// <summary>The owner must be active IT staff or admin and a member of the owner team.</summary>
    public static async Task EnsureOwnerInTeamAsync(IAppDbContext db, Guid teamId, Guid ownerId, CancellationToken cancellationToken)
    {
        var member = await (
            from m in db.TeamMemberships
            join u in db.Users on m.UserId equals u.Id
            where m.TeamId == teamId && u.Id == ownerId && u.IsActive && u.Role != UserRole.Employee
            select m).AnyAsync(cancellationToken);
        if (!member)
        {
            throw new DomainException(ErrorCodes.ProblemOwnerNotInTeam, "The owner is not an active member of the owner team.");
        }
    }
}
