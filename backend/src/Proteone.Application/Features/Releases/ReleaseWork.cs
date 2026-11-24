using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Application.Features.Changes;
using Proteone.Domain.Audit;
using Proteone.Domain.Releases;

namespace Proteone.Application.Features.Releases;

/// <summary>
/// Release access and the frame of release writes (ADR-0040). Every active IT staff member and admin reads every release;
/// employees get not found. A write needs an admin or a member of the owner team and runs under the change lock, so no
/// change is closed, cancelled or moved while a release is completed or cancelled.
/// </summary>
public sealed class ReleaseWork(IAppDbContext db, ChangeScope changes, TimeProvider clock)
{
    private const string NameSeparator = " ";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static bool CanWork(ChangeViewer viewer, Release release) =>
        viewer.IsAdmin || (viewer.IsStaff && viewer.TeamIds.Contains(release.OwnerTeamId));

    public async Task<ChangeViewer> StaffViewerAsync(CancellationToken cancellationToken)
    {
        var viewer = await changes.ViewerAsync(cancellationToken);
        return viewer.IsStaff ? viewer : throw new NotFoundException(AuditEntityTypes.Release);
    }

    public async Task<(Release Release, ChangeViewer Viewer)> LoadAsync(string number, CancellationToken cancellationToken)
    {
        var viewer = await StaffViewerAsync(cancellationToken);
        var normalized = ReleaseNumber.Normalize(number);
        var release = await db.Releases.SingleOrDefaultAsync(r => r.Number == normalized, cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Release);
        return (release, viewer);
    }

    public async Task<ReleaseDetail> RunAsync(
        string number, uint? version, Func<Release, ChangeViewer, DateTimeOffset, CancellationToken, Task> change, CancellationToken cancellationToken)
    {
        ReleaseDetail? detail = null;
        await db.RunSerializedAsync(SerializationLocks.ChangeWork, async ct =>
        {
            var (release, viewer) = await LoadAsync(number, ct);
            if (!CanWork(viewer, release))
            {
                throw new ForbiddenException();
            }

            if (version is { } expected)
            {
                db.SetExpectedVersion(release, expected);
            }

            await change(release, viewer, clock.GetUtcNow(), ct);
            await db.SaveChangesAsync(ct);
            detail = await DetailAsync(release.Id, viewer, ct);
        }, cancellationToken);
        return detail!;
    }

    public void Record(Release release, ReleaseEventKind kind, Guid actorId, DateTimeOffset now, object? data = null) =>
        db.ReleaseEvents.Add(ReleaseEvent.Record(release.Id, kind, actorId, JsonSerializer.Serialize(data ?? new { }, Json), now));

    public async Task<ReleaseDetail> DetailAsync(Guid releaseId, ChangeViewer viewer, CancellationToken cancellationToken)
    {
        var r = await db.Releases.AsNoTracking().SingleAsync(x => x.Id == releaseId, cancellationToken);
        var team = await db.Teams.Where(x => x.Id == r.OwnerTeamId).Select(x => x.Name).SingleAsync(cancellationToken);
        var owner = await db.Users.Where(u => u.Id == r.OwnerId).Select(u => u.FirstName + NameSeparator + u.LastName).SingleOrDefaultAsync(cancellationToken);
        var visible = changes.Visible(viewer);
        var rows = await db.ChangeRequests.AsNoTracking().Where(c => c.ReleaseId == releaseId).OrderBy(c => c.Number)
            .Select(c => new ReleaseChangeItem(
                c.Number, c.Title, c.Status, db.Teams.Where(t => t.Id == c.OwnerTeamId).Select(t => t.Name).First(), visible.Any(v => v.Id == c.Id)))
            .ToListAsync(cancellationToken);
        return new ReleaseDetail(
            r.Id, r.Number, r.Title, r.Description, r.Status, r.OwnerTeamId, team, r.OwnerId, owner, r.WindowStart, r.WindowEnd, r.Notes,
            r.CancelReason, r.CreatedAt, r.StartedAt, r.ClosedAt, rows, CanWork(viewer, r), r.Version);
    }
}
