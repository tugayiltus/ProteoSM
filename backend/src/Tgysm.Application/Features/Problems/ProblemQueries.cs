using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Features.Services;

namespace Tgysm.Application.Features.Problems;

internal static class ProblemQueries
{
    private const string NameSeparator = " ";

    public static async Task<ProblemDetail> LoadDetailAsync(IAppDbContext db, Guid problemId, ProblemViewer viewer, CancellationToken cancellationToken)
    {
        var p = await db.Problems.AsNoTracking().SingleAsync(x => x.Id == problemId, cancellationToken);
        var team = await db.Teams.Where(x => x.Id == p.OwnerTeamId).Select(x => x.Name).SingleAsync(cancellationToken);
        var owner = p.OwnerId is { } ownerId
            ? await db.Users.Where(u => u.Id == ownerId).Select(u => u.FirstName + NameSeparator + u.LastName).SingleAsync(cancellationToken)
            : null;
        var articleTitle = p.ArticleId is { } articleId
            ? await db.KnowledgeArticles.Where(a => a.Id == articleId).Select(a => a.Title).SingleOrDefaultAsync(cancellationToken)
            : null;
        return new ProblemDetail(
            p.Id, p.Number, p.Title, p.Description, p.Impact, p.Status, p.OwnerTeamId, team, p.OwnerId, owner, p.RootCause, p.Workaround,
            p.Resolution, p.CancelReason, p.ArticleId, articleTitle, p.CreatedAt, p.KnownErrorAt, p.ResolvedAt, viewer.CanWork(p), p.Version,
            await ServiceAssignment.RefAsync(db, p.ServiceId, cancellationToken));
    }
}
