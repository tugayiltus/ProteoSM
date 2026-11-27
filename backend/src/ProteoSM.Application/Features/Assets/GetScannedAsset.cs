using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Assets;
using ProteoSM.Domain.Audit;

namespace ProteoSM.Application.Features.Assets;

/// <summary>What a scanned QR label shows on the incident form; nothing about assignee, location or notes (ADR-0028).</summary>
public sealed record ScannedAssetDto(Guid Id, string AssetTag, AssetType Type, string Brand, string Model, bool CanReport);

/// <summary>Any signed-in user may read a scanned device, so an employee can report a fault on it.</summary>
public sealed class GetScannedAssetHandler(IAppDbContext db)
{
    public async Task<ScannedAssetDto> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        var asset = await db.Assets.AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new { a.Id, a.AssetTag, a.Type, a.Brand, a.Model, a.Status })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Asset);
        return new ScannedAssetDto(asset.Id, asset.AssetTag, asset.Type, asset.Brand, asset.Model, Asset.CanBeReported(asset.Status));
    }
}
