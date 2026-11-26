using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Application.Common.Paging;
using Proteone.Application.Features.Services;
using Proteone.Application.Sla;
using Proteone.Domain.Common;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Features.Tickets;

public enum TicketQueue
{
    Mine,
    Team,
    AssignedToMe,
    Unassigned,
    OnHold,
    SlaRisk,
    ToRate,
}

public sealed record ListTicketsQuery(TicketQueue? Queue, TicketStatus? Status, TicketKind? Kind, Priority? Priority, string? Search, int Page, int PageSize, Guid? ServiceId, bool OpenOnly = false);

/// <summary>Lists tickets in the viewer's scope; queues and filters only narrow it.</summary>
public sealed class ListTicketsHandler(IAppDbContext db, TicketScope scope, TicketSlaReader slaReader, TimeProvider clock)
{
    private const string NameSeparator = " ";

    public async Task<PagedResult<TicketListItem>> HandleAsync(ListTicketsQuery query, CancellationToken cancellationToken)
    {
        var viewer = await scope.ViewerAsync(cancellationToken);
        if ((query.Queue == TicketQueue.SlaRisk || query.ServiceId is not null) && !viewer.SeesInternal)
        {
            throw new ForbiddenException();
        }

        var tickets = scope.Visible(viewer).AsNoTracking();
        var teamIds = viewer.TeamIds;
        var ratingWindowStart = clock.GetUtcNow() - RatingLimits.Window;
        tickets = query.Queue switch
        {
            TicketQueue.Mine => tickets.Where(t => t.RequesterId == viewer.UserId),
            TicketQueue.Team => tickets.Where(t => teamIds.Contains(t.TeamId)),
            TicketQueue.AssignedToMe => tickets.Where(t => t.AssigneeId == viewer.UserId),
            TicketQueue.Unassigned => tickets.Where(t => t.AssigneeId == null && t.Status != TicketStatus.Completed && t.Status != TicketStatus.Cancelled),
            TicketQueue.OnHold => tickets.Where(t => t.Status == TicketStatus.OnHold),
            TicketQueue.SlaRisk => tickets.Where(t => db.SlaInstances.Any(i => i.TicketId == t.Id && i.StoppedAt == null && (i.WarnedAt != null || i.BreachedAt != null))),
            // Only the actor's own tickets, for every role: rating is the requester's act.
            TicketQueue.ToRate => tickets.Where(t => t.RequesterId == viewer.UserId
                && t.Status == TicketStatus.Completed
                && t.Outcome != null && TicketRating.RateableOutcomes.Contains(t.Outcome.Value)
                && t.CompletedAt > ratingWindowStart
                && !db.TicketRatings.Any(r => r.TicketId == t.Id)),
            _ => tickets,
        };

        if (query.Status is { } status)
        {
            tickets = tickets.Where(t => t.Status == status);
        }

        if (query.Kind is { } kind)
        {
            tickets = tickets.Where(t => t.Kind == kind);
        }

        if (query.Priority is { } priority)
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }

        if (query.OpenOnly)
        {
            tickets = tickets.Where(t => !ImpactStatuses.ClosedTickets.Contains(t.Status));
        }

        if (query.ServiceId is { } serviceId)
        {
            tickets = tickets.Where(t => t.ServiceId == serviceId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var number = TicketNumber.Normalize(query.Search);
            var key = TurkishText.ToSearchKey(query.Search);
            tickets = TicketNumber.IsValid(number)
                ? tickets.Where(t => t.Number == number)
                : tickets.Where(t => EF.Functions.Like(t.SearchText, LikePattern.Containing(key), LikePattern.EscapeText));
        }

        var ordered = query.Queue == TicketQueue.ToRate
            ? tickets.OrderByDescending(t => t.CompletedAt).ThenByDescending(t => t.Id)
            : tickets.OrderByDescending(t => t.UpdatedAt).ThenByDescending(t => t.Id);
        var page = await ordered
            .Select(t => new TicketListItem(
                t.Id, t.Number, t.Kind, t.Subject, t.Status, t.HoldReason, t.Priority,
                db.Teams.Where(x => x.Id == t.TeamId).Select(x => x.Name).First(),
                db.Users.Where(u => u.Id == t.AssigneeId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                db.Users.Where(u => u.Id == t.RequesterId).Select(u => u.FirstName + NameSeparator + u.LastName).First(),
                t.CreatedAt, t.UpdatedAt, Array.Empty<TicketSla>()))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);

        var sla = await slaReader.LoadAsync([.. page.Items.Select(i => i.Id)], cancellationToken);
        return page with { Items = [.. page.Items.Select(i => i with { Sla = sla.GetValueOrDefault(i.Id) ?? [] })] };
    }
}
