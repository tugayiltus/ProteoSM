using Proteone.Domain.Common;
using Proteone.Domain.Tickets;

namespace Proteone.Domain.Problems;

/// <summary>Problem ↔ incident link. An incident belongs to at most one problem; requests cannot be linked (ADR-0034).</summary>
public sealed class ProblemTicketLink
{
    private ProblemTicketLink()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid ProblemId { get; private init; }
    public Guid TicketId { get; private init; }
    public Guid CreatedById { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public static ProblemTicketLink Create(Guid problemId, Guid ticketId, TicketKind ticketKind, Guid createdById, DateTimeOffset now)
    {
        if (ticketKind != TicketKind.Incident)
        {
            throw new DomainException(ErrorCodes.ProblemTicketNotIncident, "Only an incident can be linked to a problem.");
        }

        return new ProblemTicketLink { ProblemId = problemId, TicketId = ticketId, CreatedById = createdById, CreatedAt = now };
    }
}
