using Proteone.Domain.Common;
using Proteone.Domain.Problems;
using Proteone.Domain.Sla;
using Proteone.Domain.Tickets;

namespace Proteone.Domain.Tests.Problems;

public sealed class ProblemSlaTargetTests
{
    private const int ValidMinutes = 960;
    private const int ValidPercent = 80;
    private const int ChangedMinutes = 2400;
    private const int ChangedPercent = 50;

    [Fact]
    public void Create_keeps_values()
    {
        var target = ProblemSlaTarget.Create(Level.High, ValidMinutes, ValidPercent);

        Assert.Equal(Level.High, target.Impact);
        Assert.Equal(ValidMinutes, target.TargetMinutes);
        Assert.Equal(ValidPercent, target.WarningPercent);
    }

    [Fact]
    public void Change_replaces_values()
    {
        var target = ProblemSlaTarget.Create(Level.Low, ValidMinutes, ValidPercent);

        target.Change(ChangedMinutes, ChangedPercent);

        Assert.Equal(ChangedMinutes, target.TargetMinutes);
        Assert.Equal(ChangedPercent, target.WarningPercent);
    }

    [Fact]
    public void Defaults_cover_every_impact_level_once()
    {
        Assert.Equal(
            [(Level.High, 960, 80), (Level.Medium, 2400, 80), (Level.Low, 4800, 80)],
            ProblemSlaTarget.Defaults.Select(d => (d.Impact, d.TargetMinutes, d.WarningPercent)).ToArray());
    }

    [Theory]
    [InlineData(0, ValidPercent)]
    [InlineData(-1, ValidPercent)]
    [InlineData(SlaTarget.MaxTargetMinutes + 1, ValidPercent)]
    [InlineData(ValidMinutes, SlaTarget.MinWarningPercent - 1)]
    [InlineData(ValidMinutes, SlaTarget.MaxWarningPercent + 1)]
    public void ProblemSlaTarget_rejects_zero_minutes_and_out_of_range_percent(int minutes, int percent)
    {
        var create = Assert.Throws<DomainException>(() => ProblemSlaTarget.Create(Level.High, minutes, percent));
        Assert.Equal(ErrorCodes.InvalidValue, create.Code);

        var target = ProblemSlaTarget.Create(Level.High, ValidMinutes, ValidPercent);
        var change = Assert.Throws<DomainException>(() => target.Change(minutes, percent));
        Assert.Equal(ErrorCodes.InvalidValue, change.Code);
        Assert.Equal(ValidMinutes, target.TargetMinutes);
    }
}
