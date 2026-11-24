using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Audit;
using Proteone.Domain.Knowledge;

namespace Proteone.Application.Features.Knowledge;

public sealed record GetArticleQuery(Guid Id);

/// <summary>Unreadable and unknown ids are both "not found", so drafts and internal articles do not leak.</summary>
public sealed class GetArticleHandler(IAppDbContext db, ICurrentActor actor)
{
    public async Task<ArticleDetail> HandleAsync(GetArticleQuery query, CancellationToken cancellationToken)
    {
        var role = await ArticleQueries.RoleOfAsync(db, actor, cancellationToken);
        var readable = await db.KnowledgeArticles.AsNoTracking().Where(ArticleAccess.ReadableBy(role)).AnyAsync(a => a.Id == query.Id, cancellationToken);
        return readable
            ? await ArticleQueries.LoadDetailAsync(db, query.Id, cancellationToken)
            : throw new NotFoundException(AuditEntityTypes.KnowledgeArticle);
    }
}
