namespace ProteoSM.Api.Contracts;

public sealed record UpdateSoftwareRequest(uint Version, string Name, string Vendor, bool IsActive);

public sealed record UpdateLicenceRequest(uint Version, string Name, int Quantity, Guid OwnerTeamId, DateOnly? ExpiresOn, string? Notes);

public sealed record AssignLicenceRequest(Guid? UserId, Guid? AssetId);

public sealed record SetLicenceKeyRequest(uint Version, string? Key)
{
    // A record prints its members; the key must not reach a log.
    public override string ToString() => nameof(SetLicenceKeyRequest);
}
