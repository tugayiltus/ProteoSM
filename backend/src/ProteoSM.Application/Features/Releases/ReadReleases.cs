using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Application.Common.Paging;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Releases;

namespace ProteoSM.Application.Features.Releases;

public sealed record ListReleasesQuery(ReleaseStatus? Status, Guid? TeamId, bool OpenOnly, string? Search, int Page, int PageSize);

public sealed class ListReleasesHandler(IAppDbContext db, ReleaseWork work)
{
    private const string NameSeparator = " ";

    public async Task<PagedResult<ReleaseListItem>> HandleAsync(ListReleasesQuery query, CancellationToken cancellationToken)
    {
        await work.StaffViewerAsync(cancellationToken);
        var releases = db.Releases.AsNoTracking();
        if (query.Status is { } status)
        {
            releases = releases.Where(r => r.Status == status);
        }

        if (query.OpenOnly)
        {
            releases = releases.Where(r => r.Status == ReleaseStatus.Planned || r.Status == ReleaseStatus.InProgress);
        }

        if (query.TeamId is { } teamId)
        {
            releases = releases.Where(r => r.OwnerTeamId == teamId);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var number = ReleaseNumber.Normalize(query.Search);
            var pattern = LikePattern.Containing(TurkishText.ToSearchKey(query.Search.Trim()));
            releases = ReleaseNumber.IsValid(number)
                ? releases.Where(r => r.Number == number)
                : releases.Where(r => EF.Functions.Like(r.SearchText, pattern, LikePattern.EscapeText));
        }

        return await releases
            .OrderByDescending(r => r.UpdatedAt).ThenByDescending(r => r.Id)
            .Select(r => new ReleaseListItem(
                r.Id, r.Number, r.Title, r.Status,
                db.Teams.Where(t => t.Id == r.OwnerTeamId).Select(t => t.Name).First(),
                db.Users.Where(u => u.Id == r.OwnerId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
                db.ChangeRequests.Count(c => c.ReleaseId == r.Id), r.WindowStart, r.WindowEnd, r.UpdatedAt))
            .ToPagedResultAsync(new PageRequest(query.Page, query.PageSize), cancellationToken);
    }
}

public sealed record GetReleaseQuery(string Number);

public sealed class GetReleaseHandler(ReleaseWork work)
{
    public async Task<ReleaseDetail> HandleAsync(GetReleaseQuery query, CancellationToken cancellationToken)
    {
        var (release, viewer) = await work.LoadAsync(query.Number, cancellationToken);
        return await work.DetailAsync(release.Id, viewer, cancellationToken);
    }
}

public sealed record GetReleaseTimelineQuery(string Number);

public sealed class GetReleaseTimelineHandler(IAppDbContext db, ReleaseWork work)
{
    private const string NameSeparator = " ";

    public async Task<IReadOnlyList<ReleaseTimelineItem>> HandleAsync(GetReleaseTimelineQuery query, CancellationToken cancellationToken)
    {
        var (release, _) = await work.LoadAsync(query.Number, cancellationToken);
        var rows = await db.ReleaseEvents.AsNoTracking().Where(e => e.ReleaseId == release.Id)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.Kind,
                e.DataJson,
                e.OccurredAt,
                Actor = db.Users.Where(u => u.Id == e.ActorUserId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new ReleaseTimelineItem(r.Id, r.Kind, r.Actor, JsonSerializer.Deserialize<JsonElement>(r.DataJson), r.OccurredAt))];
    }
}
