using Proteone.Api.Http;
using Proteone.Api.Security;

namespace Proteone.Api.Endpoints.Admin;

internal static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder api)
    {
        var admin = api.MapGroup(ApiRoutes.AdminGroup).RequireAuthorization(Policies.Admin);
        admin.MapUserEndpoints();
        admin.MapDepartmentEndpoints();
        admin.MapLocationEndpoints();
        admin.MapTeamEndpoints();
        admin.MapServiceAdminEndpoints();
        admin.MapMonitoringSourceEndpoints();
        admin.MapSystemSettingsEndpoints();
        admin.MapAnnouncementAdminEndpoints();
        admin.MapCatalogEndpoints();
        admin.MapAuditEndpoints();
        admin.MapEmailTemplateEndpoints();
        admin.MapEmailDeliveryEndpoints();
        admin.MapSlaEndpoints();
        admin.MapAuthSettingsEndpoints();
        admin.MapEmailSettingsEndpoints();
        admin.MapSystemEndpoints();
        return admin;
    }
}
