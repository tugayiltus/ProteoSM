using Proteone.Api.Http;
using Proteone.Application.Features.Settings;

namespace Proteone.Api.Endpoints.Admin;

public sealed record UpdateSystemSettingsRequest(int LicenceNoticeDays, int WarrantyWindowDays, uint Version);

/// <summary>Company-wide settings (ADR-0042); the admin group checks the role.</summary>
internal static class SystemSettingsEndpoints
{
    private const string Tag = "Admin: Settings";

    public static RouteGroupBuilder MapSystemSettingsEndpoints(this RouteGroupBuilder admin)
    {
        var settings = admin.MapGroup(ApiRoutes.SystemSettings).WithTags(Tag);
        settings.MapGet("", async (GetSystemSettingsHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        settings.MapPut("", async (UpdateSystemSettingsRequest r, UpdateSystemSettingsHandler h, CancellationToken ct) =>
                TypedResults.Ok(await h.HandleAsync(new UpdateSystemSettingsCommand(r.LicenceNoticeDays, r.WarrantyWindowDays, r.Version), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status409Conflict);
        return admin;
    }
}
