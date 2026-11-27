using ProteoSM.Api.Http;
using ProteoSM.Api.Security;
using ProteoSM.Application.Features.Reference;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Api.Endpoints;

internal static class ReferenceEndpoints
{
    private const string Tag = "Reference";
    private const string KindParameter = "kind";

    public static RouteGroupBuilder MapReferenceEndpoints(this RouteGroupBuilder api)
    {
        var reference = api.MapGroup(ApiRoutes.Reference).WithTags(Tag);

        reference.MapGet("/locations", async (ListReferenceLocationsHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)));
        reference.MapGet("/departments", async (ListReferenceDepartmentsHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)));
        reference.MapGet("/categories", async (string? kind, ListReferenceCategoriesHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new ListReferenceCategoriesQuery(QueryParsing.ParseEnum<TicketKind>(kind, KindParameter)), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest);
        reference.MapGet("/request-types", async (ListReferenceRequestTypesHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)));
        reference.MapGet("/teams", async (ListReferenceTeamsHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)))
            .RequireAuthorization(Policies.ItStaffOrAdmin);
        reference.MapGet("/users", async (string? search, SearchReferenceUsersHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new SearchReferenceUsersQuery(search), ct)))
            .RequireAuthorization(Policies.ItStaffOrAdmin);

        return reference;
    }
}
