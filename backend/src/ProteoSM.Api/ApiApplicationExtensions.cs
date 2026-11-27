using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using ProteoSM.Api.Endpoints;
using ProteoSM.Api.Endpoints.Admin;
using ProteoSM.Api.Endpoints.Assets;
using ProteoSM.Api.Endpoints.Changes;
using ProteoSM.Api.Endpoints.Problems;
using ProteoSM.Api.Endpoints.Releases;
using ProteoSM.Api.Endpoints.Services;
using ProteoSM.Api.Endpoints.Licences;
using ProteoSM.Api.Endpoints.Tasks;
using ProteoSM.Api.Endpoints.Tickets;
using ProteoSM.Api.Http;
using ProteoSM.Api.Security;
using ProteoSM.Domain.Common;

namespace ProteoSM.Api;

internal static class ApiApplicationExtensions
{
    private const string SpaIndexFile = "index.html";
    private const string AdminIndexFile = "admin/index.html";
    private const string AdminPagesPattern = "/admin/{*path:nonfile}";
    private const string ApiFallbackPattern = ApiRoutes.ApiPrefix + "/{**path}";
    private const string TestingEnvironment = "Testing";

    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseMiddleware<CorrelationIdMiddleware>();
        if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment(TestingEnvironment))
        {
            // Local dev: the API runs on plain HTTP behind the Vite proxy, which sends X-Forwarded-Proto: https.
            // Default ForwardedHeadersOptions trust loopback proxies only.
            app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto });
        }
        else
        {
            // Behind nginx (ADR-0024): client address and scheme come from the trusted proxy networks only.
            var trust = app.Services.GetRequiredService<IOptions<ProxyTrustOptions>>().Value;
            app.UseForwardedHeaders(ProxyTrust.CreateForwardedHeadersOptions(trust));
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseDefaultFiles(new DefaultFilesOptions { RedirectToAppendTrailingSlash = false });
        app.UseStaticFiles();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseMiddleware<CsrfValidationMiddleware>();
        app.UseAuthorization();
        return app;
    }

    public static RouteGroupBuilder MapApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
        }

        var api = app.MapGroup(ApiRoutes.V1);
        api.MapAuthEndpoints();
        api.MapHealthEndpoints();
        api.MapAdminEndpoints();
        api.MapAssetEndpoints();
        api.MapMyAssetEndpoints();
        api.MapAssetImportEndpoints();
        api.MapReferenceEndpoints();
        api.MapTicketEndpoints();
        api.MapTaskEndpoints();
        api.MapProblemEndpoints();
        api.MapServiceEndpoints();
        api.MapLicenceEndpoints();
        api.MapReleaseEndpoints();
        api.MapMonitoringEndpoints();
        api.MapAnnouncementEndpoints();
        api.MapChangeEndpoints();
        api.MapApprovalEndpoints();
        api.MapKnowledgeEndpoints();
        api.MapSearchEndpoints();
        api.MapReportEndpoints();

        // Unknown API routes are API errors, not SPA pages.
        app.MapFallback(ApiFallbackPattern, context => ApiProblem.WriteAsync(context, StatusCodes.Status404NotFound, ErrorCodes.NotFound))
            .AllowAnonymous();

        // Production serves both entry pages of the build from wwwroot (ADR-0003, ADR-0038). The pages hold no data: every
        // admin endpoint checks the role itself.
        app.MapFallbackToFile(AdminPagesPattern, AdminIndexFile).AllowAnonymous();
        app.MapFallbackToFile(SpaIndexFile).AllowAnonymous();
        return api;
    }
}
