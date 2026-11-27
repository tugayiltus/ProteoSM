using Microsoft.AspNetCore.Http.HttpResults;
using Proteone.Api.Http;
using Proteone.Application.Abstractions;
using Proteone.Application.Features.Email.Settings;
using Proteone.Domain.Email;

namespace Proteone.Api.Endpoints.Admin;

/// <summary>A null or empty password keeps the stored one.</summary>
public sealed record UpdateEmailSettingsRequest(
    EmailProvider Provider,
    string? SenderName,
    string SenderAddress,
    string Host,
    int Port,
    EmailSecurity Security,
    string? UserName,
    string? Password,
    uint Version);

public sealed record EmailSourceRequest(uint Version);

/// <summary>Outgoing mail settings of the panel; admin only through the admin group.</summary>
internal static class EmailSettingsEndpoints
{
    private const string Tag = "Admin: Email sending";

    public static RouteGroupBuilder MapEmailSettingsEndpoints(this RouteGroupBuilder admin)
    {
        var settings = admin.MapGroup(ApiRoutes.EmailSettings).WithTags(Tag);
        settings.MapGet("", async (GetEmailSettingsHandler handler, CancellationToken ct) => TypedResults.Ok(await handler.HandleAsync(ct)));
        settings.MapPut("", UpdateAsync).ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        settings.MapPost("/test", async Task<Ok<EmailTestResult>> (TestEmailSettingsHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.HandleAsync(ct)))
            .ProducesProblem(StatusCodes.Status409Conflict);
        settings.MapPost("/activate", async Task<Ok<EmailSettingsDto>> (EmailSourceRequest request, SwitchEmailSourceHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.ActivateAsync(new ActivateEmailSettingsCommand(request.Version), ct)))
            .ProducesProblem(StatusCodes.Status409Conflict);
        settings.MapPost("/use-environment", async Task<Ok<EmailSettingsDto>> (EmailSourceRequest request, SwitchEmailSourceHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.UseEnvironmentAsync(new UseEnvironmentEmailCommand(request.Version), ct)))
            .ProducesProblem(StatusCodes.Status409Conflict);
        return admin;
    }

    private static async Task<Ok<EmailSettingsDto>> UpdateAsync(UpdateEmailSettingsRequest request, UpdateEmailSettingsHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(new UpdateEmailSettingsCommand(request.Provider, request.SenderName, request.SenderAddress, request.Host,
            request.Port, request.Security, request.UserName, request.Password, request.Version), ct));
}
