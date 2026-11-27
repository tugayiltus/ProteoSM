using ProteoSM.Domain.Common;

namespace ProteoSM.Domain.Settings;

/// <summary>The one row of company-wide settings an admin edits (ADR-0042); the migration inserts it.</summary>
public sealed class SystemSettings : Entity
{
    public const int DefaultLicenceNoticeDays = 30;
    public const int DefaultWarrantyWindowDays = 90;
    public const int MinDays = 1;
    public const int MaxDays = 365;

    private SystemSettings()
    {
    }

    /// <summary>The owner team is told this many days before a licence ends.</summary>
    public int LicenceNoticeDays { get; private set; } = DefaultLicenceNoticeDays;

    /// <summary>Days ahead the warranty report looks when no end date is chosen.</summary>
    public int WarrantyWindowDays { get; private set; } = DefaultWarrantyWindowDays;

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(int licenceNoticeDays, int warrantyWindowDays, DateTimeOffset now)
    {
        RequireInRange(licenceNoticeDays);
        RequireInRange(warrantyWindowDays);
        LicenceNoticeDays = licenceNoticeDays;
        WarrantyWindowDays = warrantyWindowDays;
        UpdatedAt = now;
    }

    private static void RequireInRange(int days)
    {
        if (days is < MinDays or > MaxDays)
        {
            throw new ArgumentOutOfRangeException(nameof(days), days, "The number of days is out of range.");
        }
    }
}
