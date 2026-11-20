namespace Tgysm.Domain.Problems;

/// <summary>Problem ↔ change link: the change fixes or works around the problem (ADR-0034).</summary>
public sealed class ProblemChangeLink
{
    private ProblemChangeLink()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid ProblemId { get; private init; }
    public Guid ChangeId { get; private init; }
    public Guid CreatedById { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    public static ProblemChangeLink Create(Guid problemId, Guid changeId, Guid createdById, DateTimeOffset now) =>
        new() { ProblemId = problemId, ChangeId = changeId, CreatedById = createdById, CreatedAt = now };
}
