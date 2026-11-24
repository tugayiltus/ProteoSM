using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Application.Features.Tickets;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Problems;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Features.Problems;

/// <summary>
/// Incident link rules shared by "Probleme bağla", the problem's "Arıza bağla" and "Problem oluştur" from an incident
/// (ADR-0034). Callers run inside <see cref="SerializationLocks.ProblemLink"/>.
/// </summary>
internal static class ProblemLinkRules
{
    public const string AlreadyLinkedMessage = "The incident is already linked to a problem.";

    /// <summary>The ticket as the problem viewer sees it; outside their ticket scope it is not found.</summary>
    public static async Task<(Ticket Ticket, TicketViewer Viewer)> FindVisibleTicketAsync(
        TicketScope tickets, ProblemViewer viewer, string ticketNumber, CancellationToken cancellationToken)
    {
        var ticketViewer = new TicketViewer(viewer.UserId, viewer.Role, viewer.TeamIds);
        var normalized = TicketNumber.Normalize(ticketNumber);
        var ticket = await tickets.Visible(ticketViewer).SingleOrDefaultAsync(t => t.Number == normalized, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Ticket);
        return (ticket, ticketViewer);
    }

    /// <summary>
    /// Links and unlinks are for those who may work the incident (its team or admin, the <c>TicketWork</c> rule) and for
    /// the problem's owner team; others get forbidden.
    /// </summary>
    public static void EnsureMayLink(TicketViewer ticketViewer, Ticket ticket, ProblemViewer viewer, Problem problem)
    {
        if (!ticketViewer.CanWork(ticket) && !viewer.CanWork(problem))
        {
            throw new ForbiddenException();
        }
    }

    /// <summary>A resolved or cancelled problem takes no new links.</summary>
    public static void EnsureOpen(Problem problem)
    {
        if (problem.IsClosed)
        {
            throw new DomainException(ErrorCodes.ProblemClosed, "A closed problem takes no new links.");
        }
    }

    /// <summary>
    /// Adds the incident link with its history, ticket event (internal) and audit on both sides. The caller saves with
    /// <see cref="SaveChangesExtensions.SaveTranslatingDuplicateAsync"/> and then touches the ticket.
    /// </summary>
    public static async Task AddIncidentAsync(
        IAppDbContext db, IAuditLog audit, Problem problem, Ticket ticket, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        EnsureOpen(problem);
        var link = ProblemTicketLink.Create(problem.Id, ticket.Id, ticket.Kind, actorId, now);
        if (await db.ProblemTicketLinks.AnyAsync(l => l.TicketId == ticket.Id, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ProblemTicketAlreadyLinked, AlreadyLinkedMessage);
        }

        db.ProblemTicketLinks.Add(link);
        var data = new { ticketNumber = ticket.Number, problemNumber = problem.Number };
        ProblemHistory.Record(db, problem, ProblemEventKind.TicketLinked, actorId, now, data);
        TicketTimeline.Record(db, ticket, TicketEventKind.ProblemLinked, Visibility.Internal, actorId, now, data);
        audit.Record(AuditActions.ProblemTicketLinked, AuditEntityTypes.Problem, problem.Id, newValues: data);
        audit.Record(AuditActions.TicketProblemLinked, AuditEntityTypes.Ticket, ticket.Id, newValues: data);
    }
}
