using FluentValidation;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Application.Features.Changes;
using ProteoSM.Application.Features.Tasks;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Changes;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Releases;
using ProteoSM.Domain.Users;

namespace ProteoSM.Application.Features.Releases;

internal sealed class ReleaseDraftValidator : AbstractValidator<ReleaseDraft>
{
    public ReleaseDraftValidator()
    {
        RuleFor(d => d.Title).NotEmpty().MaximumLength(Release.TitleMaxLength);
        RuleFor(d => d.Description).NotEmpty().MaximumLength(Release.TextMaxLength);
        RuleFor(d => d.OwnerTeamId).NotEmpty();
        RuleFor(d => d.Notes).MaximumLength(Release.TextMaxLength);
        RuleFor(d => d.WindowEnd).GreaterThan(d => d.WindowStart).When(d => d.WindowStart is not null && d.WindowEnd is not null);
    }
}

internal static class ReleaseRules
{
    /// <summary>The owner team must be one of the editor's teams (admins: any active team), the owner an active member of it.</summary>
    public static async Task EnsureOwnerAsync(IAppDbContext db, ChangeViewer viewer, Guid teamId, Guid? ownerId, CancellationToken cancellationToken)
    {
        if (!viewer.IsAdmin && !viewer.TeamIds.Contains(teamId))
        {
            throw new DomainException(ErrorCodes.ReleaseOwnerTeamNotAllowed, "The owner team must be one of your teams.");
        }

        await TaskRules.EnsureActiveTeamAsync(db, teamId, cancellationToken);
        if (ownerId is { } owner && !await (
            from m in db.TeamMemberships
            join u in db.Users on m.UserId equals u.Id
            where m.TeamId == teamId && u.Id == owner && u.IsActive && u.Role != UserRole.Employee
            select m).AnyAsync(cancellationToken))
        {
            throw new DomainException(ErrorCodes.ReleaseOwnerNotInTeam, "The owner is not an active member of the owner team.");
        }
    }
}

public sealed record CreateReleaseCommand(ReleaseDraft Draft);

public sealed class CreateReleaseHandler(
    IAppDbContext db, IValidator<ReleaseDraft> validator, ITicketNumberGenerator numbers, ReleaseWork work, IAuditLog audit,
    ICompanyClock companyClock, TimeProvider clock)
{
    public async Task<ReleaseDetail> HandleAsync(CreateReleaseCommand command, CancellationToken cancellationToken)
    {
        var viewer = await work.StaffViewerAsync(cancellationToken);
        await validator.ValidateAndThrowAsync(command.Draft, cancellationToken);
        await ReleaseRules.EnsureOwnerAsync(db, viewer, command.Draft.OwnerTeamId, command.Draft.OwnerId, cancellationToken);
        Release? release = null;
        await db.RunInTransactionAsync(async ct =>
        {
            var now = clock.GetUtcNow();
            var year = companyClock.YearOf(now);
            release = Release.Create(ReleaseNumber.Format(year, await numbers.NextAsync(ReleaseNumber.CounterKind, year, ct)), command.Draft, viewer.UserId, now);
            db.Releases.Add(release);
            work.Record(release, ReleaseEventKind.Created, viewer.UserId, now);
            audit.Record(AuditActions.ReleaseCreated, AuditEntityTypes.Release, release.Id,
                newValues: new { release.Number, release.Title, release.OwnerTeamId, release.OwnerId });
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return await work.DetailAsync(release!.Id, viewer, cancellationToken);
    }
}

public sealed record UpdateReleaseCommand(string Number, uint Version, ReleaseDraft Draft);

public sealed class UpdateReleaseHandler(IAppDbContext db, IValidator<ReleaseDraft> validator, ReleaseWork work, IAuditLog audit)
{
    public async Task<ReleaseDetail> HandleAsync(UpdateReleaseCommand command, CancellationToken cancellationToken)
    {
        await work.StaffViewerAsync(cancellationToken);
        await validator.ValidateAndThrowAsync(command.Draft, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, async (release, viewer, now, ct) =>
        {
            if (release.OwnerTeamId != command.Draft.OwnerTeamId || release.OwnerId != command.Draft.OwnerId)
            {
                await ReleaseRules.EnsureOwnerAsync(db, viewer, command.Draft.OwnerTeamId, command.Draft.OwnerId, ct);
            }

            var before = Values(release);
            release.Edit(command.Draft, now);
            var after = Values(release);
            if (!before.Equals(after))
            {
                work.Record(release, ReleaseEventKind.Updated, viewer.UserId, now);
                audit.Record(AuditActions.ReleaseUpdated, AuditEntityTypes.Release, release.Id, before, after);
            }
        }, cancellationToken);
    }

    private static object Values(Release r) => new { r.Title, r.Description, r.OwnerTeamId, r.OwnerId, r.WindowStart, r.WindowEnd, r.Notes };
}

public sealed record StartReleaseCommand(string Number, uint Version);

public sealed class StartReleaseHandler(ReleaseWork work, IAuditLog audit)
{
    public Task<ReleaseDetail> HandleAsync(StartReleaseCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, (release, viewer, now, _) =>
        {
            var before = release.Status;
            release.Start(now);
            work.Record(release, ReleaseEventKind.Started, viewer.UserId, now);
            audit.Record(AuditActions.ReleaseStatusChanged, AuditEntityTypes.Release, release.Id, new { Status = before }, new { release.Status });
            return Task.CompletedTask;
        }, cancellationToken);
}

public sealed record CompleteReleaseCommand(string Number, uint Version);

public sealed class CompleteReleaseHandler(IAppDbContext db, ReleaseWork work, IAuditLog audit)
{
    public Task<ReleaseDetail> HandleAsync(CompleteReleaseCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, async (release, viewer, now, ct) =>
        {
            var before = release.Status;
            release.Complete(await db.ChangeRequests.Where(c => c.ReleaseId == release.Id).Select(c => c.Status).ToListAsync(ct), now);
            work.Record(release, ReleaseEventKind.Completed, viewer.UserId, now);
            audit.Record(AuditActions.ReleaseStatusChanged, AuditEntityTypes.Release, release.Id, new { Status = before }, new { release.Status });
        }, cancellationToken);
}

public sealed record CancelReleaseCommand(string Number, uint Version, string Reason);

internal sealed class CancelReleaseValidator : AbstractValidator<CancelReleaseCommand>
{
    public CancelReleaseValidator() => RuleFor(c => c.Reason).NotEmpty().MaximumLength(Release.ReasonMaxLength);
}

/// <summary>Cancels the release and frees its changes in the same save; the history of each change says so.</summary>
public sealed class CancelReleaseHandler(IAppDbContext db, IValidator<CancelReleaseCommand> validator, ReleaseWork work, IAuditLog audit)
{
    public async Task<ReleaseDetail> HandleAsync(CancelReleaseCommand command, CancellationToken cancellationToken)
    {
        await work.StaffViewerAsync(cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, command.Version, async (release, viewer, now, ct) =>
        {
            var before = release.Status;
            release.Cancel(command.Reason, now);
            var members = await db.ChangeRequests.Where(c => c.ReleaseId == release.Id).OrderBy(c => c.Number).ToListAsync(ct);
            foreach (var change in members)
            {
                change.LeaveRelease(now);
                ChangeHistory.Record(db, change, ChangeEventKind.ReleaseCleared, viewer.UserId, now, new { release.Number, release.Title });
            }

            work.Record(release, ReleaseEventKind.Cancelled, viewer.UserId, now, new { reason = release.CancelReason, changes = members.Select(c => c.Number) });
            audit.Record(AuditActions.ReleaseStatusChanged, AuditEntityTypes.Release, release.Id, new { Status = before }, new { release.Status });
        }, cancellationToken);
    }
}

public sealed record AddReleaseChangeCommand(string Number, string ChangeNumber);

internal sealed class AddReleaseChangeValidator : AbstractValidator<AddReleaseChangeCommand>
{
    public AddReleaseChangeValidator() => RuleFor(c => c.ChangeNumber).NotEmpty();
}

/// <summary>Needs the right to work on the release and on the change. The plan, approval and state of the change stay as they are.</summary>
public sealed class AddReleaseChangeHandler(
    IAppDbContext db, IValidator<AddReleaseChangeCommand> validator, ReleaseWork work, ChangeScope changes, IAuditLog audit)
{
    public async Task<ReleaseDetail> HandleAsync(AddReleaseChangeCommand command, CancellationToken cancellationToken)
    {
        await work.StaffViewerAsync(cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        return await work.RunAsync(command.Number, version: null, async (release, viewer, now, ct) =>
        {
            release.RequireOpen();
            var (change, _) = await changes.LoadForWorkAsync(command.ChangeNumber, ct);
            change.AddToRelease(release.Id, now);
            release.Touch(now);
            ChangeHistory.Record(db, change, ChangeEventKind.ReleaseSet, viewer.UserId, now, new { release.Number, release.Title });
            work.Record(release, ReleaseEventKind.ChangeAdded, viewer.UserId, now, new { change.Number, change.Title });
            audit.Record(AuditActions.ReleaseChangeAdded, AuditEntityTypes.Release, release.Id, newValues: new { change.Number });
        }, cancellationToken);
    }
}

public sealed record RemoveReleaseChangeCommand(string Number, string ChangeNumber);

public sealed class RemoveReleaseChangeHandler(IAppDbContext db, ReleaseWork work, IAuditLog audit)
{
    public Task<ReleaseDetail> HandleAsync(RemoveReleaseChangeCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, version: null, async (release, viewer, now, ct) =>
        {
            release.RequireOpen();
            var number = ChangeNumber.Normalize(command.ChangeNumber);
            var change = await db.ChangeRequests.SingleOrDefaultAsync(c => c.Number == number && c.ReleaseId == release.Id, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Change);
            change.LeaveRelease(now);
            release.Touch(now);
            ChangeHistory.Record(db, change, ChangeEventKind.ReleaseCleared, viewer.UserId, now, new { release.Number, release.Title });
            work.Record(release, ReleaseEventKind.ChangeRemoved, viewer.UserId, now, new { change.Number, change.Title });
            audit.Record(AuditActions.ReleaseChangeRemoved, AuditEntityTypes.Release, release.Id, oldValues: new { change.Number });
        }, cancellationToken);
}
