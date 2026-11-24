using Proteone.Domain.Common;

namespace Proteone.Domain.Releases;

/// <summary>Append-only release history row. <see cref="DataJson"/> holds kind-specific details (no secrets).</summary>
public sealed class ReleaseEvent
{
    private ReleaseEvent()
    {
    }

    // Sequential: the history orders by time, then id, and several events share one instant.
    public Guid Id { get; private init; } = SequentialId.Next();
    public Guid ReleaseId { get; private init; }
    public ReleaseEventKind Kind { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public string DataJson { get; private init; } = "{}";
    public DateTimeOffset OccurredAt { get; private init; }

    public static ReleaseEvent Record(Guid releaseId, ReleaseEventKind kind, Guid? actorUserId, string dataJson, DateTimeOffset now) =>
        new() { ReleaseId = releaseId, Kind = kind, ActorUserId = actorUserId, DataJson = dataJson, OccurredAt = now };
}
