using Microsoft.AspNetCore.Antiforgery;
using Proteone.Api.Http;
using Proteone.Domain.Common;

namespace Proteone.Api.Security;

/// <summary>Requires a valid antiforgery token in <see cref="HeaderName"/> on every unsafe /api request (ADR-0004), except the signed monitoring webhook (ADR-0041).</summary>
internal sealed class CsrfValidationMiddleware(RequestDelegate next, IAntiforgery antiforgery)
{
    public const string HeaderName = "X-CSRF-TOKEN";

    public async Task InvokeAsync(HttpContext context)
    {
        if (RequiresValidation(context.Request) && context.GetEndpoint()?.Metadata.GetMetadata<SkipCsrfMetadata>() is null)
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                await ApiProblem.WriteAsync(context, StatusCodes.Status403Forbidden, ErrorCodes.CsrfInvalid);
                return;
            }
        }

        await next(context);
    }

    private static bool RequiresValidation(HttpRequest request) =>
        request.Path.StartsWithSegments(ApiRoutes.ApiPrefix)
        && !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
            || HttpMethods.IsOptions(request.Method) || HttpMethods.IsTrace(request.Method));
}
