using Proteone.Application.Abstractions;
using Proteone.Domain.Problems;
using Proteone.Domain.Sla;

namespace Proteone.Application.Reports;

/// <summary>
/// Applies the problem-related parts of a <see cref="ReportFilter"/>: the owner team and the day window. The window dates a problem by
/// its creation (<see cref="ReportDateBasis.Created"/>), its resolution (<see cref="ReportDateBasis.Completed"/>), or not at all
/// (<see cref="ReportDateBasis.None"/>, a snapshot); <see cref="ReportDateBasis.SlaStopped"/> keeps problems whose known-error timer ended in the window.
/// </summary>
public static class ProblemReportSource
{
    public static IQueryable<Problem> Apply(IQueryable<Problem> problems, ReportFilter filter, ReportDateBasis basis, ReportWindow window, IAppDbContext db)
    {
        if (basis == ReportDateBasis.SlaStopped)
        {
            problems = InStopWindow(problems, window, db);
        }
        else if (basis != ReportDateBasis.None)
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

    /// <summary>A cancelled timer has no verdict, so it dates nothing and is left out of both sides of the ratio.</summary>
    private static IQueryable<Problem> InStopWindow(IQueryable<Problem> problems, ReportWindow window, IAppDbContext db)
    {
        var timers = db.ProblemSlaTimers.Where(i => i.Result == SlaResult.Met || i.Result == SlaResult.Breached);
        timers = window.FromInclusive is { } start ? timers.Where(i => i.StoppedAt >= start) : timers;
        timers = window.ToExclusive is { } end ? timers.Where(i => i.StoppedAt < end) : timers;
        return problems.Where(p => timers.Any(i => i.ProblemId == p.Id));
    }
}
