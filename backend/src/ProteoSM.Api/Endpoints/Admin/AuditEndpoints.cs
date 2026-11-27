using Microsoft.AspNetCore.Http.HttpResults;
using ProteoSM.Api.Http;
using ProteoSM.Application.Common.Paging;
using ProteoSM.Application.Features.Audit;

namespace ProteoSM.Api.Endpoints.Admin;

internal static class AuditEndpoints
{
    private const string Tag = "Admin: Audit";

    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder admin)
    {
        admin.MapGet(ApiRoutes.AuditEvents, ListAsync).WithTags(Tag);
        return admin;
    }

    private static async Task<Ok<PagedResult<AuditEventItem>>> ListAsync(
        string? action, string? entityType, Guid? entityId, Guid? actorUserId, DateTimeOffset? from, DateTimeOffset? to,
        int? page, int? pageSize, ListAuditEventsHandler handler, CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(
            new ListAuditEventsQuery(action, entityType, entityId, actorUserId, from, to,
                page ?? PageRequest.FirstPage, pageSize ?? PageRequest.DefaultPageSize),
            cancellationToken));
}
