using Proteone.Domain.Common;
using Proteone.Domain.Sla;

namespace Proteone.Domain.Problems;

/// <summary>
/// Runs from problem creation until the problem becomes a known error or is closed or rejected. Unlike
/// <see cref="SlaInstance"/> it never pauses: <see cref="RunningSince"/> marks the start of the stretch not yet added
/// to <see cref="ElapsedSeconds"/>, which only moves when the target or the calendar changes.
/// </summary>
public sealed class ProblemSlaTimer : Entity
{
    private const int SecondsPerMinute = 60;
    private const int PercentBase = 100;

    private ProblemSlaTimer()
    {
    }

    public Guid ProblemId { get; private init; }

    public int TargetMinutes { get; private set; }

    public int WarningPercent { get; private set; }

    public DateTimeOffset StartedAt { get; private init; }

    /// <summary>Business seconds used before <see cref="RunningSince"/>; needed to re-plan after a target or calendar change.</summary>
    public long ElapsedSeconds { get; private set; }

    public DateTimeOffset RunningSince { get; private set; }

    public DateTimeOffset DueAt { get; private set; }

    public DateTimeOffset WarnAt { get; private set; }

    public DateTimeOffset? WarnedAt { get; private set; }

    public DateTimeOffset? BreachedAt { get; private set; }

    public DateTimeOffset? StoppedAt { get; private set; }

    public SlaResult? Result { get; private set; }

    public bool IsStopped => StoppedAt is not null;

    public static ProblemSlaTimer Start(Guid problemId, int targetMinutes, int warningPercent, DateTimeOffset now, BusinessCalendarRules rules)
    {
        var timer = new ProblemSlaTimer
        {
            ProblemId = problemId,
            TargetMinutes = targetMinutes,
            WarningPercent = warningPercent,
            StartedAt = now,
        };
        timer.RunFrom(now, rules);
        return timer;
    }

    /// <summary>Ends the timer when the problem becomes a known error; a no-op when it already ended.</summary>
    public void Stop(DateTimeOffset now)
    {
        if (IsStopped)
        {
            return;
        }

        StoppedAt = now;
        Result = BreachedAt is not null || now > DueAt ? SlaResult.Breached : SlaResult.Met;
    }

    /// <summary>Ends the timer without a verdict (problem closed or rejected before a known error); a no-op when it already ended.</summary>
    public void Cancel(DateTimeOffset now)
    {
        if (IsStopped)
        {
            return;
        }

        StoppedAt = now;
        Result = SlaResult.Cancelled;
    }

    /// <summary>Applies a new target (impact change): start and used time are kept, an earlier breach stays recorded, and the warning mark resets while the new warning threshold is not yet used up.</summary>
    public void ChangeTarget(int targetMinutes, int warningPercent, DateTimeOffset now, BusinessCalendarRules rules)
    {
        if (IsStopped)
        {
            throw new InvalidOperationException("The problem SLA timer is already stopped.");
        }

        TargetMinutes = targetMinutes;
        WarningPercent = warningPercent;
        ElapsedSeconds += BusinessTimeCalculator.SecondsBetween(rules, RunningSince, now);
        RunFrom(now, rules);
        ResetWarningWhileBelowThreshold();
    }

    /// <summary>
    /// Re-plans the timer after a calendar or holiday change: business time up to <paramref name="now"/> is counted
    /// under <paramref name="oldRules"/>, the rest of the target is projected under <paramref name="newRules"/>.
    /// A stopped timer is left alone.
    /// </summary>
    public void ApplyCalendarChange(DateTimeOffset now, BusinessCalendarRules oldRules, BusinessCalendarRules newRules)
    {
        if (IsStopped)
        {
            return;
        }

        ElapsedSeconds += BusinessTimeCalculator.SecondsBetween(oldRules, RunningSince, now);
        RunFrom(now, newRules);
        ResetWarningWhileBelowThreshold();
    }

    /// <summary>Business seconds left to the target at <paramref name="now"/>: negative once overdue, null once stopped.</summary>
    public long? RemainingSeconds(DateTimeOffset now, BusinessCalendarRules rules) =>
        IsStopped
            ? null
            : (long)TargetMinutes * SecondsPerMinute - ElapsedSeconds - BusinessTimeCalculator.SecondsBetween(rules, RunningSince, now);

    /// <summary>Records the warning threshold being reached; set once.</summary>
    public void RecordWarning(DateTimeOffset now) => WarnedAt ??= now;

    /// <summary>Records the due time being passed; set once, and kept when the target is later raised.</summary>
    public void RecordBreach(DateTimeOffset now) => BreachedAt ??= now;

    private void ResetWarningWhileBelowThreshold()
    {
        if (ElapsedSeconds < (long)TargetMinutes * SecondsPerMinute * WarningPercent / PercentBase)
        {
            // The new threshold is not yet used up, so the timer can warn again on the new plan.
            WarnedAt = null;
        }
    }

    private void RunFrom(DateTimeOffset now, BusinessCalendarRules rules)
    {
        RunningSince = now;
        DueAt = Moment(now, (long)TargetMinutes * SecondsPerMinute, rules);
        WarnAt = Moment(now, (long)TargetMinutes * SecondsPerMinute * WarningPercent / PercentBase, rules);
    }

    // The moment the given share of the target is used up; not before "since" when it is already used up.
    private DateTimeOffset Moment(DateTimeOffset since, long shareSeconds, BusinessCalendarRules rules)
    {
        var remaining = shareSeconds - ElapsedSeconds;
        return remaining <= 0 ? since : BusinessTimeCalculator.AddSeconds(rules, since, remaining);
    }
}
