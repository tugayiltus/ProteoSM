using System.Net;
using System.Text.Json;
using ProteoSM.Api.IntegrationTests.Infrastructure;

namespace ProteoSM.Api.IntegrationTests.Problems;

/// <summary>Problem test helpers on top of <see cref="Tickets.TicketWorld"/>: Desk is in Servis Masası, Field in Saha Destek.</summary>
public static class ProblemWorld
{
    public const string ProblemsPath = "/api/v1/problems";

    public static async Task<JsonElement> CreateAsync(ApiClient client, object body)
    {
        using var response = await client.PostAsync(ProblemsPath, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    public static Task<JsonElement> CreateForTeamAsync(
        ApiClient client, Guid teamId, string title = "Yazıcılar sık kapanıyor", Guid? ownerId = null, string impact = "medium") =>
        CreateAsync(client, new { title, description = "Birden fazla kullanıcı aynı belirtiyi bildiriyor", impact, ownerTeamId = teamId, ownerId });

    public static string PathOf(JsonElement problem, string? action = null) =>
        action is null ? $"{ProblemsPath}/{NumberOf(problem)}" : $"{ProblemsPath}/{NumberOf(problem)}/{action}";

    public static string NumberOf(JsonElement problem) => problem.GetProperty("number").GetString()!;

    public static uint VersionOf(JsonElement problem) => problem.GetProperty("version").GetUInt32();

    public static object EditBody(JsonElement problem, string? rootCause = null, string? workaround = null, string? title = null) => new
    {
        version = VersionOf(problem),
        title = title ?? problem.GetProperty("title").GetString(),
        description = problem.GetProperty("description").GetString(),
        impact = problem.GetProperty("impact").GetString(),
        rootCause,
        workaround,
    };
}
