using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Audit;

namespace Proteone.Application.Features.Users;

public sealed record GetUserQuery(Guid Id);

public sealed class GetUserHandler(IAppDbContext db)
{
    public async Task<UserDetail> HandleAsync(GetUserQuery query, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == query.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.User);
        return UserMapping.ToDetail(user);
    }
}
