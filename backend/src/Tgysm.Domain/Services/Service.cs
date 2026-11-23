using Tgysm.Domain.Common;

namespace Tgysm.Domain.Services;

/// <summary>An IT service people depend on (Phase 12). Never deleted; an inactive service keeps its records but cannot be newly set.</summary>
public sealed class Service : Entity
{
    private const string SearchSeparator = " ";

    private Service()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Turkish-insensitive uniqueness key of the name (<see cref="TurkishText.ToSearchKey"/>).</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }
    public Guid OwnerTeamId { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>Search key of name and description, served by a trigram index.</summary>
    public string SearchText { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Service Create(string name, string? description, Guid ownerTeamId, DateTimeOffset now)
    {
        var service = new Service { IsActive = true, CreatedAt = now };
        service.Update(name, description, ownerTeamId, now);
        return service;
    }

    public void Update(string name, string? description, Guid ownerTeamId, DateTimeOffset now)
    {
        var cleanName = name?.Trim() ?? string.Empty;
        if (cleanName.Length == 0 || cleanName.Length > ServiceLimits.NameMaxLength)
        {
            throw new ArgumentException($"A service name needs 1 to {ServiceLimits.NameMaxLength} characters.", nameof(name));
        }

        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (cleanDescription is { Length: > ServiceLimits.DescriptionMaxLength })
        {
            throw new ArgumentException($"A service description has at most {ServiceLimits.DescriptionMaxLength} characters.", nameof(description));
        }

        Name = cleanName;
        NormalizedName = TurkishText.ToSearchKey(cleanName);
        Description = cleanDescription;
        OwnerTeamId = ownerTeamId;
        SearchText = TurkishText.ToSearchKey(cleanDescription is null ? cleanName : cleanName + SearchSeparator + cleanDescription);
        UpdatedAt = now;
    }

    public void SetActive(bool active, DateTimeOffset now)
    {
        IsActive = active;
        UpdatedAt = now;
    }
}
