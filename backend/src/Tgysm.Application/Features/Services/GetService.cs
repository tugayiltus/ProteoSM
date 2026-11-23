using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Services;

public sealed record GetServiceQuery(Guid Id);

/// <summary>One service for IT staff and admins; employees get not found.</summary>
public sealed class GetServiceHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<ServiceDetail> HandleAsync(GetServiceQuery query, CancellationToken cancellationToken)
    {
        await ServiceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        return await ServiceQueries.LoadDetailAsync(db, query.Id, cancellationToken);
    }
}

public sealed record ListAssetServicesQuery(string AssetTag);

/// <summary>The services an asset is linked to, active or not, by asset tag; employees get not found.</summary>
public sealed class ListAssetServicesHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<IReadOnlyList<ServiceRef>> HandleAsync(ListAssetServicesQuery query, CancellationToken cancellationToken)
    {
        await ServiceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        var tag = Asset.NormalizeTag(query.AssetTag);
        var assetId = await db.Assets.Where(a => a.NormalizedAssetTag == tag).Select(a => (Guid?)a.Id).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Asset);

        return await (
            from link in db.ServiceAssets.AsNoTracking()
            where link.AssetId == assetId
            join service in db.Services on link.ServiceId equals service.Id
            orderby service.NormalizedName, service.Id
            select new ServiceRef(service.Id, service.Name, service.IsActive))
            .ToListAsync(cancellationToken);
    }
}
