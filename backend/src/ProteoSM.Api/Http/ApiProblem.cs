using Microsoft.AspNetCore.Mvc;

namespace ProteoSM.Api.Http;

/// <summary>
/// RFC 7807 body with a stable <c>code</c>, optional per-field <c>errors</c> (field → error codes) and, for a refused
/// save of active mail settings, the redacted server message in <c>testDetail</c>.
/// </summary>
internal static class ApiProblem
{
    public const string ContentType = "application/problem+json";
    public const string CodeKey = "code";
    public const string ErrorsKey = "errors";
    public const string TestDetailKey = "testDetail";

    public static ProblemDetails Create(int status, string code, IDictionary<string, string[]>? errors = null, string? testDetail = null)
    {
        var problem = new ProblemDetails { Status = status, Title = code };
        problem.Extensions[CodeKey] = code;
        if (errors is not null)
        {
            problem.Extensions[ErrorsKey] = errors;
        }

        if (testDetail is not null)
        {
            problem.Extensions[TestDetailKey] = testDetail;
        }

        return problem;
    }

    public static Task WriteAsync(HttpContext context, int status, string code, IDictionary<string, string[]>? errors = null, string? testDetail = null)
    {
        context.Response.StatusCode = status;
        return context.Response.WriteAsJsonAsync(Create(status, code, errors, testDetail), options: null, contentType: ContentType, context.RequestAborted);
    }
}
