using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Api.IntegrationTests.Tickets;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Knowledge;
using ProteoSM.Domain.Problems;

namespace ProteoSM.Api.IntegrationTests.Problems;

[Collection(ApiCollection.Name)]
public sealed class ProblemArticleTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ArticleSegment = "article";
    private const string NotAllowedCode = "problems.article_not_allowed";
    private const string RootCause = "Güvenlik duvarı oturumu erken kapatıyor";
    private const string Workaround = "İstemciyi yeniden başlatın";
    private const string ArticleTitlePrefix = "Bilinen hata: ";
    private const int ParallelCalls = 2;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> ArticleAsync(ApiClient client, JsonElement problem) =>
        client.PostAsync(ProblemWorld.PathOf(problem, ArticleSegment), new { version = ProblemWorld.VersionOf(problem) });

    private static async Task<JsonElement> PostOkAsync(ApiClient client, JsonElement problem, string action, object body)
    {
        using var response = await client.PostAsync(ProblemWorld.PathOf(problem, action), body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static async Task<JsonElement> KnownErrorAsync(ApiClient client, Guid teamId)
    {
        var problem = await ProblemWorld.CreateForTeamAsync(client, teamId, "VPN kopuyor");
        using var edit = await client.PutAsync(ProblemWorld.PathOf(problem), ProblemWorld.EditBody(problem, RootCause, Workaround));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        var edited = await client.ReadJsonAsync<JsonElement>(edit);
        return await PostOkAsync(client, edited, "known-error", new { version = ProblemWorld.VersionOf(edited) });
    }

    [Fact]
    public async Task Known_error_gets_a_draft_internal_article_and_the_problem_links_it()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await KnownErrorAsync(world.Desk, world.ServiceDesk.Id);

        using var response = await ArticleAsync(world.Desk, problem);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var detail = await world.Desk.ReadJsonAsync<JsonElement>(response);
        var articleId = detail.GetProperty("articleId").GetGuid();
        var problemId = problem.GetProperty("id").GetGuid();
        await using var db = factory.Database.CreateDbContext();
        var article = await db.KnowledgeArticles.SingleAsync(Ct);
        Assert.Equal(articleId, article.Id);
        Assert.Equal(ArticleStatus.Draft, article.Status);
        Assert.Equal(ArticleVisibility.Internal, article.Visibility);
        Assert.Equal($"{ArticleTitlePrefix}VPN kopuyor", article.Title);
        Assert.Contains($"Problem: {ProblemWorld.NumberOf(problem)}", article.Body);
        Assert.Equal(1, await db.ProblemEvents.CountAsync(e => e.ProblemId == problemId && e.Kind == ProblemEventKind.ArticleCreated, Ct));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.ProblemArticleCreated && a.EntityId == problemId, Ct));
    }

    [Fact]
    public async Task Second_call_is_409_article_not_allowed_and_adds_no_article()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await KnownErrorAsync(world.Desk, world.ServiceDesk.Id);
        using var first = await ArticleAsync(world.Desk, problem);
        var linked = await world.Desk.ReadJsonAsync<JsonElement>(first);

        using var second = await ArticleAsync(world.Desk, linked);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(NotAllowedCode, await ApiClient.ReadCodeAsync(second));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.KnowledgeArticles.CountAsync(Ct));
    }

    [Fact]
    public async Task Parallel_calls_create_one_article_only()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await KnownErrorAsync(world.Desk, world.ServiceDesk.Id);

        var responses = await Task.WhenAll(Enumerable.Range(0, ParallelCalls).Select(_ => ArticleAsync(world.Desk, problem)));

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(ParallelCalls - 1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        foreach (var response in responses)
        {
            response.Dispose();
        }

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.KnowledgeArticles.CountAsync(Ct));
        Assert.Equal(1, await db.Problems.CountAsync(p => p.ArticleId != null, Ct));
    }

    [Fact]
    public async Task Resolved_problem_article_has_the_permanent_fix_section()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await KnownErrorAsync(world.Desk, world.ServiceDesk.Id);
        var resolved = await PostOkAsync(world.Desk, problem, "resolve", new { version = ProblemWorld.VersionOf(problem), resolution = "Oturum süresi artırıldı" });

        using var response = await ArticleAsync(world.Desk, resolved);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        Assert.Contains("## Kalıcı çözüm", (await db.KnowledgeArticles.SingleAsync(Ct)).Body);
    }

    [Fact]
    public async Task Open_problem_is_409_article_not_allowed()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        using var response = await ArticleAsync(world.Desk, problem);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(NotAllowedCode, await ApiClient.ReadCodeAsync(response));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(0, await db.KnowledgeArticles.CountAsync(Ct));
    }

    [Fact]
    public async Task Non_owner_staff_is_403_and_creates_nothing()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await KnownErrorAsync(world.Desk, world.ServiceDesk.Id);

        using var response = await ArticleAsync(world.Field, problem);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(0, await db.KnowledgeArticles.CountAsync(Ct));
    }
}
