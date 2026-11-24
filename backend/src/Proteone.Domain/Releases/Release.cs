using Proteone.Domain.Changes;
using Proteone.Domain.Common;

namespace Proteone.Domain.Releases;

/// <summary>What an editor enters for a release.</summary>
public sealed record ReleaseDraft(
    string Title, string Description, Guid OwnerTeamId, Guid? OwnerId, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, string? Notes);

/// <summary>
/// Changes that go live together (ADR-0040): planned → in_progress → completed | cancelled, no approval. A release never
/// touches the plan, approval or state of its changes; the change lifecycle stays as it is (ADR-0032).
/// </summary>
public sealed class Release : Entity
{
    public const int TitleMaxLength = 200;
    public const int TextMaxLength = 4000;
    public const int ReasonMaxLength = 500;

    private Release()
    {
    }

    public string Number { get; private init; } = string.Empty;
    public string Title { get; private set; } = string.Empty;

    /// <summary>Turkish-insensitive search key of the title (<see cref="TurkishText.ToSearchKey"/>).</summary>
    public string SearchText { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;
    public Guid OwnerTeamId { get; private set; }
    public Guid? OwnerId { get; private set; }
    public DateTimeOffset? WindowStart { get; private set; }
    public DateTimeOffset? WindowEnd { get; private set; }
    public string? Notes { get; private set; }
    public ReleaseStatus Status { get; private set; }
    public string? CancelReason { get; private set; }
    public Guid CreatedById { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public bool IsOpen => Status is ReleaseStatus.Planned or ReleaseStatus.InProgress;

    public static Release Create(string number, ReleaseDraft draft, Guid createdById, DateTimeOffset now)
    {
        var release = new Release { Number = number, CreatedById = createdById, CreatedAt = now };
        release.Apply(draft, now);
        return release;
    }

    public void Edit(ReleaseDraft draft, DateTimeOffset now)
    {
        RequireOpen();
        Apply(draft, now);
    }

    public void Start(DateTimeOffset now)
    {
        if (Status != ReleaseStatus.Planned)
        {
            throw InvalidTransition();
        }

        Status = ReleaseStatus.InProgress;
        StartedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Needs at least one change, and every change closed or cancelled.</summary>
    public void Complete(IReadOnlyCollection<ChangeStatus> changes, DateTimeOffset now)
    {
        if (Status != ReleaseStatus.InProgress)
        {
            throw InvalidTransition();
        }

        if (changes.Count == 0 || changes.Any(s => s is not (ChangeStatus.Closed or ChangeStatus.Cancelled)))
        {
            throw new DomainException(ErrorCodes.ReleaseOpenChanges, "A release needs changes, all of them closed or cancelled.");
        }

        Status = ReleaseStatus.Completed;
        ClosedAt = now;
        UpdatedAt = now;
    }

    /// <summary>The caller releases the changes in the same save.</summary>
    public void Cancel(string reason, DateTimeOffset now)
    {
        RequireOpen();
        Status = ReleaseStatus.Cancelled;
        CancelReason = reason.Trim();
        ClosedAt = now;
        UpdatedAt = now;
    }

    public void RequireOpen()
    {
        if (!IsOpen)
        {
            throw new DomainException(ErrorCodes.ReleaseClosed, "A completed or cancelled release cannot be changed.");
        }
    }

    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    private void Apply(ReleaseDraft draft, DateTimeOffset now)
    {
        Title = draft.Title.Trim();
        SearchText = TurkishText.ToSearchKey(Title);
        Description = draft.Description.Trim();
        OwnerTeamId = draft.OwnerTeamId;
        OwnerId = draft.OwnerId;
        WindowStart = draft.WindowStart;
        WindowEnd = draft.WindowEnd;
        Notes = string.IsNullOrWhiteSpace(draft.Notes) ? null : draft.Notes.Trim();
        UpdatedAt = now;
    }

    private static DomainException InvalidTransition() =>
        new(ErrorCodes.ReleaseInvalidTransition, "The action is not allowed in the current state of the release.");
}
