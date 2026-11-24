using Proteone.Domain.Common;

namespace Proteone.Domain.Problems;

/// <summary>Append-only problem history row. <see cref="DataJson"/> holds kind-specific details (no secrets).</summary>
public sealed class ProblemEvent
{
    private ProblemEvent()
    {
    }

    // Sequential: the history orders by time, then id, and several events share one instant.
    public Guid Id { get; private init; } = SequentialId.Next();
    public Guid ProblemId { get; private init; }
    public ProblemEventKind Kind { get; private init; }
    public Guid? ActorUserId { get; private init; }
    public string DataJson { get; private init; } = "{}";
    public DateTimeOffset OccurredAt { get; private init; }

    public static ProblemEvent Record(Guid problemId, ProblemEventKind kind, Guid? actorUserId, string dataJson, DateTimeOffset now) =>
        new() { ProblemId = problemId, Kind = kind, ActorUserId = actorUserId, DataJson = dataJson, OccurredAt = now };
}
