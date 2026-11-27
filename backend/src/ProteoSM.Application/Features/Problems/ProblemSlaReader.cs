using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Sla;

namespace ProteoSM.Application.Features.Problems;

/// <summary>Builds the <c>sla</c> block for many problems at once: timers and calendar rules are loaded once per call (no N+1).</summary>
public sealed class ProblemSlaReader(IAppDbContext db, CalendarProvider calendar, TimeProvider clock)
{
    public async Task<IReadOnlyDictionary<Guid, ProblemSlaDto>> LoadAsync(IReadOnlyCollection<Guid> problemIds, CancellationToken cancellationToken)
    {
        if (problemIds.Count == 0)
        {
            return new Dictionary<Guid, ProblemSlaDto>();
        }

        var timers = await db.ProblemSlaTimers.AsNoTracking().Where(t => problemIds.Contains(t.ProblemId)).ToListAsync(cancellationToken);
        var rules = timers.Any(t => !t.IsStopped) ? await calendar.GetRulesAsync(cancellationToken) : null;
        var now = clock.GetUtcNow();
        return timers.ToDictionary(
            t => t.ProblemId,
            t => new ProblemSlaDto(
                ProblemSlaDto.KnownErrorMetric, t.DueAt, rules is null ? null : t.RemainingSeconds(now, rules),
                IsPaused: false, IsWarned: t.WarnedAt is not null, IsBreached: t.BreachedAt is not null, t.Result));
    }
}
