using ProteoSM.Application.Features.Services;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Sla;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Features.Problems;

/// <summary>
/// The known-error timer of a problem. Same JSON shape as one entry of a ticket <c>sla</c> array, so the ticket SLA
/// badge renders it. <c>RemainingSeconds</c> is null once stopped, negative when overdue; the timer never pauses.
/// </summary>
public sealed record ProblemSlaDto(
    string Metric,
    DateTimeOffset DueAt,
    long? RemainingSeconds,
    bool IsPaused,
    bool IsWarned,
    bool IsBreached,
    SlaResult? Result)
{
    public const string KnownErrorMetric = "known_error";
}

public sealed record ProblemListItem(
    Guid Id,
    string Number,
    string Title,
    ProblemStatus Status,
    Level Impact,
    string OwnerTeamName,
    string? OwnerName,
    int IncidentCount,
    DateTimeOffset UpdatedAt,
    ProblemSlaDto? Sla = null);

public sealed record ProblemDetail(
    Guid Id,
    string Number,
    string Title,
    string Description,
    Level Impact,
    ProblemStatus Status,
    Guid OwnerTeamId,
    string OwnerTeamName,
    Guid? OwnerId,
    string? OwnerName,
    string? RootCause,
    string? Workaround,
    string? Resolution,
    string? CancelReason,
    Guid? ArticleId,
    string? ArticleTitle,
    DateTimeOffset CreatedAt,
    DateTimeOffset? KnownErrorAt,
    DateTimeOffset? ResolvedAt,
    bool CanWork,
    uint Version,
    ServiceRef? Service,
    ProblemSlaDto? Sla = null);
