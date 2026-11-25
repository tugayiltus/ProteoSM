using Proteone.Domain.Common;

namespace Proteone.Domain.Announcements;

public enum AnnouncementLevel
{
    Info,
    Warning,
}

/// <summary>What an admin enters for an announcement.</summary>
public sealed record AnnouncementDraft(string Title, string Body, AnnouncementLevel Level, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);

/// <summary>A dated message for every signed-in user (ADR-0042). It is ended, never deleted.</summary>
public sealed class Announcement : Entity
{
    public const int TitleMaxLength = 150;
    public const int BodyMaxLength = 1000;

    private Announcement()
    {
    }

    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public AnnouncementLevel Level { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public DateTimeOffset? EndsAt { get; private set; }
    public Guid CreatedById { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Announcement Create(AnnouncementDraft draft, Guid createdById, DateTimeOffset now)
    {
        var announcement = new Announcement { CreatedById = createdById, CreatedAt = now };
        announcement.Update(draft, now);
        return announcement;
    }

    public void Update(AnnouncementDraft draft, DateTimeOffset now)
    {
        if (draft.EndsAt <= draft.StartsAt)
        {
            throw new DomainException(ErrorCodes.AnnouncementInvalidPeriod, "The end must be after the start.");
        }

        Title = draft.Title.Trim();
        Body = draft.Body.Trim();
        Level = draft.Level;
        StartsAt = draft.StartsAt.ToUniversalTime();
        EndsAt = draft.EndsAt?.ToUniversalTime();
        UpdatedAt = now;
    }
}
