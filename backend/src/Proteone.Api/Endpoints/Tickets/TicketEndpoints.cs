using Microsoft.AspNetCore.Http.HttpResults;
using Proteone.Api.Http;
using Proteone.Application.Common.Paging;
using Proteone.Application.Features.Ratings;
using Proteone.Application.Features.Tickets;
using Proteone.Domain.Tickets;

namespace Proteone.Api.Endpoints.Tickets;

public sealed record RateTicketRequest(int Score, string? Comment);

internal static partial class TicketEndpoints
{
    private const string Tag = "Tickets";
    private const string QueueParameter = "queue";
    private const string StatusParameter = "status";
    private const string KindParameter = "kind";
    private const string PriorityParameter = "priority";

    public static RouteGroupBuilder MapTicketEndpoints(this RouteGroupBuilder api)
    {
        var tickets = api.MapGroup(ApiRoutes.Tickets).WithTags(Tag).RequireAuthorization();

        tickets.MapPost("/incidents", async (CreateIncidentCommand command, CreateIncidentHandler handler, CancellationToken ct) => Created(await handler.HandleAsync(command, ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapPost("/requests", async (CreateRequestCommand command, CreateRequestHandler handler, CancellationToken ct) => Created(await handler.HandleAsync(command, ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        tickets.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest);
        tickets.MapGet("/{number}", async (string number, GetTicketHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new GetTicketQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);

        tickets.MapGet("/{number}/changes", async (string number, Application.Features.Changes.ListTicketChangesHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(new Application.Features.Changes.ListTicketChangesQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        tickets.MapGet("/{number}/problem", async Task<Results<Ok<Application.Features.Problems.TicketProblemItem>, NoContent>> (
                string number, Application.Features.Problems.GetTicketProblemHandler handler, CancellationToken ct) =>
                await handler.HandleAsync(new Application.Features.Problems.GetTicketProblemQuery(number), ct) is { } problem
                    ? TypedResults.Ok(problem)
                    : TypedResults.NoContent())
            .ProducesProblem(StatusCodes.Status404NotFound);
        tickets.MapPut("/{number}/rating", async (string number, RateTicketRequest r, RateTicketHandler handler, CancellationToken ct) =>
                TypedResults.Ok(await handler.HandleAsync(new RateTicketCommand(number, r.Score, r.Comment), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        tickets.MapTicketActionEndpoints();
        return tickets;
    }

    private static Created<TicketDetail> Created(TicketDetail detail) =>
        TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Tickets}/{detail.Number}", detail);

    private static async Task<Ok<PagedResult<TicketListItem>>> ListAsync(
        string? queue, string? status, string? kind, string? priority, string? search, int? page, int? pageSize, Guid? serviceId, bool? open,
        ListTicketsHandler handler, CancellationToken cancellationToken)
    {
        var query = new ListTicketsQuery(
            QueryParsing.ParseEnum<TicketQueue>(queue, QueueParameter),
            QueryParsing.ParseEnum<TicketStatus>(status, StatusParameter),
            QueryParsing.ParseEnum<TicketKind>(kind, KindParameter),
            QueryParsing.ParseEnum<Priority>(priority, PriorityParameter),
            search,
            page ?? PageRequest.FirstPage,
            pageSize ?? PageRequest.DefaultPageSize,
            serviceId,
            open ?? false);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }
}
