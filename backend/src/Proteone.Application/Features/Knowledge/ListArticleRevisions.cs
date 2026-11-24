using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Audit;

namespace Proteone.Application.Features.Knowledge;

public sealed record ListArticleRevisionsQuery(Guid Id);

/// <summary>Newest first. Authorization (IT staff or admin) is enforced at the endpoint.</summary>
public sealed class ListArticleRevisionsHandler(IAppDbContext db)
{
    private const string NameSeparator = " ";

    public async Task<IReadOnlyList<ArticleRevisionItem>> HandleAsync(ListArticleRevisionsQuery query, CancellationToken cancellationToken)
    {
        if (!await db.KnowledgeArticles.AsNoTracking().AnyAsync(a => a.Id == query.Id, cancellationToken))
        {
            throw new NotFoundException(AuditEntityTypes.KnowledgeArticle);
        }

        return await db.ArticleRevisions.AsNoTracking().Where(r => r.ArticleId == query.Id)
            .OrderByDescending(r => r.Revision)
            .Select(r => new ArticleRevisionItem(
                r.Revision, r.Title, r.Visibility,
                db.Users.Where(u => u.Id == r.EditedBy).Select(u => u.FirstName + NameSeparator + u.LastName).First(),
                r.EditedAt))
            .ToListAsync(cancellationToken);
    }
}
