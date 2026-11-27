using ProteoSM.Domain.Common;

namespace ProteoSM.Domain.Licences;

/// <summary>A software product the company uses (ADR-0039). Never deleted; an inactive one gets no new licence.</summary>
public sealed class Software : Entity
{
    private const string SearchSeparator = " ";
    private const string KeySeparator = "\n";

    private Software()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string Vendor { get; private set; } = string.Empty;

    /// <summary>Turkish-insensitive key of vendor and name, for search.</summary>
    public string SearchText { get; private set; } = string.Empty;

    /// <summary>The same with a line break between the two: unique, so "A B" + "C" and "A" + "B C" stay two products.</summary>
    public string NormalizedKey { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Software Create(string name, string vendor, DateTimeOffset now)
    {
        var software = new Software { CreatedAt = now };
        software.Update(name, vendor, isActive: true, now);
        return software;
    }

    public static string SearchKey(string name, string vendor) => TurkishText.ToSearchKey(vendor.Trim() + SearchSeparator + name.Trim());

    public void Update(string name, string vendor, bool isActive, DateTimeOffset now)
    {
        Name = LicenceText.Required(name, LicenceLimits.NameMaxLength, nameof(name));
        Vendor = LicenceText.Required(vendor, LicenceLimits.VendorMaxLength, nameof(vendor));
        SearchText = SearchKey(Name, Vendor);
        NormalizedKey = TurkishText.ToSearchKey(Vendor) + KeySeparator + TurkishText.ToSearchKey(Name);
        IsActive = isActive;
        UpdatedAt = now;
    }
}
