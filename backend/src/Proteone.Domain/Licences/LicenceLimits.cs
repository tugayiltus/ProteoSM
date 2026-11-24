namespace Proteone.Domain.Licences;

/// <summary>Limits of software and licence records (ADR-0039).</summary>
public static class LicenceLimits
{
    public const int NameMaxLength = 150;
    public const int VendorMaxLength = 100;
    public const int NotesMaxLength = 2000;
    public const int KeyMaxLength = 2000;
    public const int MinQuantity = 1;
    public const int MaxQuantity = 100_000;

    /// <summary>The owner team is told this many days before a licence ends.</summary>
    public const int ExpiryNoticeDays = 30;

    /// <summary>Search text is the vendor, one space and the name.</summary>
    public const int SearchTextMaxLength = VendorMaxLength + 1 + NameMaxLength;
}
