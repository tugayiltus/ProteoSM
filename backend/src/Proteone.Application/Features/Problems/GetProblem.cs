namespace Proteone.Application.Features.Problems;

public sealed record GetProblemQuery(string Number);

public sealed class GetProblemHandler(ProblemScope scope, ProblemWork work)
{
    public async Task<ProblemDetail> HandleAsync(GetProblemQuery query, CancellationToken cancellationToken)
    {
        var (problem, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        return await work.DetailAsync(problem.Id, viewer, cancellationToken);
    }
}
