using ProteoSM.Domain.Assets;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Users;

namespace ProteoSM.Domain.Licences;

/// <summary>A number of seats of one software product (ADR-0039). The seat count is checked by the caller under a lock.</summary>
public sealed class Licence : Entity
{
    private Licence()
    {
    }

    public Guid SoftwareId { get; private init; }
    public string Name { get; private set; } = string.Empty;

    /// <summary>Turkish-insensitive key of the name, for search.</summary>
    public string SearchText { get; private set; } = string.Empty;

    public LicenceKind Kind { get; private init; }
    public int Quantity { get; private set; }
    public Guid OwnerTeamId { get; private set; }
    public DateOnly? ExpiresOn { get; private set; }
    public string? Notes { get; private set; }

    /// <summary>The licence key, protected with Data Protection; never leaves the server except through the audited reveal.</summary>
    public string? ProtectedKey { get; private set; }

    /// <summary>The end date the owner team was already told about; a changed date is told again.</summary>
    public DateOnly? ExpiryNotifiedFor { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Licence Create(
        Guid softwareId, string name, LicenceKind kind, int quantity, Guid ownerTeamId, DateOnly? expiresOn, string? notes, DateTimeOffset now)
    {
        var licence = new Licence { SoftwareId = softwareId, Kind = kind, CreatedAt = now };
        licence.Update(name, quantity, ownerTeamId, expiresOn, notes, usedSeats: 0, now);
        return licence;
    }

    public void Update(string name, int quantity, Guid ownerTeamId, DateOnly? expiresOn, string? notes, int usedSeats, DateTimeOffset now)
    {
        if (quantity is < LicenceLimits.MinQuantity or > LicenceLimits.MaxQuantity)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity));
        }

        if (quantity < usedSeats)
        {
            throw new DomainException(ErrorCodes.LicenceQuantityBelowUse, "The quantity is lower than the number of used seats.");
        }

        Name = LicenceText.Required(name, LicenceLimits.NameMaxLength, nameof(name));
        SearchText = TurkishText.ToSearchKey(Name);
        Quantity = quantity;
        OwnerTeamId = ownerTeamId;
        ExpiresOn = expiresOn;
        Notes = LicenceText.Optional(notes, LicenceLimits.NotesMaxLength, nameof(notes));
        UpdatedAt = now;
    }

    public void SetProtectedKey(string? protectedKey, DateTimeOffset now)
    {
        ProtectedKey = protectedKey;
        UpdatedAt = now;
    }

    public void MarkExpiryNotified() => ExpiryNotifiedFor = ExpiresOn;

    public bool IsExpired(DateOnly today) => ExpiresOn < today;

    public LicenceAssignment AssignTo(User user, int usedSeats, DateOnly today, Guid actorId, DateTimeOffset now)
    {
        RequireSeat(LicenceKind.PerUser, usedSeats, today);
        return user.IsActive
            ? LicenceAssignment.Create(Id, user.Id, null, actorId, now)
            : throw new DomainException(ErrorCodes.LicenceTargetNotAssignable, "An inactive user cannot get a licence.");
    }

    public LicenceAssignment AssignTo(Asset asset, int usedSeats, DateOnly today, Guid actorId, DateTimeOffset now)
    {
        RequireSeat(LicenceKind.PerDevice, usedSeats, today);
        return asset.Status is not (AssetStatus.Retired or AssetStatus.Lost)
            ? LicenceAssignment.Create(Id, null, asset.Id, actorId, now)
            : throw new DomainException(ErrorCodes.LicenceTargetNotAssignable, "A retired or lost asset cannot get a licence.");
    }

    private void RequireSeat(LicenceKind kind, int usedSeats, DateOnly today)
    {
        if (Kind != kind)
        {
            throw new DomainException(ErrorCodes.LicenceKindMismatch, "The licence is for the other kind of target.");
        }

        if (IsExpired(today))
        {
            throw new DomainException(ErrorCodes.LicenceExpired, "The licence has ended.");
        }

        if (usedSeats >= Quantity)
        {
            throw new DomainException(ErrorCodes.LicenceFull, "Every seat of the licence is used.");
        }
    }
}
