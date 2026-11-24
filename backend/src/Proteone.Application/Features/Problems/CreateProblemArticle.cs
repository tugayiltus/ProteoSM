using Proteone.Application.Abstractions;
using Proteone.Application.Features.Knowledge;
using Proteone.Domain.Audit;
using Proteone.Domain.Knowledge;
using Proteone.Domain.Problems;

namespace Proteone.Application.Features.Problems;

public sealed record CreateProblemArticleCommand(string Number, uint Version);

/// <summary>
/// Creates the draft, internal known-error article of a problem through <see cref="CreateArticleHandler"/> (its
/// validation, numbering and audit) and links it. <see cref="CreateArticleHandler"/> saves on its own, so everything runs
/// in one transaction: a version conflict or a refused link rolls the article back, and of two parallel calls only the
/// one whose save passes the version check keeps its article.
/// </summary>
public sealed class CreateProblemArticleHandler(IAppDbContext db, ProblemWork work, CreateArticleHandler articles, IAuditLog audit)
{
    public async Task<ProblemDetail> HandleAsync(CreateProblemArticleCommand command, CancellationToken cancellationToken)
    {
        ProblemDetail? detail = null;
        await db.RunInTransactionAsync(async ct =>
        {
            detail = await work.RunAsync(command.Number, command.Version, async (problem, viewer, now) =>
            {
                var (title, summary, body) = KnownErrorArticleText.Build(problem, problem.Number);
                var article = await articles.HandleAsync(new CreateArticleCommand(title, summary, body, null, ArticleVisibility.Internal), ct);
                problem.AttachArticle(article.Id, now);
                ProblemHistory.Record(db, problem, ProblemEventKind.ArticleCreated, viewer.UserId, now, new { articleId = article.Id });
                audit.Record(AuditActions.ProblemArticleCreated, AuditEntityTypes.Problem, problem.Id, newValues: new { ArticleId = article.Id });
            }, ct);
        }, cancellationToken);
        return detail!;
    }
}
