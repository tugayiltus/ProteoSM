using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Announcements;
using Proteone.Domain.Audit;

namespace Proteone.Application.Features.Announcements;

/// <summary>What every signed-in user sees; no author and no version.</summary>
public sealed record ActiveAnnouncement(Guid Id, string Title, string Body, AnnouncementLevel Level, DateTimeOffset StartsAt, DateTimeOffset? EndsAt);

public sealed record AnnouncementItem(
    Guid Id, string Title, string Body, AnnouncementLevel Level, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, uint Version);

internal sealed class AnnouncementDraftValidator : AbstractValidator<AnnouncementDraft>
{
    public AnnouncementDraftValidator()
    {
        RuleFor(d => d.Title).NotEmpty().MaximumLength(Announcement.TitleMaxLength);
        RuleFor(d => d.Body).NotEmpty().MaximumLength(Announcement.BodyMaxLength);
        RuleFor(d => d.Level).IsInEnum();
    }
}

public sealed class ListActiveAnnouncementsHandler(IAppDbContext db, TimeProvider clock)
{
    /// <summary>More than a handful at once is noise; the newest win.</summary>
    public const int MaxShown = 5;

    public async Task<IReadOnlyList<ActiveAnnouncement>> HandleAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        return await db.Announcements.AsNoTracking()
            .Where(a => a.StartsAt <= now && (a.EndsAt == null || a.EndsAt > now))
            .OrderByDescending(a => a.StartsAt).ThenBy(a => a.Id)
            .Take(MaxShown)
            .Select(a => new ActiveAnnouncement(a.Id, a.Title, a.Body, a.Level, a.StartsAt, a.EndsAt))
            .ToListAsync(cancellationToken);
    }
}

public sealed class ListAnnouncementsHandler(IAppDbContext db)
{
    /// <summary>Enough for one company's recent messages on one page.</summary>
    public const int MaxListed = 100;

    public async Task<IReadOnlyList<AnnouncementItem>> HandleAsync(CancellationToken cancellationToken) =>
        await db.Announcements.AsNoTracking()
            .OrderByDescending(a => a.StartsAt).ThenBy(a => a.Id)
            .Take(MaxListed)
            .Select(a => new AnnouncementItem(a.Id, a.Title, a.Body, a.Level, a.StartsAt, a.EndsAt, a.Version))
            .ToListAsync(cancellationToken);
}

internal static class AnnouncementMapping
{
    public static AnnouncementItem ToItem(Announcement a) => new(a.Id, a.Title, a.Body, a.Level, a.StartsAt, a.EndsAt, a.Version);

    public static object AuditValues(Announcement a) => new { a.Title, a.Body, a.Level, a.StartsAt, a.EndsAt };
}

public sealed record CreateAnnouncementCommand(AnnouncementDraft Draft);

public sealed class CreateAnnouncementHandler(IAppDbContext db, ICurrentActor actor, IValidator<AnnouncementDraft> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<AnnouncementItem> HandleAsync(CreateAnnouncementCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command.Draft, cancellationToken);
        var announcement = Announcement.Create(command.Draft, actor.UserId ?? throw new AuthenticationFailedException(), clock.GetUtcNow());
        db.Announcements.Add(announcement);
        audit.Record(AuditActions.AnnouncementCreated, AuditEntityTypes.Announcement, announcement.Id, newValues: AnnouncementMapping.AuditValues(announcement));
        await db.SaveChangesAsync(cancellationToken);
        return AnnouncementMapping.ToItem(announcement);
    }
}

public sealed record UpdateAnnouncementCommand(Guid Id, uint Version, AnnouncementDraft Draft);

public sealed class UpdateAnnouncementHandler(IAppDbContext db, IValidator<AnnouncementDraft> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<AnnouncementItem> HandleAsync(UpdateAnnouncementCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command.Draft, cancellationToken);
        var announcement = await db.Announcements.SingleOrDefaultAsync(a => a.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Announcement);
        db.SetExpectedVersion(announcement, command.Version);
        var before = AnnouncementMapping.AuditValues(announcement);
        announcement.Update(command.Draft, clock.GetUtcNow());
        audit.Record(AuditActions.AnnouncementUpdated, AuditEntityTypes.Announcement, announcement.Id, before, AnnouncementMapping.AuditValues(announcement));
        await db.SaveChangesAsync(cancellationToken);
        return AnnouncementMapping.ToItem(announcement);
    }
}
