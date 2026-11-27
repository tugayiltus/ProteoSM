using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Application.Common.Paging;
using ProteoSM.Domain.Assets;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Licences;

namespace ProteoSM.Application.Features.Licences;

/// <summary>List filter: licences without a free seat, that end within the notice period, or that have ended.</summary>
public enum LicenceState
{
    Full,
    Expiring,
    Expired,
}

public sealed record ListLicencesQuery(Guid? SoftwareId, Guid? TeamId, LicenceKind? Kind, LicenceState? State, string? Search, int Page, int PageSize);

public sealed class ListLicencesHandler(IAppDbContext db, ICurrentActor actor, ICompanyClock companyClock, TimeProvider clock)
{
    public async Task<PagedResult<LicenceListItem>> HandleAsync(ListLicencesQuery query, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        var today = companyClock.DateOf(clock.GetUtcNow());
        var noticeEnd = today.AddDays(await db.SystemSettings.Select(s => s.LicenceNoticeDays).SingleAsync(cancellationToken));
        var licences = db.Licences.AsNoTracking();
        if (query.SoftwareId is { } softwareId)
        {
            licences = licences.Where(l => l.SoftwareId == softwareId);
        }

        if (query.TeamId is { } teamId)
        {
            licences = licences.Where(l => l.OwnerTeamId == teamId);
        }

        if (query.Kind is { } kind)
        {
            licences = licences.Where(l => l.Kind == kind);
        }

        licences = query.State switch
        {
            LicenceState.Full => licences.Where(l => db.LicenceAssignments.Count(a => a.LicenceId == l.Id && a.ReleasedAt == null) >= l.Quantity),
            LicenceState.Expiring => licences.Where(l => l.ExpiresOn >= today && l.ExpiresOn <= noticeEnd),
            LicenceState.Expired => licences.Where(l => l.ExpiresOn < today),
            _ => licences,
        };

        var rows =
            from licence in licences
            join software in db.Software on licence.SoftwareId equals software.Id
            join team in db.Teams on licence.OwnerTeamId equals team.Id
            select new { licence, software, team };
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Containing(TurkishText.ToSearchKey(query.Search.Trim()));
            rows = rows.Where(r => EF.Functions.Like(r.software.SearchText, pattern, LikePattern.EscapeText)
                || EF.Functions.Like(r.licence.SearchText, pattern, LikePattern.EscapeText));
        }

        return await rows.OrderBy(r => r.software.SearchText).ThenBy(r => r.licence.SearchText).ThenBy(r => r.licence.Id)
            .Select(r => new LicenceListItem(
                r.licence.Id, r.licence.Name, r.software.Id, r.software.Name, r.software.Vendor, r.licence.Kind, r.licence.Quantity,
                db.LicenceAssignments.Count(a => a.LicenceId == r.licence.Id && a.ReleasedAt == null),
                r.team.Id, r.team.Name, r.licence.ExpiresOn, r.licence.ExpiresOn < today))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}

public sealed record GetLicenceQuery(Guid Id);

public sealed class GetLicenceHandler(IAppDbContext db, ICurrentActor actor, ICompanyClock companyClock, TimeProvider clock)
{
    public async Task<LicenceDetail> HandleAsync(GetLicenceQuery query, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        return await LicenceQueries.LoadDetailAsync(db, query.Id, companyClock.DateOf(clock.GetUtcNow()), cancellationToken);
    }
}

public sealed record ListLicenceAssignmentsQuery(Guid LicenceId, bool Released, int Page, int PageSize);

/// <summary>The seats in use, or the released ones, newest first.</summary>
public sealed class ListLicenceAssignmentsHandler(IAppDbContext db, ICurrentActor actor)
{
    private const string NameSeparator = " ";

    public async Task<PagedResult<LicenceAssignmentItem>> HandleAsync(ListLicenceAssignmentsQuery query, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        if (!await db.Licences.AnyAsync(l => l.Id == query.LicenceId, cancellationToken))
        {
            throw new NotFoundException(AuditEntityTypes.Licence);
        }

        return await (
            from assignment in db.LicenceAssignments.AsNoTracking()
            where assignment.LicenceId == query.LicenceId && (assignment.ReleasedAt != null) == query.Released
            join u in db.Users on assignment.UserId equals u.Id into users
            from user in users.DefaultIfEmpty()
            join a in db.Assets on assignment.AssetId equals a.Id into assets
            from asset in assets.DefaultIfEmpty()
            orderby assignment.ReleasedAt ?? assignment.AssignedAt descending, assignment.Id
            select new LicenceAssignmentItem(
                assignment.Id, assignment.UserId, user == null ? null : user.FirstName + NameSeparator + user.LastName,
                assignment.AssetId, asset == null ? null : asset.AssetTag,
                (user != null && user.IsActive) || (asset != null && asset.Status != AssetStatus.Retired && asset.Status != AssetStatus.Lost),
                assignment.AssignedAt, assignment.ReleasedAt))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}

public sealed record ListAssetLicencesQuery(string AssetTag);

public sealed class ListAssetLicencesHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<IReadOnlyList<AssetLicenceItem>> HandleAsync(ListAssetLicencesQuery query, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureStaffAsync(db, actor, cancellationToken);
        var tag = Asset.NormalizeTag(query.AssetTag);
        var assetId = await db.Assets.Where(a => a.NormalizedAssetTag == tag).Select(a => (Guid?)a.Id).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Asset);
        return await (
            from assignment in db.LicenceAssignments.AsNoTracking()
            where assignment.AssetId == assetId && assignment.ReleasedAt == null
            join licence in db.Licences on assignment.LicenceId equals licence.Id
            join software in db.Software on licence.SoftwareId equals software.Id
            orderby software.SearchText, licence.SearchText, licence.Id
            select new AssetLicenceItem(licence.Id, licence.Name, software.Name, software.Vendor, licence.ExpiresOn))
            .ToListAsync(cancellationToken);
    }
}

/// <summary>The software the signed-in user has: own seats and seats of the devices assigned to them. Names only.</summary>
public sealed class ListMySoftwareHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<IReadOnlyList<MySoftwareItem>> HandleAsync(CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        var myAssets = db.AssetAssignments.Where(a => a.UserId == userId && a.ReturnedAt == null).Select(a => a.AssetId);
        var mine =
            from assignment in db.LicenceAssignments
            where assignment.ReleasedAt == null
                && (assignment.UserId == userId || (assignment.AssetId != null && myAssets.Contains(assignment.AssetId.Value)))
            join licence in db.Licences on assignment.LicenceId equals licence.Id
            select licence.SoftwareId;
        return await db.Software.AsNoTracking().Where(s => mine.Contains(s.Id))
            .OrderBy(s => s.SearchText).ThenBy(s => s.Id)
            .Select(s => new MySoftwareItem(s.Name, s.Vendor))
            .ToListAsync(cancellationToken);
    }
}
