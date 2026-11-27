using Microsoft.AspNetCore.Http.HttpResults;
using ProteoSM.Api.Http;
using ProteoSM.Application.Features.Operations;

namespace ProteoSM.Api.Endpoints;

internal sealed record HealthResponse(string Status);

internal static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet(ApiRoutes.Health, GetAsync).AllowAnonymous().WithTags("Health");
        return api;
    }

    private static async Task<Ok<HealthResponse>> GetAsync(ListWorkerHeartbeatsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(new HealthResponse((await handler.HandleAsync(cancellationToken)).Status));
}
