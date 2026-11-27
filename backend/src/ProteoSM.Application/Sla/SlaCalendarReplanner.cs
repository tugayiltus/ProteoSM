using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Sla;

namespace ProteoSM.Application.Sla;

/// <summary>
/// Re-plans running SLA timers when the company calendar or a holiday changes (owner decision, final review F3):
/// elapsed business time up to now counts under the old rules, the remaining time is projected under the new ones.
/// Paused and stopped timers are left alone. Running problem timers are re-planned the same way. Callers run <see cref="RunAsync"/> as their whole unit of work, so the
/// calendar change, the recomputed instances and their <see cref="SlaEventKind.CalendarChanged"/> events commit in one save.
/// </summary>
public sealed class SlaCalendarReplanner(IAppDbContext db, CalendarProvider calendar, TimeProvider clock)
{
    /// <summary>
    /// In one transaction: locks every unstopped instance (so a concurrent ticket action's write waits and this run
    /// reads its committed state), reads the current rules, lets <paramref name="change"/> apply the calendar change
    /// and return the new rules, recomputes the running instances, and saves once through <paramref name="save"/>.
    /// </summary>
    public async Task RunAsync(
        Func<BusinessCalendarRules, CancellationToken, Task<BusinessCalendarRules>> change,
        Func<CancellationToken, Task> save,
        CancellationToken cancellationToken)
    {
        await db.RunInTransactionAsync(async ct =>
        {
            await db.SlaInstances
                .Where(i => i.StoppedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(i => i.RunningSince, i => i.RunningSince), ct);

            await db.ProblemSlaTimers
                .Where(t => t.StoppedAt == null)
                .ExecuteUpdateAsync(set => set.SetProperty(t => t.RunningSince, t => t.RunningSince), ct);

            var oldRules = await calendar.GetRulesAsync(ct);
            var newRules = await change(oldRules, ct);
            var now = clock.GetUtcNow();
            var running = await db.SlaInstances.Where(i => i.StoppedAt == null && i.RunningSince != null).ToListAsync(ct);
            foreach (var instance in running)
            {
                instance.ApplyCalendarChange(now, oldRules, newRules);
            }

            var problemTimers = await db.ProblemSlaTimers.Where(t => t.StoppedAt == null).ToListAsync(ct);
            foreach (var timer in problemTimers)
            {
                timer.ApplyCalendarChange(now, oldRules, newRules);
            }

            await save(ct);
        }, cancellationToken);
    }
}
