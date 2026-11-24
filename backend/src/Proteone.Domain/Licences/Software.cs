using Proteone.Domain.Common;

namespace Proteone.Domain.Licences;

/// <summary>A software product the company uses (ADR-0039). Never deleted; an inactive one gets no new licence.</summary>
public sealed class Software : Entity
{
    private const string SearchSeparator = " ";

    private Software()
    {
    }

    public string Name { get; private set; } = string.Empty;
    public string Vendor { get; private set; } = string.Empty;

    /// <summary>Turkish-insensitive key of vendor and name: uniqueness and search.</summary>
    public string SearchText { get; private set; } = string.Empty;

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
        IsActive = isActive;
        UpdatedAt = now;
    }
}
