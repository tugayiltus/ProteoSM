using Microsoft.AspNetCore.Http.HttpResults;
using Tgysm.Api.Contracts;
using Tgysm.Api.Http;
using Tgysm.Application.Common.Paging;
using Tgysm.Application.Features.Problems;
using Tgysm.Domain.Problems;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.Endpoints.Problems;

public sealed record CreateProblemRequest(string Title, string Description, Level Impact, Guid OwnerTeamId, Guid? OwnerId, string? IncidentNumber);

public sealed record UpdateProblemRequest(uint Version, string Title, string Description, Level Impact, string? RootCause, string? Workaround);

public sealed record ResolveProblemRequest(uint Version, string Resolution);

public sealed record CancelProblemRequest(uint Version, string Reason);

public sealed record TransferProblemRequest(uint Version, Guid TeamId, string Reason);

public sealed record SetProblemOwnerRequest(uint Version, Guid? OwnerId);

public sealed record ProblemNoteRequest(string Text);

public sealed record AddProblemTicketRequest(string TicketNumber);

public sealed record AddProblemChangeRequest(string ChangeNumber);

internal static class ProblemEndpoints
{
    private const string Tag = "Problems";
    private const string NumberRoute = "/{number}";
    private const string QueueParameter = "queue";
    private const string StatusParameter = "status";
    private const string ImpactParameter = "impact";
    private const string TicketsSegment = "tickets";
    private const string ChangesSegment = "changes";
    private const string TicketNumberRoute = "{ticketNumber}";
    private const string ChangeNumberRoute = "{changeNumber}";

    public static RouteGroupBuilder MapProblemEndpoints(this RouteGroupBuilder api)
    {
        var problems = api.MapGroup(ApiRoutes.Problems).WithTags(Tag).RequireAuthorization();
        problems.MapPost("", async (CreateProblemRequest r, CreateProblemHandler h, CancellationToken ct) =>
                Created(await h.HandleAsync(new CreateProblemCommand(r.Title, r.Description, r.Impact, r.OwnerTeamId, r.OwnerId, r.IncidentNumber), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        problems.MapGet("", ListAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound);
        problems.MapGet(NumberRoute, async (string number, GetProblemHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new GetProblemQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        problems.MapGet($"{NumberRoute}/timeline", async (string number, GetProblemTimelineHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new GetProblemTimelineQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        MapActions(problems);
        MapLinks(problems);
        return problems;
    }

    private static async Task<Ok<PagedResult<ProblemListItem>>> ListAsync(
        string? queue, string? status, string? impact, string? search, int? page, int? pageSize, ListProblemsHandler handler, CancellationToken cancellationToken)
    {
        var query = new ListProblemsQuery(
            QueryParsing.ParseEnum<ProblemQueue>(queue, QueueParameter),
            QueryParsing.ParseEnum<ProblemStatus>(status, StatusParameter),
            QueryParsing.ParseEnum<Level>(impact, ImpactParameter),
            search,
            page ?? PageRequest.FirstPage,
            pageSize ?? PageRequest.DefaultPageSize);
        return TypedResults.Ok(await handler.HandleAsync(query, cancellationToken));
    }

    private static void MapActions(RouteGroupBuilder problems)
    {
        Action(problems.MapPut(NumberRoute, async (string number, UpdateProblemRequest r, UpdateProblemHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new UpdateProblemCommand(number, r.Version, r.Title, r.Description, r.Impact, r.RootCause, r.Workaround), ct))));
        Action(problems.MapPost($"{NumberRoute}/start", async (string number, VersionRequest r, StartProblemHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new StartProblemCommand(number, r.Version), ct))));
        Action(problems.MapPost($"{NumberRoute}/known-error", async (string number, VersionRequest r, MarkKnownErrorHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new MarkKnownErrorCommand(number, r.Version), ct))));
        Action(problems.MapPost($"{NumberRoute}/resolve", async (string number, ResolveProblemRequest r, ResolveProblemHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ResolveProblemCommand(number, r.Version, r.Resolution), ct))));
        Action(problems.MapPost($"{NumberRoute}/cancel", async (string number, CancelProblemRequest r, CancelProblemHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new CancelProblemCommand(number, r.Version, r.Reason), ct))));
        Action(problems.MapPost($"{NumberRoute}/transfer", async (string number, TransferProblemRequest r, TransferProblemHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new TransferProblemCommand(number, r.Version, r.TeamId, r.Reason), ct))));
        Action(problems.MapPost($"{NumberRoute}/owner", async (string number, SetProblemOwnerRequest r, SetProblemOwnerHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new SetProblemOwnerCommand(number, r.Version, r.OwnerId), ct))));
        problems.MapPost($"{NumberRoute}/notes", async (string number, ProblemNoteRequest r, AddProblemNoteHandler h, CancellationToken ct) =>
            {
                await h.HandleAsync(new AddProblemNoteCommand(number, r.Text), ct);
                return TypedResults.NoContent();
            })
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void MapLinks(RouteGroupBuilder problems)
    {
        problems.MapGet($"{NumberRoute}/{TicketsSegment}", async (string number, ListProblemTicketsHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ListProblemTicketsQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        Action(problems.MapPost($"{NumberRoute}/{TicketsSegment}", async (string number, AddProblemTicketRequest r, AddProblemTicketHandler h, CancellationToken ct) =>
            TypedResults.Created(
                $"{ApiRoutes.V1}{ApiRoutes.Problems}/{number}/{TicketsSegment}", await h.HandleAsync(new AddProblemTicketCommand(number, r.TicketNumber), ct))));
        problems.MapDelete($"{NumberRoute}/{TicketsSegment}/{TicketNumberRoute}", async (string number, string ticketNumber, RemoveProblemTicketHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new RemoveProblemTicketCommand(number, ticketNumber), ct)))
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);
        problems.MapGet($"{NumberRoute}/{ChangesSegment}", async (string number, ListProblemChangesHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new ListProblemChangesQuery(number), ct))).ProducesProblem(StatusCodes.Status404NotFound);
        Action(problems.MapPost($"{NumberRoute}/{ChangesSegment}", async (string number, AddProblemChangeRequest r, AddProblemChangeHandler h, CancellationToken ct) =>
            TypedResults.Created(
                $"{ApiRoutes.V1}{ApiRoutes.Problems}/{number}/{ChangesSegment}", await h.HandleAsync(new AddProblemChangeCommand(number, r.ChangeNumber), ct))));
        problems.MapDelete($"{NumberRoute}/{ChangesSegment}/{ChangeNumberRoute}", async (string number, string changeNumber, RemoveProblemChangeHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new RemoveProblemChangeCommand(number, changeNumber), ct)))
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static void Action(RouteHandlerBuilder route) =>
        route.ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

    private static Created<ProblemDetail> Created(ProblemDetail detail) =>
        TypedResults.Created($"{ApiRoutes.V1}{ApiRoutes.Problems}/{detail.Number}", detail);
}
