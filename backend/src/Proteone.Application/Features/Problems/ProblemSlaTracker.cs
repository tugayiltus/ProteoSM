using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Sla;
using Proteone.Domain.Problems;

namespace Proteone.Application.Features.Problems;

/// <summary>
/// Keeps the known-error timer of a problem in step with its life. Every method only adds to the caller's unit of
/// work and never saves, so the timer commits together with the problem, its history and its audit entry.
/// A problem without a timer row (created before the feature) is left alone by every method but <see cref="StartAsync"/>.
/// </summary>
public sealed class ProblemSlaTracker(IAppDbContext db, CalendarProvider calendar)
{
    public async Task StartAsync(Problem problem, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var target = await db.ProblemSlaTargets.AsNoTracking().SingleAsync(t => t.Impact == problem.Impact, cancellationToken);
        var rules = await calendar.GetRulesAsync(cancellationToken);
        db.ProblemSlaTimers.Add(ProblemSlaTimer.Start(problem.Id, target.TargetMinutes, target.WarningPercent, now, rules));
    }

    /// <summary>Applies the target of the problem's current impact; a no-op without a running timer.</summary>
    public async Task RetargetAsync(Problem problem, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var timer = await db.ProblemSlaTimers.SingleOrDefaultAsync(t => t.ProblemId == problem.Id, cancellationToken);
        if (timer is null || timer.IsStopped)
        {
            return;
        }

        var target = await db.ProblemSlaTargets.AsNoTracking().SingleAsync(t => t.Impact == problem.Impact, cancellationToken);
        timer.ChangeTarget(target.TargetMinutes, target.WarningPercent, now, await calendar.GetRulesAsync(cancellationToken));
    }

    public async Task StopAsync(Guid problemId, DateTimeOffset now, CancellationToken cancellationToken) =>
        (await db.ProblemSlaTimers.SingleOrDefaultAsync(t => t.ProblemId == problemId, cancellationToken))?.Stop(now);

    public async Task CancelAsync(Guid problemId, DateTimeOffset now, CancellationToken cancellationToken) =>
        (await db.ProblemSlaTimers.SingleOrDefaultAsync(t => t.ProblemId == problemId, cancellationToken))?.Cancel(now);
}
