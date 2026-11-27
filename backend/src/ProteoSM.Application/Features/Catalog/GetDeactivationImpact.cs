using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Application.Features.Services;
using ProteoSM.Domain.Audit;

namespace ProteoSM.Application.Features.Catalog;

/// <summary>Wire names of the counts; the admin screen turns each one into text.</summary>
public static class DeactivationCounts
{
    public const string Models = "models";
    public const string ActiveModels = "active_models";
    public const string Assets = "assets";
    public const string ActiveChildren = "active_children";
    public const string OpenTickets = "open_tickets";
}

public sealed record ImpactCount(string Key, int Count);

/// <summary>Records that still point at a catalog entry. Deactivating the entry never changes them.</summary>
public sealed record DeactivationImpact(IReadOnlyList<ImpactCount> Counts);

public sealed class GetBrandDeactivationImpactHandler(IAppDbContext db)
{
    public async Task<DeactivationImpact> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Brands.AnyAsync(b => b.Id == id, cancellationToken))
        {
            throw new NotFoundException(AuditEntityTypes.Brand);
        }

        var models = db.AssetModels.AsNoTracking().Where(m => m.BrandId == id);
        return new DeactivationImpact(
        [
            new(DeactivationCounts.Models, await models.CountAsync(cancellationToken)),
            new(DeactivationCounts.ActiveModels, await models.CountAsync(m => m.IsActive, cancellationToken)),
            new(DeactivationCounts.Assets, await db.Assets.AsNoTracking().CountAsync(a => models.Any(m => m.Id == a.ModelId), cancellationToken)),
        ]);
    }
}

public sealed class GetAssetModelDeactivationImpactHandler(IAppDbContext db)
{
    public async Task<DeactivationImpact> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.AssetModels.AnyAsync(m => m.Id == id, cancellationToken))
        {
            throw new NotFoundException(AuditEntityTypes.AssetModel);
        }

        return new DeactivationImpact([new(DeactivationCounts.Assets, await db.Assets.AsNoTracking().CountAsync(a => a.ModelId == id, cancellationToken))]);
    }
}

public sealed class GetCategoryDeactivationImpactHandler(IAppDbContext db)
{
    public async Task<DeactivationImpact> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == id, cancellationToken))
        {
            throw new NotFoundException(AuditEntityTypes.Category);
        }

        return new DeactivationImpact(
        [
            new(DeactivationCounts.ActiveChildren, await db.Categories.AsNoTracking().CountAsync(c => c.ParentId == id && c.IsActive, cancellationToken)),
            new(DeactivationCounts.OpenTickets, await db.Tickets.AsNoTracking().CountAsync(t => t.CategoryId == id && !ImpactStatuses.ClosedTickets.Contains(t.Status), cancellationToken)),
        ]);
    }
}

public sealed class GetRequestTypeDeactivationImpactHandler(IAppDbContext db)
{
    public async Task<DeactivationImpact> HandleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await db.RequestTypes.AnyAsync(r => r.Id == id, cancellationToken))
        {
            throw new NotFoundException(AuditEntityTypes.RequestType);
        }

        return new DeactivationImpact([new(DeactivationCounts.OpenTickets, await db.Tickets.AsNoTracking().CountAsync(t => t.RequestTypeId == id && !ImpactStatuses.ClosedTickets.Contains(t.Status), cancellationToken))]);
    }
}
