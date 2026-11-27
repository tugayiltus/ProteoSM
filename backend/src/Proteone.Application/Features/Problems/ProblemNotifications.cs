using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Domain.Notifications;
using Proteone.Domain.Problems;

namespace Proteone.Application.Features.Problems;

/// <summary>
/// Outbox events of problem actions. Written in the action's own save; who receives them, and whether a linked
/// incident is still open, is decided at dispatch time (<c>RecipientResolver</c>).
/// </summary>
internal static class ProblemNotifications
{
    public static void Created(INotificationOutbox outbox, Problem problem)
    {
        // The owner given at creation is the event's user: problem_created skips them, problem_assigned tells them.
        outbox.AddForSubject(NotificationEventTypes.ProblemCreated, OutboxSubjectTypes.Problem, problem.Id, problem.OwnerId);
        if (problem.OwnerId is { } owner)
        {
            Assigned(outbox, problem, owner);
        }
    }

    /// <summary>A new owner is told alone (<paramref name="ownerId"/>); without one the new team's members are.</summary>
    public static void Assigned(INotificationOutbox outbox, Problem problem, Guid? ownerId) =>
        outbox.AddForSubject(NotificationEventTypes.ProblemAssigned, OutboxSubjectTypes.Problem, problem.Id, ownerId);

    /// <summary>
    /// The problem event plus one event per linked incident for its requester. The requester event carries the ticket
    /// only, so nothing of the problem reaches an employee (ADR-0034).
    /// </summary>
    public static async Task StatusChangedAsync(
        IAppDbContext db, INotificationOutbox outbox, Problem problem, string problemEvent, string ticketEvent, CancellationToken cancellationToken)
    {
        outbox.AddForSubject(problemEvent, OutboxSubjectTypes.Problem, problem.Id, userId: null);
        var ticketIds = await db.ProblemTicketLinks.AsNoTracking()
            .Where(l => l.ProblemId == problem.Id)
            .Select(l => l.TicketId)
            .ToListAsync(cancellationToken);
        foreach (var ticketId in ticketIds)
        {
            outbox.Add(ticketEvent, ticketId, userId: null);
        }
    }
}
