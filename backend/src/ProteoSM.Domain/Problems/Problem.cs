using ProteoSM.Domain.Common;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Domain.Problems;

/// <summary>
/// The shared cause behind recurring incidents (ADR-0034). States: open → investigating → known_error →
/// resolved | cancelled. Known error needs a root cause and a workaround; closed problems never reopen.
/// </summary>
public sealed class Problem : Entity
{
    public const int TitleMaxLength = 200;
    public const int TextMaxLength = 4000;
    public const int ReasonMaxLength = 500;

    private Problem()
    {
    }

    public string Number { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;

    /// <summary>Turkish-insensitive search key of the title (<see cref="TurkishText.ToSearchKey"/>).</summary>
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>The symptom.</summary>
    public string Description { get; private set; } = string.Empty;
    public Level Impact { get; private set; }
    public Guid OwnerTeamId { get; private set; }
    public Guid? OwnerId { get; private set; }
    public ProblemStatus Status { get; private set; }
    public string? RootCause { get; private set; }
    public string? Workaround { get; private set; }
    public string? Resolution { get; private set; }
    public string? CancelReason { get; private set; }
    public Guid? ArticleId { get; private set; }
    public Guid CreatedById { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? KnownErrorAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>The IT service this record belongs to; any state may set or clear it (Phase 12).</summary>
    public Guid? ServiceId { get; private set; }

    public bool IsClosed => Status is ProblemStatus.Resolved or ProblemStatus.Cancelled;

    public static Problem Create(
        string number, string title, string description, Level impact, Guid ownerTeamId, Guid? ownerId, Guid createdById, DateTimeOffset now) =>
        new()
        {
            Number = number,
            Title = title.Trim(),
            SearchText = TurkishText.ToSearchKey(title),
            Description = description.Trim(),
            Impact = impact,
            OwnerTeamId = ownerTeamId,
            OwnerId = ownerId,
            Status = ProblemStatus.Open,
            CreatedById = createdById,
            CreatedAt = now,
            UpdatedAt = now,
        };

    public void Edit(string title, string description, Level impact, string? rootCause, string? workaround, DateTimeOffset now)
    {
        RequireNotClosed();
        var newRootCause = Clean(rootCause);
        var newWorkaround = Clean(workaround);
        if (Status == ProblemStatus.KnownError && (newRootCause is null || newWorkaround is null))
        {
            throw KnownErrorIncomplete();
        }

        Title = title.Trim();
        SearchText = TurkishText.ToSearchKey(title);
        Description = description.Trim();
        Impact = impact;
        RootCause = newRootCause;
        Workaround = newWorkaround;
        Touch(now);
    }

    public void Start(DateTimeOffset now)
    {
        Require([ProblemStatus.Open], nameof(Start));
        Status = ProblemStatus.Investigating;
        Touch(now);
    }

    public void MarkKnownError(DateTimeOffset now)
    {
        Require([ProblemStatus.Open, ProblemStatus.Investigating], nameof(MarkKnownError));
        if (string.IsNullOrWhiteSpace(RootCause) || string.IsNullOrWhiteSpace(Workaround))
        {
            throw KnownErrorIncomplete();
        }

        Status = ProblemStatus.KnownError;
        KnownErrorAt = now;
        Touch(now);
    }

    public void Resolve(string resolution, DateTimeOffset now)
    {
        Require([ProblemStatus.Open, ProblemStatus.Investigating, ProblemStatus.KnownError], nameof(Resolve));
        if (string.IsNullOrWhiteSpace(resolution))
        {
            throw new DomainException(ErrorCodes.ProblemResolutionRequired, "A resolution is required.");
        }

        Status = ProblemStatus.Resolved;
        Resolution = resolution.Trim();
        ResolvedAt = now;
        Close(now);
    }

    public void Cancel(string reason, DateTimeOffset now)
    {
        Require([ProblemStatus.Open, ProblemStatus.Investigating, ProblemStatus.KnownError], nameof(Cancel));
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(ErrorCodes.ProblemCancelReasonRequired, "A cancel reason is required.");
        }

        Status = ProblemStatus.Cancelled;
        CancelReason = reason.Trim();
        Close(now);
    }

    /// <summary>Moves the problem to another team; the new team picks its own owner, so the owner is cleared.</summary>
    public void Transfer(Guid teamId, DateTimeOffset now)
    {
        RequireNotClosed();
        OwnerTeamId = teamId;
        OwnerId = null;
        Touch(now);
    }

    /// <summary>Sets or clears the owner; membership of the owner team is checked by the caller.</summary>
    public void SetOwner(Guid? ownerId, DateTimeOffset now)
    {
        RequireNotClosed();
        OwnerId = ownerId;
        Touch(now);
    }

    /// <summary>Links the known-error article, once, while the problem is a known error or resolved.</summary>
    public void AttachArticle(Guid articleId, DateTimeOffset now)
    {
        if (ArticleId is not null || Status is not (ProblemStatus.KnownError or ProblemStatus.Resolved))
        {
            throw new DomainException(ErrorCodes.ProblemArticleNotAllowed, "An article needs a known error or resolved problem without one.");
        }

        ArticleId = articleId;
        Touch(now);
    }

    public void Touch(DateTimeOffset now) => UpdatedAt = now;

    /// <summary>Sets or clears the service in any state; touches <c>UpdatedAt</c> only when the value changes.</summary>
    public bool SetService(Guid? serviceId, DateTimeOffset now)
    {
        if (ServiceId == serviceId)
        {
            return false;
        }

        ServiceId = serviceId;
        Touch(now);
        return true;
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static DomainException KnownErrorIncomplete() =>
        new(ErrorCodes.ProblemKnownErrorIncomplete, "A known error needs a root cause and a workaround.");

    private void Close(DateTimeOffset now)
    {
        ClosedAt = now;
        Touch(now);
    }

    private void RequireNotClosed()
    {
        if (IsClosed)
        {
            throw new DomainException(ErrorCodes.ProblemClosed, "A closed problem cannot be changed.");
        }
    }

    private void Require(ProblemStatus[] allowed, string operation)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException(ErrorCodes.ProblemInvalidTransition, $"'{operation}' is not allowed while the problem is {Status}.");
        }
    }
}
