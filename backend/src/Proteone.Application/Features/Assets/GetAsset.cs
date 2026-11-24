using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Audit;

namespace Proteone.Application.Features.Assets;

public sealed record GetAssetQuery(Guid Id);

public sealed class GetAssetHandler(IAppDbContext db)
{
    public async Task<AssetItem> HandleAsync(GetAssetQuery query, CancellationToken cancellationToken) =>
        await db.Assets.AsNoTracking().Where(a => a.Id == query.Id).ToItems(db).SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(AuditEntityTypes.Asset);
}
