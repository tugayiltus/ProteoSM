using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using Proteone.Api.Contracts;
using Proteone.Api.Http;
using Proteone.Application.Common.Paging;
using Proteone.Application.Features.Licences;
using Proteone.Domain.Licences;

namespace Proteone.Api.Endpoints.Licences;

/// <summary>
/// Software and licences for IT staff and admins (ADR-0039). Every signed-in user reaches the routes; the handlers
/// answer "not found" to employees. Keys are for admins only.
/// </summary>
internal static class LicenceEndpoints
{
    private const string Tag = "Licences";
    private const string KindParameter = "kind";
    private const string StateParameter = "state";
    private const string NoStore = "no-store";
    private const string AssetLicencesRoute = ApiRoutes.Assets + "/{tag}" + ApiRoutes.Licences;

    public static RouteGroupBuilder MapLicenceEndpoints(this RouteGroupBuilder api)
    {
        var software = api.MapGroup(ApiRoutes.Software).WithTags(Tag).RequireAuthorization().ProducesProblem(StatusCodes.Status404NotFound);
        software.MapGet("", ListSoftwareAsync).ProducesProblem(StatusCodes.Status400BadRequest);
        software.MapPost("", async (CreateSoftwareCommand command, CreateSoftwareHandler h, CancellationToken ct) =>
        {
            var item = await h.HandleAsync(command, ct);
            return TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Software}/{item.Id}", item);
        }).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        software.MapPut("/{id:guid}", async (Guid id, UpdateSoftwareRequest r, UpdateSoftwareHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new UpdateSoftwareCommand(id, r.Version, r.Name, r.Vendor, r.IsActive), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);

        var licences = api.MapGroup(ApiRoutes.Licences).WithTags(Tag).RequireAuthorization().ProducesProblem(StatusCodes.Status404NotFound);
        licences.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest);
        licences.MapPost("", async (CreateLicenceCommand command, CreateLicenceHandler h, CancellationToken ct) =>
        {
            var detail = await h.HandleAsync(command, ct);
            return TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Licences}/{detail.Id}", detail);
        }).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        licences.MapGet("/{id:guid}", async (Guid id, GetLicenceHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new GetLicenceQuery(id), ct)));
        licences.MapPut("/{id:guid}", async (Guid id, UpdateLicenceRequest r, UpdateLicenceHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new UpdateLicenceCommand(id, r.Version, r.Name, r.Quantity, r.OwnerTeamId, r.ExpiresOn, r.Notes), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        licences.MapGet("/{id:guid}/assignments", async (
                Guid id, bool? released, int? page, int? pageSize, ListLicenceAssignmentsHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(
                new ListLicenceAssignmentsQuery(id, released ?? false, page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize), ct)));
        licences.MapPost("/{id:guid}/assignments", async (Guid id, AssignLicenceRequest r, AssignLicenceHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new AssignLicenceCommand(id, r.UserId, r.AssetId), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        licences.MapDelete("/{id:guid}/assignments/{assignmentId:guid}", async (
            Guid id, Guid assignmentId, ReleaseLicenceHandler h, CancellationToken ct) =>
        {
            await h.HandleAsync(new ReleaseLicenceCommand(id, assignmentId), ct);
            return TypedResults.NoContent();
        });
        licences.MapPut("/{id:guid}/key", async (Guid id, SetLicenceKeyRequest r, SetLicenceKeyHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetLicenceKeyCommand(id, r.Version, r.Key), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status409Conflict);
        licences.MapPost("/{id:guid}/key/reveal", RevealKeyAsync).ProducesProblem(StatusCodes.Status403Forbidden);

        api.MapGet(ApiRoutes.MySoftware, async (ListMySoftwareHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)))
            .WithTags(Tag).RequireAuthorization();
        // Outside the asset group on purpose: that group answers employees with 403, licence routes answer 404.
        api.MapGet(AssetLicencesRoute, async (string tag, ListAssetLicencesHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new ListAssetLicencesQuery(tag), ct)))
            .WithTags(Tag).RequireAuthorization().ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<Ok<PagedResult<SoftwareItem>>> ListSoftwareAsync(
        bool? active, string? q, int? page, int? pageSize, ListSoftwareHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListSoftwareQuery(active, q, page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize), cancellationToken));

    private static async Task<Ok<PagedResult<LicenceListItem>>> ListAsync(
        Guid? softwareId, Guid? teamId, string? kind, string? state, string? q, int? page, int? pageSize,
        ListLicencesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListLicencesQuery(
                softwareId, teamId, QueryParsing.ParseEnum<LicenceKind>(kind, KindParameter), QueryParsing.ParseEnum<LicenceState>(state, StateParameter), q,
                page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize),
            cancellationToken));

    private static async Task<Ok<LicenceKeyResult>> RevealKeyAsync(
        Guid id, RevealLicenceKeyHandler handler, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new RevealLicenceKeyCommand(id), cancellationToken);
        response.Headers[HeaderNames.CacheControl] = NoStore;
        return TypedResults.Ok(result);
    }
}
