using Proteone.Domain.Licences;

namespace Proteone.Application.Features.Licences;

public sealed record SoftwareItem(Guid Id, string Name, string Vendor, bool IsActive, int LicenceCount, uint Version);

public sealed record LicenceListItem(
    Guid Id, string Name, Guid SoftwareId, string SoftwareName, string Vendor, LicenceKind Kind, int Quantity, int Used,
    Guid OwnerTeamId, string OwnerTeamName, DateOnly? ExpiresOn, bool IsExpired);

/// <summary>The key itself is never part of a detail; <paramref name="HasKey"/> only says that one is stored.</summary>
public sealed record LicenceDetail(
    Guid Id, string Name, Guid SoftwareId, string SoftwareName, string Vendor, LicenceKind Kind, int Quantity, int Used,
    Guid OwnerTeamId, string OwnerTeamName, DateOnly? ExpiresOn, bool IsExpired, string? Notes, bool HasKey, uint Version);

/// <summary>One seat. <paramref name="TargetAvailable"/> is false when the user was deactivated or the asset retired or lost after the assignment.</summary>
public sealed record LicenceAssignmentItem(
    Guid Id, Guid? UserId, string? UserName, Guid? AssetId, string? AssetTag, bool TargetAvailable,
    DateTimeOffset AssignedAt, DateTimeOffset? ReleasedAt);

public sealed record AssetLicenceItem(Guid LicenceId, string LicenceName, string SoftwareName, string Vendor, DateOnly? ExpiresOn);

public sealed record MySoftwareItem(string Name, string Vendor);

public sealed record LicenceKeyResult(string Key)
{
    // A record prints its members; the key must not reach a log.
    public override string ToString() => nameof(LicenceKeyResult);
}
