using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Proteone.Api.Endpoints;
using Proteone.Api.Endpoints.Admin;
using Proteone.Api.Endpoints.Assets;
using Proteone.Api.Endpoints.Changes;
using Proteone.Api.Endpoints.Problems;
using Proteone.Api.Endpoints.Services;
using Proteone.Api.Endpoints.Tasks;
using Proteone.Api.Endpoints.Tickets;
using Proteone.Api.Http;
using Proteone.Api.Security;
using Proteone.Domain.Common;

namespace Proteone.Api;

internal static class ApiApplicationExtensions
{
    private const string SpaIndexFile = "index.html";
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

        app.UseDefaultFiles();
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
        api.MapChangeEndpoints();
        api.MapApprovalEndpoints();
        api.MapKnowledgeEndpoints();
        api.MapSearchEndpoints();
        api.MapReportEndpoints();

        // Unknown API routes are API errors, not SPA pages.
        app.MapFallback(ApiFallbackPattern, context => ApiProblem.WriteAsync(context, StatusCodes.Status404NotFound, ErrorCodes.NotFound))
            .AllowAnonymous();

        // Production serves the built SPA from wwwroot (ADR-0003).
        app.MapFallbackToFile(SpaIndexFile).AllowAnonymous();
        return api;
    }
}
