using ProteoSM.Domain.Changes;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Features.Services;

/// <summary>What the impact view calls open; the list filter <c>open</c> uses the same sets, so both show the same records.</summary>
internal static class ImpactStatuses
{
    public static readonly TicketStatus[] ClosedTickets = [TicketStatus.Completed, TicketStatus.Cancelled];

    public static readonly ChangeStatus[] OpenChanges = [ChangeStatus.PendingApproval, ChangeStatus.Scheduled, ChangeStatus.Implementing, ChangeStatus.Verifying];

    public static readonly ProblemStatus[] ClosedProblems = [ProblemStatus.Resolved, ProblemStatus.Cancelled];
}
