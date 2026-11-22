using Tgysm.Domain.Problems;

namespace Tgysm.Application.Reports;

/// <summary>
/// Applies the problem-related parts of a <see cref="ReportFilter"/>: the owner team and the day window. The window dates a problem by
/// its creation (<see cref="ReportDateBasis.Created"/>), its resolution (<see cref="ReportDateBasis.Completed"/>), or not at all
/// (<see cref="ReportDateBasis.None"/>, a snapshot).
/// </summary>
public static class ProblemReportSource
{
    public static IQueryable<Problem> Apply(IQueryable<Problem> problems, ReportFilter filter, ReportDateBasis basis, ReportWindow window)
    {
        if (basis != ReportDateBasis.None)
        {
            var (from, to) = (window.FromInclusive, window.ToExclusive);
            var byCreation = basis == ReportDateBasis.Created;
            if (from is { } start)
            {
                problems = byCreation ? problems.Where(p => p.CreatedAt >= start) : problems.Where(p => p.ResolvedAt >= start);
            }

            if (to is { } end)
            {
                problems = byCreation ? problems.Where(p => p.CreatedAt < end) : problems.Where(p => p.ResolvedAt < end);
            }
        }

        return filter.TeamId is { } teamId ? problems.Where(p => p.OwnerTeamId == teamId) : problems;
    }
}
