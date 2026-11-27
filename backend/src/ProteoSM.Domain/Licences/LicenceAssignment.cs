using ProteoSM.Domain.Common;

namespace ProteoSM.Domain.Licences;

/// <summary>One seat in use: exactly one of user and asset. Released rows stay as history.</summary>
public sealed class LicenceAssignment
{
    private LicenceAssignment()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid LicenceId { get; private init; }
    public Guid? UserId { get; private init; }
    public Guid? AssetId { get; private init; }
    public DateTimeOffset AssignedAt { get; private init; }
    public Guid AssignedBy { get; private init; }
    public DateTimeOffset? ReleasedAt { get; private set; }
    public Guid? ReleasedBy { get; private set; }

    internal static LicenceAssignment Create(Guid licenceId, Guid? userId, Guid? assetId, Guid actorId, DateTimeOffset now) =>
        new() { LicenceId = licenceId, UserId = userId, AssetId = assetId, AssignedBy = actorId, AssignedAt = StoredTime.TruncateToMicroseconds(now) };

    public void Release(Guid actorId, DateTimeOffset now)
    {
        if (ReleasedAt is null)
        {
            ReleasedAt = StoredTime.TruncateToMicroseconds(now);
            ReleasedBy = actorId;
        }
    }
}
