using Tgysm.Domain.Assets;
using Tgysm.Domain.Common;

namespace Tgysm.Domain.Services;

/// <summary>Service ↔ asset link. A retired or lost asset cannot be newly linked; an existing link stays when the asset later changes status.</summary>
public sealed class ServiceAsset
{
    private ServiceAsset()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid ServiceId { get; private init; }
    public Guid AssetId { get; private init; }
    public Guid CreatedById { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public static ServiceAsset Create(Guid serviceId, Asset asset, Guid by, DateTimeOffset now)
    {
        if (asset.Status is AssetStatus.Retired or AssetStatus.Lost)
        {
            throw new DomainException(ErrorCodes.ServiceAssetNotLinkable, "A retired or lost asset cannot be linked to a service.");
        }

        return new ServiceAsset { ServiceId = serviceId, AssetId = asset.Id, CreatedById = by, CreatedAt = now };
    }
}
