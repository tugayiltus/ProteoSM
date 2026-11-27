using Microsoft.AspNetCore.Http.HttpResults;
using ProteoSM.Api.Contracts;
using ProteoSM.Api.Http;
using ProteoSM.Application.Common.Paging;
using ProteoSM.Application.Features.Releases;
using ProteoSM.Domain.Releases;

namespace ProteoSM.Api.Endpoints.Releases;

public sealed record CreateReleaseRequest(
    string Title, string Description, Guid OwnerTeamId, Guid? OwnerId, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, string? Notes)
{
    internal ReleaseDraft ToDraft() => new(Title, Description, OwnerTeamId, OwnerId, WindowStart, WindowEnd, Notes);
}

public sealed record UpdateReleaseRequest(
    uint Version, string Title, string Description, Guid OwnerTeamId, Guid? OwnerId, DateTimeOffset? WindowStart, DateTimeOffset? WindowEnd, string? Notes)
{
    internal ReleaseDraft ToDraft() => new(Title, Description, OwnerTeamId, OwnerId, WindowStart, WindowEnd, Notes);
}

public sealed record CancelReleaseRequest(uint Version, string Reason);

public sealed record AddReleaseChangeRequest(string ChangeNumber);

/// <summary>Releases for IT staff and admins (ADR-0040); the handlers answer "not found" to employees.</summary>
internal static class ReleaseEndpoints
{
    private const string Tag = "Releases";
    private const string NumberRoute = "/{number}";
    private const string ChangesRoute = NumberRoute + ApiRoutes.Changes;
    private const string StatusParameter = "status";

    public static RouteGroupBuilder MapReleaseEndpoints(this RouteGroupBuilder api)
    {
        var releases = api.MapGroup(ApiRoutes.Releases).WithTags(Tag).RequireAuthorization().ProducesProblem(StatusCodes.Status404NotFound);
        releases.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest);
        releases.MapPost("", async (CreateReleaseRequest r, CreateReleaseHandler h, CancellationToken ct) =>
        {
            var detail = await h.HandleAsync(new CreateReleaseCommand(r.ToDraft()), ct);
            return TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Releases}/{detail.Number}", detail);
        }).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        releases.MapGet(NumberRoute, async (string number, GetReleaseHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new GetReleaseQuery(number), ct)));
        releases.MapGet($"{NumberRoute}/timeline", async (string number, GetReleaseTimelineHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new GetReleaseTimelineQuery(number), ct)));
        Action(releases.MapPut(NumberRoute, async (string number, UpdateReleaseRequest r, UpdateReleaseHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new UpdateReleaseCommand(number, r.Version, r.ToDraft()), ct))));
        Action(releases.MapPost($"{NumberRoute}/start", async (string number, VersionRequest r, StartReleaseHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new StartReleaseCommand(number, r.Version), ct))));
        Action(releases.MapPost($"{NumberRoute}/complete", async (string number, VersionRequest r, CompleteReleaseHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new CompleteReleaseCommand(number, r.Version), ct))));
        Action(releases.MapPost($"{NumberRoute}/cancel", async (string number, CancelReleaseRequest r, CancelReleaseHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new CancelReleaseCommand(number, r.Version, r.Reason), ct))));
        Action(releases.MapPost(ChangesRoute, async (string number, AddReleaseChangeRequest r, AddReleaseChangeHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new AddReleaseChangeCommand(number, r.ChangeNumber), ct))));
        Action(releases.MapDelete($"{ChangesRoute}/{{changeNumber}}", async (string number, string changeNumber, RemoveReleaseChangeHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new RemoveReleaseChangeCommand(number, changeNumber), ct))));
        return api;
    }

    private static async Task<Ok<PagedResult<ReleaseListItem>>> ListAsync(
        string? status, Guid? teamId, bool? open, string? q, int? page, int? pageSize, ListReleasesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListReleasesQuery(
                QueryParsing.ParseEnum<ReleaseStatus>(status, StatusParameter), teamId, open ?? false, q,
                page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize),
            cancellationToken));

    private static void Action(RouteHandlerBuilder route) =>
        route.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status409Conflict);
}
