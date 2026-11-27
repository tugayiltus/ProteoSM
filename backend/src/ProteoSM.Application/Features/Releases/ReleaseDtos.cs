using System.Text.Json;
using ProteoSM.Domain.Changes;
using ProteoSM.Domain.Releases;

namespace ProteoSM.Application.Features.Releases;

public sealed record ReleaseRef(string Number, string Title, ReleaseStatus Status);

public sealed record ReleaseListItem(
    Guid Id, string Number, string Title, ReleaseStatus Status, string OwnerTeamName, string? OwnerName, int ChangeCount,
    DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, DateTimeOffset UpdatedAt);

/// <summary>A change of the release. A row the viewer cannot open shows number, title, status and team only (ADR-0033).</summary>
public sealed record ReleaseChangeItem(string Number, string Title, ChangeStatus Status, string OwnerTeamName, bool CanOpen);

public sealed record ReleaseDetail(
    Guid Id, string Number, string Title, string Description, ReleaseStatus Status, Guid OwnerTeamId, string OwnerTeamName,
    Guid? OwnerId, string? OwnerName, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, string? Notes, string? CancelReason,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? ClosedAt, IReadOnlyList<ReleaseChangeItem> Changes,
    bool CanWork, uint Version);

public sealed record ReleaseTimelineItem(Guid Id, ReleaseEventKind Kind, string? ActorName, JsonElement Data, DateTimeOffset OccurredAt);
