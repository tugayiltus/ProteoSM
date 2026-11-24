using Proteone.Domain.Changes;
using Proteone.Domain.Problems;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Features.Services;

/// <summary>What the impact view calls open; the list filter <c>open</c> uses the same sets, so both show the same records.</summary>
internal static class ImpactStatuses
{
    public static readonly TicketStatus[] ClosedTickets = [TicketStatus.Completed, TicketStatus.Cancelled];

    public static readonly ChangeStatus[] OpenChanges = [ChangeStatus.PendingApproval, ChangeStatus.Scheduled, ChangeStatus.Implementing, ChangeStatus.Verifying];

    public static readonly ProblemStatus[] ClosedProblems = [ProblemStatus.Resolved, ProblemStatus.Cancelled];
}
