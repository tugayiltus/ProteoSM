using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using ProteoSM.Application.Common;
using ProteoSM.Application.Features.Email.Settings;
using ProteoSM.Domain.Common;

namespace ProteoSM.Api.Http;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        switch (exception)
        {
            case OperationCanceledException when context.RequestAborted.IsCancellationRequested:
                // The client went away; there is nobody to answer and nothing went wrong on the server.
                logger.LogDebug("Request {Method} {Path} was cancelled by the client.", context.Request.Method, context.Request.Path);
                break;
            case ValidationException validation:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed, ValidationErrorMapper.ToFieldCodes(validation.Errors));
                break;
            case BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge }:
                await ApiProblem.WriteAsync(context, StatusCodes.Status413PayloadTooLarge, ErrorCodes.ValidationFailed);
                break;
            case BadHttpRequestException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.ValidationFailed);
                break;
            case DirectoryUnavailableException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status503ServiceUnavailable, ErrorCodes.AuthDirectoryUnavailable);
                break;
            case AuthenticationFailedException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status401Unauthorized, ErrorCodes.InvalidCredentials);
                break;
            case PasswordTokenInvalidException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, ErrorCodes.TokenInvalid);
                break;
            case NotFoundException notFound:
                await ApiProblem.WriteAsync(context, StatusCodes.Status404NotFound, notFound.Code);
                break;
            case GoneException gone:
                await ApiProblem.WriteAsync(context, StatusCodes.Status410Gone, gone.Code);
                break;
            case ForbiddenException forbidden:
                await ApiProblem.WriteAsync(context, StatusCodes.Status403Forbidden, forbidden.Code);
                break;
            case EmailSettingsTestFailedException testFailed:
                await ApiProblem.WriteAsync(context, StatusCodes.Status409Conflict, testFailed.Code, testDetail: testFailed.Detail);
                break;
            case ConcurrencyException:
                await ApiProblem.WriteAsync(context, StatusCodes.Status409Conflict, ErrorCodes.ConcurrencyConflict);
                break;
            case DomainException { Code: ErrorCodes.RatingNotRequester } notRequester:
                await ApiProblem.WriteAsync(context, StatusCodes.Status403Forbidden, notRequester.Code);
                break;
            case DomainException { Code: ErrorCodes.ChangeSelfApproval } selfApproval:
                await ApiProblem.WriteAsync(context, StatusCodes.Status403Forbidden, selfApproval.Code);
                break;
            case DomainException { Code: ErrorCodes.ChangeTicketLinkKindInvalid } linkKind:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, linkKind.Code);
                break;
            case DomainException { Code: ErrorCodes.ChangeCalendarRange } calendarRange:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, calendarRange.Code);
                break;
            case DomainException { Code: ErrorCodes.ProblemResolutionRequired or ErrorCodes.ProblemCancelReasonRequired } required:
                await ApiProblem.WriteAsync(context, StatusCodes.Status400BadRequest, required.Code);
                break;
            case DomainException domain:
                await ApiProblem.WriteAsync(context, StatusCodes.Status409Conflict, domain.Code);
                break;
            default:
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                await ApiProblem.WriteAsync(context, StatusCodes.Status500InternalServerError, ErrorCodes.Unexpected);
                break;
        }

        return true;
    }
}
