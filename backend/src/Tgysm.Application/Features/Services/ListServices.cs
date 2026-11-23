using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Application.Common.Paging;
using Tgysm.Domain.Common;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Services;

public sealed record ListServicesQuery(bool? Active, Guid? TeamId, string? Search, int Page, int PageSize);

/// <summary>Lists services for IT staff and admins, with asset and open-record counts. Employees get not found.</summary>
public sealed class ListServicesHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<PagedResult<ServiceListItem>> HandleAsync(ListServicesQuery query, CancellationToken cancellationToken)
    {
        await ServiceQueries.EnsureStaffAsync(db, actor, cancellationToken);

        var services = db.Services.AsNoTracking();
        if (query.Active is { } active)
        {
            services = services.Where(s => s.IsActive == active);
        }

        if (query.TeamId is { } teamId)
        {
            services = services.Where(s => s.OwnerTeamId == teamId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Containing(TurkishText.ToSearchKey(query.Search));
            services = services.Where(s => EF.Functions.Like(s.SearchText, pattern, LikePattern.EscapeText));
        }

        return await (
            from service in services
            join team in db.Teams on service.OwnerTeamId equals team.Id
            orderby service.NormalizedName, service.Id
            select new ServiceListItem(
                service.Id,
                service.Name,
                service.OwnerTeamId,
                team.Name,
                service.IsActive,
                db.ServiceAssets.Count(l => l.ServiceId == service.Id),
                db.Tickets.Count(t => t.ServiceId == service.Id && t.Status != TicketStatus.Completed && t.Status != TicketStatus.Cancelled)))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}
