using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;

namespace Tgysm.Application.Features.Services;

public sealed record UnlinkServiceAssetCommand(Guid ServiceId, Guid AssetId);

/// <summary>Removes a service ↔ asset link under the asset lifecycle lock; an unknown link is not found.</summary>
public sealed class UnlinkServiceAssetHandler(IAppDbContext db, IAuditLog audit)
{
    public Task HandleAsync(UnlinkServiceAssetCommand command, CancellationToken cancellationToken) =>
        db.RunSerializedAsync(SerializationLocks.AssetLifecycle, async ct =>
        {
            var link = await db.ServiceAssets.SingleOrDefaultAsync(l => l.ServiceId == command.ServiceId && l.AssetId == command.AssetId, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Asset);
            var assetTag = await db.Assets.Where(a => a.Id == command.AssetId).Select(a => a.AssetTag).SingleAsync(ct);

            db.ServiceAssets.Remove(link);
            audit.Record(AuditActions.ServiceAssetUnlinked, AuditEntityTypes.Service, command.ServiceId,
                oldValues: new { link.AssetId, AssetTag = assetTag });
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
}
