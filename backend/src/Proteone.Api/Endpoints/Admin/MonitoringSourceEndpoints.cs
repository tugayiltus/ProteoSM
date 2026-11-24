using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Net.Http.Headers;
using Proteone.Api.Contracts;
using Proteone.Api.Http;
using Proteone.Application.Features.Monitoring;
using Proteone.Domain.Monitoring;
using Proteone.Domain.Tickets;

namespace Proteone.Api.Endpoints.Admin;

public sealed record CreateMonitoringSourceRequest(
    string Name, Guid TeamId, Guid RequesterId, Guid? ServiceId, Guid? CategoryId, Level Impact, Level Urgency, bool IsActive)
{
    internal MonitoringSourceDraft ToDraft() => new(Name, TeamId, RequesterId, ServiceId, CategoryId, Impact, Urgency, IsActive);
}

public sealed record UpdateMonitoringSourceRequest(
    uint Version, string Name, Guid TeamId, Guid RequesterId, Guid? ServiceId, Guid? CategoryId, Level Impact, Level Urgency, bool IsActive)
{
    internal MonitoringSourceDraft ToDraft() => new(Name, TeamId, RequesterId, ServiceId, CategoryId, Impact, Urgency, IsActive);
}

/// <summary>Monitoring sources (ADR-0041); the admin group checks the role.</summary>
internal static class MonitoringSourceEndpoints
{
    private const string Tag = "Admin: Monitoring";
    private const string NoStore = "no-store";

    public static RouteGroupBuilder MapMonitoringSourceEndpoints(this RouteGroupBuilder admin)
    {
        var sources = admin.MapGroup(ApiRoutes.MonitoringSources).WithTags(Tag);
        sources.MapGet("", async (ListMonitoringSourcesHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        sources.MapPost("", CreateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        sources.MapPut("/{id:guid}", async (Guid id, UpdateMonitoringSourceRequest r, UpdateMonitoringSourceHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new UpdateMonitoringSourceCommand(id, r.Version, r.ToDraft()), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        sources.MapPost("/{id:guid}/secret", ReplaceSecretAsync).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        return admin;
    }

    private static async Task<Created<MonitoringSourceSecret>> CreateAsync(
        CreateMonitoringSourceRequest request, CreateMonitoringSourceHandler handler, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CreateMonitoringSourceCommand(request.ToDraft()), cancellationToken);
        response.Headers[HeaderNames.CacheControl] = NoStore;
        return TypedResults.Created($"{ApiRoutes.Admin}{ApiRoutes.MonitoringSources}/{result.Source.Id}", result);
    }

    private static async Task<Ok<MonitoringSourceSecret>> ReplaceSecretAsync(
        Guid id, VersionRequest request, ReplaceMonitoringSecretHandler handler, HttpResponse response, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ReplaceMonitoringSecretCommand(id, request.Version), cancellationToken);
        response.Headers[HeaderNames.CacheControl] = NoStore;
        return TypedResults.Ok(result);
    }
}
