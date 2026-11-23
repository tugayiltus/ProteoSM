using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Features.Services;

namespace Tgysm.Api.Endpoints.Admin;

public sealed record UpdateServiceRequest(string Name, string? Description, Guid OwnerTeamId, uint Version);

public sealed record LinkServiceAssetRequest(Guid AssetId);

/// <summary>Service catalogue administration (Phase 12). No delete: a service is deactivated instead.</summary>
internal static class ServiceEndpoints
{
    private const string Tag = "Admin: Services";
    private const string AssetsSegment = "/assets";

    public static RouteGroupBuilder MapServiceAdminEndpoints(this RouteGroupBuilder admin)
    {
        var services = admin.MapGroup(ApiRoutes.Services).WithTags(Tag);

        services.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        services.MapPut("/{id:guid}", UpdateAsync)
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        services.MapPost("/{id:guid}/activate", (Guid id, VersionRequest r, SetServiceActiveHandler h, CancellationToken ct) => SetActiveAsync(id, r, h, isActive: true, ct))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        services.MapPost("/{id:guid}/deactivate", (Guid id, VersionRequest r, SetServiceActiveHandler h, CancellationToken ct) => SetActiveAsync(id, r, h, isActive: false, ct))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        services.MapPost($"/{{id:guid}}{AssetsSegment}", LinkAssetAsync).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        services.MapDelete($"/{{id:guid}}{AssetsSegment}/{{assetId:guid}}", UnlinkAssetAsync).ProducesProblem(StatusCodes.Status404NotFound);

        return admin;
    }

    private static async Task<Created<ServiceDetail>> CreateAsync(CreateServiceCommand command, CreateServiceHandler handler, CancellationToken cancellationToken)
    {
        var detail = await handler.HandleAsync(command, cancellationToken);
        return TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Services}/{detail.Id}", detail);
    }

    private static async Task<Ok<ServiceDetail>> UpdateAsync(Guid id, UpdateServiceRequest request, UpdateServiceHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new UpdateServiceCommand(id, request.Version, request.Name, request.Description, request.OwnerTeamId), cancellationToken));

    private static async Task<Ok<ServiceDetail>> SetActiveAsync(
        Guid id, VersionRequest request, SetServiceActiveHandler handler, bool isActive, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new SetServiceActiveCommand(id, request.Version, isActive), cancellationToken));

    private static async Task<Created<ServiceAssetItem>> LinkAssetAsync(
        Guid id, LinkServiceAssetRequest request, LinkServiceAssetHandler handler, CancellationToken cancellationToken)
    {
        var item = await handler.HandleAsync(new LinkServiceAssetCommand(id, request.AssetId), cancellationToken);
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.Services}/{id}{AssetsSegment}/{item.AssetId}", item);
    }

    private static async Task<NoContent> UnlinkAssetAsync(Guid id, Guid assetId, UnlinkServiceAssetHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new UnlinkServiceAssetCommand(id, assetId), cancellationToken);
        return TypedResults.NoContent();
    }
}
