using System.Text.Json.Serialization;
using Proteone.Application.Features.Ratings;
using Proteone.Application.Features.Services;
using Proteone.Application.Sla;
using Proteone.Domain.Tickets;

namespace Proteone.Application.Features.Tickets;

public sealed record TicketListItem(
    Guid Id,
    string Number,
    TicketKind Kind,
    string Subject,
    TicketStatus Status,
    HoldReason? HoldReason,
    Priority Priority,
    string TeamName,
    string? AssigneeName,
    string RequesterName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<TicketSla> Sla);

public sealed record RelatedTicketItem(string Number, TicketKind Kind, string Subject, TicketStatus Status);

public sealed record ApprovalSummary(Guid Id, uint Version, ApprovalStatus Status, Guid? ApproverId, string? ApproverName, string? Reason, DateTimeOffset CreatedAt, DateTimeOffset? DecidedAt);

/// <summary>
/// Ticket detail for every viewer. <see cref="Service"/> is IT-only: it stays null for employees and is then left out of
/// the JSON altogether, so the requester's payload carries no service property (Phase 12).
/// <see cref="CanRate"/> is true only for the requester while the rating window is open; <see cref="RateUntil"/> is the
/// exclusive end of that window, null when the ticket cannot be rated.
/// </summary>
public sealed record TicketDetail(
    Guid Id,
    string Number,
    TicketKind Kind,
    string Subject,
    string Description,
    TicketStatus Status,
    HoldReason? HoldReason,
    string? HoldNote,
    Level Impact,
    Level Urgency,
    Priority Priority,
    Guid? CategoryId,
    string? CategoryName,
    Guid? LocationId,
    string? LocationName,
    Guid RequesterId,
    string RequesterName,
    Guid TeamId,
    string TeamName,
    Guid? AssigneeId,
    string? AssigneeName,
    Guid? RequestTypeId,
    string? RequestTypeName,
    bool RequiresApproval,
    string? RootCause,
    string? Resolution,
    TicketOutcome? Outcome,
    string? CancelReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    ApprovalSummary? Approval,
    IReadOnlyList<RelatedTicketItem> RelatedTickets,
    bool CanWork,
    uint Version,
    IReadOnlyList<TicketSla> Sla,
    int OpenTaskCount,
    IReadOnlyList<TicketTaskItem> Tasks,
    TicketRatingView? Rating,
    bool CanRate,
    DateTimeOffset? RateUntil,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ServiceRef? Service,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? MonitoringSource);

/// <summary>A task opened from the ticket, for IT staff on the ticket detail; the requester never sees tasks (ADR-0031).</summary>
public sealed record TicketTaskItem(string Number, string Title, Proteone.Domain.Tasks.WorkTaskStatus Status, string TeamName, string? AssigneeName);
