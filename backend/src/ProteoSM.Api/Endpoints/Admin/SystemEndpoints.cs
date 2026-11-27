using Microsoft.AspNetCore.Http.HttpResults;
using ProteoSM.Api.Http;
using ProteoSM.Application.Features.Operations;

namespace ProteoSM.Api.Endpoints.Admin;

internal static class SystemEndpoints
{
    private const string Tag = "Admin: System";

    public static RouteGroupBuilder MapSystemEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGroup(ApiRoutes.System).WithTags(Tag).MapGet("/workers", ListWorkersAsync);
        return admin;
    }

    private static async Task<Ok<WorkerHeartbeatsResult>> ListWorkersAsync(ListWorkerHeartbeatsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(cancellationToken));
}
