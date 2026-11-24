using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common.Paging;
using Proteone.Domain.Assets;

namespace Proteone.Application.Features.Assets;

public sealed record ListAssetsQuery(string? Search, AssetType? Type, AssetStatus? Status, Guid? LocationId, int Page, int PageSize, Guid? ServiceId = null);

public sealed class ListAssetsHandler(IAppDbContext db)
{
    public Task<PagedResult<AssetItem>> HandleAsync(ListAssetsQuery query, CancellationToken cancellationToken)
    {
        var assets = db.Assets.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var code = Asset.NormalizeTag(query.Search);
            var text = query.Search.Trim().ToLowerInvariant();
            assets = assets.Where(a =>
                a.NormalizedAssetTag.Contains(code)
                || (a.NormalizedSerialNumber != null && a.NormalizedSerialNumber.Contains(code))
                || a.Model.ToLower().Contains(text)
                || a.Brand.ToLower().Contains(text));
        }

        if (query.Type is { } type)
        {
            assets = assets.Where(a => a.Type == type);
        }

        if (query.Status is { } status)
        {
            assets = assets.Where(a => a.Status == status);
        }

        if (query.LocationId is { } locationId)
        {
            assets = assets.Where(a => a.LocationId == locationId);
        }

        if (query.ServiceId is { } serviceId)
        {
            assets = assets.Where(a => db.ServiceAssets.Any(l => l.AssetId == a.Id && l.ServiceId == serviceId));
        }

        return assets.ToItems(db).ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
