using ProteoSM.Api.Http;
using ProteoSM.Api.Security;
using ProteoSM.Application.Features.Approvals;

namespace ProteoSM.Api.Endpoints.Tickets;

public sealed record ApprovalDecisionRequest(uint Version, string? Reason);

internal static class ApprovalEndpoints
{
    private const string Tag = "Approvals";

    public static RouteGroupBuilder MapApprovalEndpoints(this RouteGroupBuilder api)
    {
        var approvals = api.MapGroup(ApiRoutes.Approvals).WithTags(Tag).RequireAuthorization(Policies.Admin);

        approvals.MapGet("/mine", async (ListMyApprovalsHandler h, CancellationToken ct) => TypedResults.Ok(await h.HandleAsync(ct)));
        approvals.MapPost("/{id:guid}/approve", async (Guid id, ApprovalDecisionRequest r, DecideApprovalHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new DecideApprovalCommand(id, r.Version, Approve: true, null), ct)))
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        approvals.MapPost("/{id:guid}/reject", async (Guid id, ApprovalDecisionRequest r, DecideApprovalHandler h, CancellationToken ct) =>
            TypedResults.Ok(await h.HandleAsync(new DecideApprovalCommand(id, r.Version, Approve: false, r.Reason), ct)))
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);

        return approvals;
    }
}
