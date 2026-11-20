using Tgysm.Domain.Problems;
using Tgysm.Domain.Tickets;

namespace Tgysm.Application.Features.Problems;

public sealed record ProblemListItem(
    Guid Id,
    string Number,
    string Title,
    ProblemStatus Status,
    Level Impact,
    string OwnerTeamName,
    string? OwnerName,
    int IncidentCount,
    DateTimeOffset UpdatedAt);

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
    uint Version);
