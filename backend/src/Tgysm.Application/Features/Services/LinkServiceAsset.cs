using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Services;

namespace Tgysm.Application.Features.Services;

public sealed record LinkServiceAssetCommand(Guid ServiceId, Guid AssetId);

/// <summary>
/// Links an asset to a service. Runs under the asset lifecycle lock, so the status check cannot race a retire or a
/// "lost" mark: a link is never written for an asset that is already retired or lost.
/// </summary>
public sealed class LinkServiceAssetHandler(IAppDbContext db, ICurrentActor actor, IAuditLog audit, TimeProvider clock)
{
    public async Task<ServiceAssetItem> HandleAsync(LinkServiceAssetCommand command, CancellationToken cancellationToken)
    {
        var actorId = actor.UserId ?? throw new AuthenticationFailedException();
        ServiceAssetItem? item = null;
        await db.RunSerializedAsync(SerializationLocks.AssetLifecycle, async ct =>
        {
            if (!await db.Services.AnyAsync(s => s.Id == command.ServiceId, ct))
            {
                throw new NotFoundException(AuditEntityTypes.Service);
            }

            var asset = await db.Assets.AsNoTracking().SingleOrDefaultAsync(a => a.Id == command.AssetId, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Asset);
            if (await db.ServiceAssets.AnyAsync(l => l.ServiceId == command.ServiceId && l.AssetId == command.AssetId, ct))
            {
                throw new DomainException(ErrorCodes.ServiceAssetLinkExists, ServiceQueries.LinkExistsMessage);
            }

            var link = ServiceAsset.Create(command.ServiceId, asset, actorId, clock.GetUtcNow());
            db.ServiceAssets.Add(link);
            audit.Record(AuditActions.ServiceAssetLinked, AuditEntityTypes.Service, command.ServiceId,
                newValues: new { link.AssetId, asset.AssetTag });
            await db.SaveTranslatingDuplicateAsync(ErrorCodes.ServiceAssetLinkExists, ServiceQueries.LinkExistsMessage, ct);
            item = new ServiceAssetItem(asset.Id, asset.AssetTag, asset.Status, link.CreatedAt);
        }, cancellationToken);
        return item!;
    }
}
