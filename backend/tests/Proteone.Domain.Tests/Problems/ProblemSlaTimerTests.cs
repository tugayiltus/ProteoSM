using Proteone.Domain.Problems;
using Proteone.Domain.Sla;

namespace Proteone.Domain.Tests.Problems;

public sealed class ProblemSlaTimerTests
{
    private const int TwoDaysMinutes = 960;
    private const int LongerMinutes = 2400;
    private const int WarningPercent = 80;
    private const int HighWarningPercent = 99;
    private const int OfficeOpenHour = 9;
    private const int OfficeCloseHour = 17;
    private const int ShortCloseHour = 13;
    private const int IstanbulOffsetHours = 3;

    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");
    private static readonly Guid ProblemId = Guid.NewGuid();

    // 2026-01-05 is a Monday; Istanbul is +03:00 all year.
    private static readonly DateOnly Monday = new(2026, 1, 5);
    private static readonly DateOnly Tuesday = new(2026, 1, 6);
    private static readonly DateOnly Wednesday = new(2026, 1, 7);

    private static readonly BusinessCalendarRules Rules = Calendar(OfficeCloseHour);

    private static BusinessCalendarRules Calendar(int closeHour)
    {
        var hours = new WorkingRange(new TimeOnly(OfficeOpenHour, 0), new TimeOnly(closeHour, 0));
        return new BusinessCalendarRules(
            Istanbul,
            new Dictionary<DayOfWeek, WorkingRange>
            {
                [DayOfWeek.Monday] = hours,
                [DayOfWeek.Tuesday] = hours,
                [DayOfWeek.Wednesday] = hours,
                [DayOfWeek.Thursday] = hours,
                [DayOfWeek.Friday] = hours,
            },
            new HashSet<DateOnly>());
    }

    private static DateTimeOffset At(DateOnly date, int hour, int minute = 0) =>
        new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, minute)), TimeSpan.FromHours(IstanbulOffsetHours)).ToUniversalTime();

    private static ProblemSlaTimer Start(DateTimeOffset now, int minutes = TwoDaysMinutes, int percent = WarningPercent) =>
        ProblemSlaTimer.Start(ProblemId, minutes, percent, now, Rules);

    [Fact]
    public void Start_sets_due_and_warn_from_business_time()
    {
        var timer = Start(At(Monday, OfficeOpenHour));

        Assert.Equal(ProblemId, timer.ProblemId);
        Assert.Equal(At(Monday, OfficeOpenHour), timer.StartedAt);
        Assert.Equal(At(Tuesday, OfficeCloseHour), timer.DueAt);
        // 80 % of 16 business hours is 12 h 48 min: 8 h on Monday, then 4 h 48 min on Tuesday.
        Assert.Equal(At(Tuesday, 13, 48), timer.WarnAt);
        Assert.Null(timer.WarnedAt);
        Assert.Null(timer.BreachedAt);
        Assert.False(timer.IsStopped);
        Assert.Null(timer.Result);
    }

    [Fact]
    public void Stop_before_due_is_met()
    {
        var timer = Start(At(Monday, OfficeOpenHour));

        timer.Stop(At(Tuesday, 12));

        Assert.True(timer.IsStopped);
        Assert.Equal(At(Tuesday, 12), timer.StoppedAt);
        Assert.Equal(SlaResult.Met, timer.Result);
    }

    [Fact]
    public void Stop_after_due_is_breached()
    {
        var timer = Start(At(Monday, OfficeOpenHour));

        timer.Stop(At(Wednesday, OfficeOpenHour));

        Assert.Equal(SlaResult.Breached, timer.Result);
    }

    [Fact]
    public void Stop_after_recorded_breach_is_breached_even_when_the_target_was_raised()
    {
        var timer = Start(At(Monday, OfficeOpenHour));
        timer.RecordBreach(At(Tuesday, OfficeCloseHour));
        timer.ChangeTarget(LongerMinutes, WarningPercent, At(Wednesday, OfficeOpenHour), Rules);

        timer.Stop(At(Wednesday, 10));

        Assert.Equal(SlaResult.Breached, timer.Result);
    }

    [Fact]
    public void Stop_twice_keeps_first_result()
    {
        var timer = Start(At(Monday, OfficeOpenHour));
        timer.Stop(At(Tuesday, 12));

        timer.Stop(At(Wednesday, OfficeOpenHour));

        Assert.Equal(SlaResult.Met, timer.Result);
        Assert.Equal(At(Tuesday, 12), timer.StoppedAt);
    }

    [Fact]
    public void Cancel_sets_cancelled()
    {
        var timer = Start(At(Monday, OfficeOpenHour));

        timer.Cancel(At(Monday, 12));

        Assert.True(timer.IsStopped);
        Assert.Equal(SlaResult.Cancelled, timer.Result);
        Assert.Equal(At(Monday, 12), timer.StoppedAt);
    }

    [Fact]
    public void Cancel_after_stop_is_a_no_op()
    {
        var timer = Start(At(Monday, OfficeOpenHour));
        timer.Stop(At(Tuesday, 12));

        timer.Cancel(At(Tuesday, 13));

        Assert.Equal(SlaResult.Met, timer.Result);
        Assert.Equal(At(Tuesday, 12), timer.StoppedAt);
    }

    [Fact]
    public void ChangeTarget_keeps_start_and_breach_and_resets_warning_below_threshold()
    {
        var timer = Start(At(Monday, OfficeOpenHour));
        timer.RecordWarning(At(Tuesday, 14));
        timer.RecordBreach(At(Tuesday, OfficeCloseHour));

        // 16 business hours used; 80 % of 40 h is 32 h, so the warning can fire again.
        timer.ChangeTarget(LongerMinutes, WarningPercent, At(Tuesday, OfficeCloseHour), Rules);

        Assert.Equal(At(Monday, OfficeOpenHour), timer.StartedAt);
        Assert.Equal(At(Tuesday, OfficeCloseHour), timer.BreachedAt);
        Assert.Null(timer.WarnedAt);
        Assert.Equal(LongerMinutes, timer.TargetMinutes);
        // 24 business hours left from Tuesday 17:00: Wednesday, Thursday, Friday 09:00 to 17:00 = 24 h.
        Assert.Equal(At(new DateOnly(2026, 1, 9), OfficeCloseHour), timer.DueAt);
    }

    [Fact]
    public void ChangeTarget_keeps_warning_when_threshold_is_already_used_up()
    {
        var timer = Start(At(Monday, OfficeOpenHour));
        timer.RecordWarning(At(Tuesday, 14));

        // 13 business hours are used; 1 % of the target is far less, so the warning stays recorded.
        timer.ChangeTarget(TwoDaysMinutes, SlaTarget.MinWarningPercent, At(Tuesday, 14), Rules);

        Assert.Equal(At(Tuesday, 14), timer.WarnedAt);
    }

    [Fact]
    public void ChangeTarget_on_stopped_timer_throws()
    {
        var timer = Start(At(Monday, OfficeOpenHour));
        timer.Stop(At(Tuesday, 12));

        Assert.Throws<InvalidOperationException>(() =>
            timer.ChangeTarget(LongerMinutes, HighWarningPercent, At(Tuesday, 13), Rules));
    }

    [Fact]
    public void ApplyCalendarChange_counts_elapsed_under_old_rules()
    {
        var timer = Start(At(Monday, OfficeOpenHour));
        var shortDays = Calendar(ShortCloseHour);

        // 8 h elapsed under the old rules; the remaining 8 h run on 4 h days: Tuesday and Wednesday, 09:00 to 13:00.
        timer.ApplyCalendarChange(At(Monday, OfficeCloseHour), Rules, shortDays);

        Assert.Equal(At(Wednesday, ShortCloseHour), timer.DueAt);
        Assert.Equal(At(Monday, OfficeOpenHour), timer.StartedAt);
    }

    [Fact]
    public void ApplyCalendarChange_on_stopped_timer_is_a_no_op()
    {
        var timer = Start(At(Monday, OfficeOpenHour));
        var due = timer.DueAt;
        timer.Stop(At(Tuesday, 12));

        timer.ApplyCalendarChange(At(Tuesday, 13), Rules, Calendar(ShortCloseHour));

        Assert.Equal(due, timer.DueAt);
    }

    [Fact]
    public void RecordWarning_and_RecordBreach_are_set_once()
    {
        var timer = Start(At(Monday, OfficeOpenHour));

        timer.RecordWarning(At(Tuesday, 14));
        timer.RecordWarning(At(Tuesday, 15));
        timer.RecordBreach(At(Tuesday, OfficeCloseHour));
        timer.RecordBreach(At(Wednesday, OfficeOpenHour));

        Assert.Equal(At(Tuesday, 14), timer.WarnedAt);
        Assert.Equal(At(Tuesday, OfficeCloseHour), timer.BreachedAt);
    }
}
