using ProteoSM.Api.Http;
using ProteoSM.Application.Features.Announcements;
using ProteoSM.Domain.Announcements;

namespace ProteoSM.Api.Endpoints;

public sealed record CreateAnnouncementRequest(string Title, string Body, AnnouncementLevel Level, DateTimeOffset StartsAt, DateTimeOffset? EndsAt)
{
    internal AnnouncementDraft ToDraft() => new(Title, Body, Level, StartsAt, EndsAt);
}

public sealed record UpdateAnnouncementRequest(uint Version, string Title, string Body, AnnouncementLevel Level, DateTimeOffset StartsAt, DateTimeOffset? EndsAt)
{
    internal AnnouncementDraft ToDraft() => new(Title, Body, Level, StartsAt, EndsAt);
}

/// <summary>Announcements (ADR-0042): everyone signed in reads the current ones, admins manage all of them.</summary>
internal static class AnnouncementEndpoints
{
    private const string Tag = "Announcements";
    private const string AdminTag = "Admin: Announcements";

    public static RouteGroupBuilder MapAnnouncementEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(ApiRoutes.Announcements, async (ListActiveAnnouncementsHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)))
            .WithTags(Tag).RequireAuthorization();
        return api;
    }

    public static RouteGroupBuilder MapAnnouncementAdminEndpoints(this RouteGroupBuilder admin)
    {
        var group = admin.MapGroup(ApiRoutes.Announcements).WithTags(AdminTag);
        group.MapGet("", async (ListAnnouncementsHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        group.MapPost("", async (CreateAnnouncementRequest r, CreateAnnouncementHandler h, CancellationToken ct) =>
            {
                var item = await h.HandleAsync(new CreateAnnouncementCommand(r.ToDraft()), ct);
                return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Announcements}/{item.Id}", item);
            })
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        group.MapPut("/{id:guid}", async (Guid id, UpdateAnnouncementRequest r, UpdateAnnouncementHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new UpdateAnnouncementCommand(id, r.Version, r.ToDraft()), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        return admin;
    }
}
