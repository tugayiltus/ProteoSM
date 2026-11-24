namespace Proteone.Domain.Services;

/// <summary>Length limits of a service (Phase 12).</summary>
public static class ServiceLimits
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 1000;

    /// <summary>Search text is the name, one space and the description.</summary>
    public const int SearchTextMaxLength = NameMaxLength + 1 + DescriptionMaxLength;
}
