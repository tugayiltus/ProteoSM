using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Audit;

namespace ProteoSM.Application.Features.Assets;

public sealed record GetAssetQuery(Guid Id);

public sealed class GetAssetHandler(IAppDbContext db)
{
    public async Task<AssetItem> HandleAsync(GetAssetQuery query, CancellationToken cancellationToken) =>
        await db.Assets.AsNoTracking().Where(a => a.Id == query.Id).ToItems(db).SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(AuditEntityTypes.Asset);
}
