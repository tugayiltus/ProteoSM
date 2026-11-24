using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Application.Features.Changes;
using Proteone.Application.Features.Tickets;
using Proteone.Domain.Audit;
using Proteone.Domain.Changes;
using Proteone.Domain.Problems;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Features.Services;

public sealed record Section<T>(IReadOnlyList<T> Items, int Total);

/// <summary>A linked asset. <c>Id</c> lets an admin unlink it; <c>Tag</c> opens its detail page.</summary>
public sealed record ImpactAsset(Guid Id, string Tag, string Name, string Status);

/// <summary>An open record of a service. Without access the row stays at number, title and status (ADR-0033).</summary>
public sealed record ImpactRecord(string Number, string Title, string Status, bool CanOpen);

public sealed record ServiceImpact(
    Section<ImpactAsset> Assets, Section<ImpactRecord> Tickets, Section<ImpactRecord> Changes, Section<ImpactRecord> Problems);

public sealed record GetServiceImpactQuery(Guid ServiceId);

/// <summary>
/// What a service touches right now: linked assets (retired ones too) and its open tickets, changes and problems,
/// each section capped at <see cref="ServiceImpactLimits.PerSection"/> with a full count. IT staff and admins only.
/// </summary>
public sealed class GetServiceImpactHandler(IAppDbContext db, ICurrentActor actor, TicketScope tickets, ChangeScope changes)
{
    private const string NameSeparator = " ";

    private static readonly TicketStatus[] ClosedTicketStatuses = [TicketStatus.Completed, TicketStatus.Cancelled];
    private static readonly ChangeStatus[] ImpactChangeStatuses =
        [ChangeStatus.PendingApproval, ChangeStatus.Scheduled, ChangeStatus.Implementing, ChangeStatus.Verifying];
    private static readonly ProblemStatus[] ClosedProblemStatuses = [ProblemStatus.Resolved, ProblemStatus.Cancelled];

    public async Task<ServiceImpact> HandleAsync(GetServiceImpactQuery query, CancellationToken cancellationToken)
    {
        await ServiceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        var serviceId = query.ServiceId;
        if (!await db.Services.AnyAsync(s => s.Id == serviceId, cancellationToken))
        {
            throw new NotFoundException(AuditEntityTypes.Service);
        }

        return new ServiceImpact(
            await AssetsAsync(serviceId, cancellationToken),
            await TicketsAsync(serviceId, cancellationToken),
            await ChangesAsync(serviceId, cancellationToken),
            await ProblemsAsync(serviceId, cancellationToken));
    }

    private async Task<Section<ImpactAsset>> AssetsAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        var linked = db.ServiceAssets.AsNoTracking().Where(l => l.ServiceId == serviceId);
        var total = await linked.CountAsync(cancellationToken);
        var rows = await (
            from l in linked
            join a in db.Assets on l.AssetId equals a.Id
            orderby a.NormalizedAssetTag, a.Id
            select new { a.Id, a.AssetTag, a.Brand, a.Model, a.Status })
            .Take(ServiceImpactLimits.PerSection).ToListAsync(cancellationToken);
        return new Section<ImpactAsset>(
            [.. rows.Select(r => new ImpactAsset(r.Id, r.AssetTag, r.Brand + NameSeparator + r.Model, Wire(r.Status)))], total);
    }

    private async Task<Section<ImpactRecord>> TicketsAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        var viewer = await tickets.ViewerAsync(cancellationToken);
        var openable = tickets.Visible(viewer);
        var open = db.Tickets.AsNoTracking().Where(t => t.ServiceId == serviceId && !ClosedTicketStatuses.Contains(t.Status));
        var total = await open.CountAsync(cancellationToken);
        var rows = await open
            .OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Select(t => new { t.Number, t.Subject, t.Status, CanOpen = openable.Any(o => o.Id == t.Id) })
            .Take(ServiceImpactLimits.PerSection).ToListAsync(cancellationToken);
        return new Section<ImpactRecord>([.. rows.Select(r => new ImpactRecord(r.Number, r.Subject, Wire(r.Status), r.CanOpen))], total);
    }

    private async Task<Section<ImpactRecord>> ChangesAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        var viewer = await changes.ViewerAsync(cancellationToken);
        var openable = changes.Visible(viewer);
        var open = db.ChangeRequests.AsNoTracking().Where(c => c.ServiceId == serviceId && ImpactChangeStatuses.Contains(c.Status));
        var total = await open.CountAsync(cancellationToken);
        var rows = await open
            .OrderByDescending(c => c.WindowStart).ThenByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .Select(c => new { c.Number, c.Title, c.Status, CanOpen = openable.Any(o => o.Id == c.Id) })
            .Take(ServiceImpactLimits.PerSection).ToListAsync(cancellationToken);
        return new Section<ImpactRecord>([.. rows.Select(r => new ImpactRecord(r.Number, r.Title, Wire(r.Status), r.CanOpen))], total);
    }

    /// <summary>Every IT staff member and admin reads every problem (ADR-0034), so problem rows always open.</summary>
    private async Task<Section<ImpactRecord>> ProblemsAsync(Guid serviceId, CancellationToken cancellationToken)
    {
        var open = db.Problems.AsNoTracking().Where(p => p.ServiceId == serviceId && !ClosedProblemStatuses.Contains(p.Status));
        var total = await open.CountAsync(cancellationToken);
        var rows = await open
            .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
            .Select(p => new { p.Number, p.Title, p.Status })
            .Take(ServiceImpactLimits.PerSection).ToListAsync(cancellationToken);
        return new Section<ImpactRecord>([.. rows.Select(r => new ImpactRecord(r.Number, r.Title, Wire(r.Status), true))], total);
    }

    /// <summary>The status as the API writes enums everywhere else: snake_case.</summary>
    private static string Wire<TEnum>(TEnum value)
        where TEnum : struct, Enum => JsonNamingPolicy.SnakeCaseLower.ConvertName(value.ToString());
}
