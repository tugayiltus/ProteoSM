using System.Security.Claims;
using ProteoSM.Api.Http;
using ProteoSM.Api.Security;
using ProteoSM.Application.Features.Monitoring;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Monitoring;

namespace ProteoSM.Api.Endpoints;

/// <summary>The monitoring webhook (ADR-0041): anonymous, signed, rate limited per client before anything else.</summary>
internal static class MonitoringEndpoints
{
    private const string EventsRoute = ApiRoutes.Monitoring + "/{sourceId:guid}/events";
    private const string TimestampHeader = "X-Proteone-Timestamp";
    private const string SignatureHeader = "X-Proteone-Signature";

    public static RouteGroupBuilder MapMonitoringEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost(EventsRoute, (Delegate)ReceiveAsync)
            .AllowAnonymous()
            .WithMetadata(new SkipCsrfMetadata())
            .RequireRateLimiting(RateLimitPolicies.Monitoring)
            .ExcludeFromDescription();
        return api;
    }

    /// <summary>Writes the problem itself: no trace id or other part of the answer may differ between refusals.</summary>
    private static async Task ReceiveAsync(Guid sourceId, HttpContext context, ReceiveMonitoringEventHandler handler, CancellationToken cancellationToken)
    {
        // A session cookie on the request means nothing here: no record may carry its user.
        context.User = new ClaimsPrincipal(new ClaimsIdentity());
        var request = context.Request;
        if (await ReadBodyAsync(request, cancellationToken) is not { } body)
        {
            await ApiProblem.WriteAsync(context, StatusCodes.Status413PayloadTooLarge, ErrorCodes.ValidationFailed);
            return;
        }

        var result = await handler.HandleAsync(
            new ReceiveMonitoringEventCommand(sourceId, request.Headers[TimestampHeader], request.Headers[SignatureHeader], body), cancellationToken);
        switch (result)
        {
            case MonitoringEventResult.Accepted:
                context.Response.StatusCode = StatusCodes.Status200OK;
                break;
            case MonitoringEventResult.Limited:
                await ApiProblem.WriteAsync(context, StatusCodes.Status429TooManyRequests, ErrorCodes.RateLimited);
                break;
            default:
                await ApiProblem.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.MonitoringUnauthorized);
                break;
        }
    }

    /// <summary>Null when the body is larger than allowed; reads at most one byte more, whatever the length header says.</summary>
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var buffer = new byte[MonitoringLimits.MaxBodyBytes + 1];
        var read = await request.Body.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);
        return read > MonitoringLimits.MaxBodyBytes ? null : buffer[..read];
    }
}
