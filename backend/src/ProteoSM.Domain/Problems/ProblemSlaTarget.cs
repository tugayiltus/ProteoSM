using ProteoSM.Domain.Common;
using ProteoSM.Domain.Sla;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Domain.Problems;

/// <summary>
/// Business minutes a problem may take from creation to known error, per impact level. Editable in place: unlike
/// ticket targets there are no policy versions, because a running problem timer takes its own copy at start.
/// </summary>
public sealed class ProblemSlaTarget : Entity
{
    private const int HighTargetMinutes = 960;
    private const int MediumTargetMinutes = 2400;
    private const int LowTargetMinutes = 4800;
    private const int DefaultWarningPercent = 80;

    /// <summary>Seeded values: two, five and ten business days of eight hours.</summary>
    public static readonly IReadOnlyList<(Level Impact, int TargetMinutes, int WarningPercent)> Defaults =
    [
        (Level.High, HighTargetMinutes, DefaultWarningPercent),
        (Level.Medium, MediumTargetMinutes, DefaultWarningPercent),
        (Level.Low, LowTargetMinutes, DefaultWarningPercent),
    ];

    private ProblemSlaTarget()
    {
    }

    public Level Impact { get; private init; }

    public int TargetMinutes { get; private set; }

    public int WarningPercent { get; private set; }

    public static ProblemSlaTarget Create(Level impact, int targetMinutes, int warningPercent)
    {
        Validate(targetMinutes, warningPercent);
        return new ProblemSlaTarget { Impact = impact, TargetMinutes = targetMinutes, WarningPercent = warningPercent };
    }

    public void Change(int targetMinutes, int warningPercent)
    {
        Validate(targetMinutes, warningPercent);
        TargetMinutes = targetMinutes;
        WarningPercent = warningPercent;
    }

    // Same range as ticket targets so both screens accept the same input.
    private static void Validate(int targetMinutes, int warningPercent)
    {
        if (targetMinutes is <= 0 or > SlaTarget.MaxTargetMinutes)
        {
            throw new DomainException(ErrorCodes.InvalidValue, "A target must allow between one minute and one year.");
        }

        if (warningPercent is < SlaTarget.MinWarningPercent or > SlaTarget.MaxWarningPercent)
        {
            throw new DomainException(ErrorCodes.InvalidValue, "The warning percentage must be between 1 and 99.");
        }
    }
}
