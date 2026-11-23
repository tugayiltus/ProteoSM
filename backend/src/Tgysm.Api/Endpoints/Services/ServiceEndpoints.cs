using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Services;

namespace Tgysm.Api.Endpoints.Services;

/// <summary>
/// Service reads for IT staff and admins (Phase 12). Every signed-in user reaches the routes; the handlers answer
/// "not found" to employees, so employees cannot tell that services exist.
/// </summary>
internal static class ServiceEndpoints
{
    private const string Tag = "Services";
    private const string AssetServicesRoute = ApiRoutes.Assets + "/{tag}" + ApiRoutes.Services;

    public static RouteGroupBuilder MapServiceEndpoints(this RouteGroupBuilder api)
    {
        var services = api.MapGroup(ApiRoutes.Services).WithTags(Tag).RequireAuthorization();
        services.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        services.MapGet("/{id:guid}", async (Guid id, GetServiceHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new GetServiceQuery(id), ct))).ProducesProblem(StatusCodes.Status404NotFound);

        services.MapGet("/{id:guid}/impact", async (Guid id, GetServiceImpactHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new GetServiceImpactQuery(id), ct))).ProducesProblem(StatusCodes.Status404NotFound);

        // Outside the asset group on purpose: that group answers employees with 403, service routes answer 404.
        api.MapGet(AssetServicesRoute, async (string tag, ListAssetServicesHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new ListAssetServicesQuery(tag), ct)))
            .WithTags(Tag).RequireAuthorization().ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }

    private static async Task<Ok<PagedResult<ServiceListItem>>> ListAsync(
        bool? active, Guid? teamId, string? q, int? page, int? pageSize, ListServicesHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListServicesQuery(active, teamId, q, page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize), cancellationToken));
}
