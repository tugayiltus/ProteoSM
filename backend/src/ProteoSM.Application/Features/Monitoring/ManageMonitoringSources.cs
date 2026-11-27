using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Monitoring;
using ProteoSM.Domain.Tickets;
using ProteoSM.Domain.Users;

namespace ProteoSM.Application.Features.Monitoring;

/// <summary>The secret is never part of it.</summary>
public sealed record MonitoringSourceItem(
    Guid Id, string Name, Guid TeamId, string TeamName, Guid RequesterId, string RequesterName, Guid? ServiceId, string? ServiceName,
    Guid? CategoryId, string? CategoryName, Level Impact, Level Urgency, bool IsActive, DateTimeOffset SecretSetAt,
    DateTimeOffset? LastEventAt, int FailedCount, DateTimeOffset? LastFailedAt, uint Version);

/// <summary>Answer of a create or a replacement: the only time the secret is readable.</summary>
public sealed record MonitoringSourceSecret(MonitoringSourceItem Source, string Secret)
{
    // A record prints its members; the secret must not reach a log.
    public override string ToString() => nameof(MonitoringSourceSecret);
}

internal sealed class MonitoringSourceDraftValidator : AbstractValidator<MonitoringSourceDraft>
{
    public MonitoringSourceDraftValidator()
    {
        RuleFor(d => d.Name).NotEmpty().MaximumLength(MonitoringLimits.NameMaxLength);
        RuleFor(d => d.TeamId).NotEmpty();
        RuleFor(d => d.RequesterId).NotEmpty();
        RuleFor(d => d.Impact).IsInEnum();
        RuleFor(d => d.Urgency).IsInEnum();
    }
}

internal static class MonitoringSources
{
    public const string NameTakenMessage = "Another monitoring source uses this name.";
    private const string NameSeparator = " ";

    public static string NewSecret() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(MonitoringLimits.SecretBytes));

    public static IQueryable<MonitoringSourceItem> Items(IAppDbContext db, Guid? id = null) =>
        from source in db.MonitoringSources.AsNoTracking()
        where id == null || source.Id == id
        join status in db.MonitoringSourceStatuses on source.Id equals status.SourceId
        join team in db.Teams on source.TeamId equals team.Id
        join user in db.Users on source.RequesterId equals user.Id
        orderby source.NormalizedName, source.Id
        select new MonitoringSourceItem(
            source.Id, source.Name, team.Id, team.Name, user.Id, user.FirstName + NameSeparator + user.LastName,
            source.ServiceId, db.Services.Where(s => s.Id == source.ServiceId).Select(s => s.Name).FirstOrDefault(),
            source.CategoryId, db.Categories.Where(c => c.Id == source.CategoryId).Select(c => c.Name).FirstOrDefault(),
            source.Impact, source.Urgency, source.IsActive, source.SecretSetAt, status.LastEventAt, status.FailedCount, status.LastFailedAt, source.Version);

    public static Task<MonitoringSourceItem> ItemAsync(IAppDbContext db, Guid id, CancellationToken cancellationToken) =>
        Items(db, id).SingleAsync(cancellationToken);

    /// <summary>References of a new or changed source must be active; unchanged ones may have been deactivated since.</summary>
    public static async Task EnsureReferencesAsync(IAppDbContext db, MonitoringSourceDraft draft, MonitoringSource? current, CancellationToken cancellationToken)
    {
        var teamOk = draft.TeamId == current?.TeamId || await db.Teams.AnyAsync(t => t.Id == draft.TeamId && t.IsActive, cancellationToken);
        var serviceOk = draft.ServiceId is not { } service || service == current?.ServiceId
            || await db.Services.AnyAsync(s => s.Id == service && s.IsActive, cancellationToken);
        var categoryOk = draft.CategoryId is not { } category || category == current?.CategoryId
            || (await db.Categories.AsNoTracking().SingleOrDefaultAsync(c => c.Id == category && c.IsActive, cancellationToken))?.AppliesTo(TicketKind.Incident) == true;
        if (!teamOk || !serviceOk || !categoryOk)
        {
            throw new DomainException(ErrorCodes.ReferenceInactive, "A selected team, service or category is inactive or does not apply.");
        }

        if (draft.RequesterId != current?.RequesterId
            && !await db.Users.AnyAsync(u => u.Id == draft.RequesterId && u.IsActive && u.Role != UserRole.Employee, cancellationToken))
        {
            throw new DomainException(ErrorCodes.MonitoringRequesterInvalid, "The requester must be active IT staff or an admin.");
        }
    }

    public static object AuditValues(MonitoringSource s) =>
        new { s.Name, s.TeamId, s.RequesterId, s.ServiceId, s.CategoryId, s.Impact, s.Urgency, s.IsActive };
}

/// <summary>Admin route; the list is short, so it is not paged.</summary>
public sealed class ListMonitoringSourcesHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<MonitoringSourceItem>> HandleAsync(CancellationToken cancellationToken) =>
        await MonitoringSources.Items(db).ToListAsync(cancellationToken);
}

public sealed record CreateMonitoringSourceCommand(MonitoringSourceDraft Draft);

public sealed class CreateMonitoringSourceHandler(
    IAppDbContext db, IValidator<MonitoringSourceDraft> validator, IMonitoringSecretProtector protector, IAuditLog audit, TimeProvider clock)
{
    public async Task<MonitoringSourceSecret> HandleAsync(CreateMonitoringSourceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command.Draft, cancellationToken);
        await MonitoringSources.EnsureReferencesAsync(db, command.Draft, current: null, cancellationToken);
        var secret = MonitoringSources.NewSecret();
        var source = MonitoringSource.Create(command.Draft, protector.Protect(secret), clock.GetUtcNow());
        db.MonitoringSources.Add(source);
        db.MonitoringSourceStatuses.Add(MonitoringSourceStatus.For(source.Id));
        audit.Record(AuditActions.MonitoringSourceCreated, AuditEntityTypes.MonitoringSource, source.Id, newValues: MonitoringSources.AuditValues(source));
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.MonitoringNameTaken, MonitoringSources.NameTakenMessage, cancellationToken);
        return new MonitoringSourceSecret(await MonitoringSources.ItemAsync(db, source.Id, cancellationToken), secret);
    }
}

public sealed record UpdateMonitoringSourceCommand(Guid Id, uint Version, MonitoringSourceDraft Draft);

public sealed class UpdateMonitoringSourceHandler(IAppDbContext db, IValidator<MonitoringSourceDraft> validator, IAuditLog audit, TimeProvider clock)
{
    public async Task<MonitoringSourceItem> HandleAsync(UpdateMonitoringSourceCommand command, CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command.Draft, cancellationToken);
        var source = await db.MonitoringSources.SingleOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.MonitoringSource);
        db.SetExpectedVersion(source, command.Version);
        await MonitoringSources.EnsureReferencesAsync(db, command.Draft, source, cancellationToken);
        var before = MonitoringSources.AuditValues(source);
        source.Update(command.Draft, clock.GetUtcNow());
        audit.Record(AuditActions.MonitoringSourceUpdated, AuditEntityTypes.MonitoringSource, source.Id, before, MonitoringSources.AuditValues(source));
        await db.SaveTranslatingDuplicateAsync(ErrorCodes.MonitoringNameTaken, MonitoringSources.NameTakenMessage, cancellationToken);
        return await MonitoringSources.ItemAsync(db, source.Id, cancellationToken);
    }
}

public sealed record ReplaceMonitoringSecretCommand(Guid Id, uint Version);

/// <summary>The former secret stops with this save.</summary>
public sealed class ReplaceMonitoringSecretHandler(IAppDbContext db, IMonitoringSecretProtector protector, IAuditLog audit, TimeProvider clock)
{
    public async Task<MonitoringSourceSecret> HandleAsync(ReplaceMonitoringSecretCommand command, CancellationToken cancellationToken)
    {
        var source = await db.MonitoringSources.SingleOrDefaultAsync(s => s.Id == command.Id, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.MonitoringSource);
        db.SetExpectedVersion(source, command.Version);
        var secret = MonitoringSources.NewSecret();
        source.ReplaceSecret(protector.Protect(secret), clock.GetUtcNow());
        audit.Record(AuditActions.MonitoringSecretReplaced, AuditEntityTypes.MonitoringSource, source.Id);
        await db.SaveChangesAsync(cancellationToken);
        return new MonitoringSourceSecret(await MonitoringSources.ItemAsync(db, source.Id, cancellationToken), secret);
    }
}
