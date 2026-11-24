using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Licences;

namespace Proteone.Application.Features.Licences;

/// <summary>An empty key clears the stored one.</summary>
public sealed record SetLicenceKeyCommand(Guid LicenceId, uint Version, string? Key)
{
    // A record prints its members; the key must not reach a log or an error message.
    public override string ToString() => nameof(SetLicenceKeyCommand);
}

internal sealed class SetLicenceKeyValidator : AbstractValidator<SetLicenceKeyCommand>
{
    public SetLicenceKeyValidator()
    {
        RuleFor(c => c.Key).MaximumLength(LicenceLimits.KeyMaxLength);
    }
}

/// <summary>Stores or clears the protected key. The audit row says that it changed, never what it is.</summary>
public sealed class SetLicenceKeyHandler(
    IAppDbContext db, ICurrentActor actor, IValidator<SetLicenceKeyCommand> validator, ILicenceKeyProtector protector, IAuditLog audit,
    ICompanyClock companyClock, TimeProvider clock)
{
    public async Task<LicenceDetail> HandleAsync(SetLicenceKeyCommand command, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureAdminAsync(db, actor, cancellationToken);
        await validator.ValidateAndThrowAsync(command, cancellationToken);
        var now = clock.GetUtcNow();
        var licence = await LicenceQueries.LoadAsync(db, command.LicenceId, cancellationToken);
        db.SetExpectedVersion(licence, command.Version);
        var key = string.IsNullOrWhiteSpace(command.Key) ? null : command.Key.Trim();
        if (key is not null || licence.ProtectedKey is not null)
        {
            licence.SetProtectedKey(key is null ? null : protector.Protect(key), now);
            audit.Record(key is null ? AuditActions.LicenceKeyCleared : AuditActions.LicenceKeySet, AuditEntityTypes.Licence, licence.Id);
            await db.SaveChangesAsync(cancellationToken);
        }

        return await LicenceQueries.LoadDetailAsync(db, licence.Id, companyClock.DateOf(now), cancellationToken);
    }
}

public sealed record RevealLicenceKeyCommand(Guid LicenceId);

/// <summary>Shows the key to an admin. The audit row is saved first: no key leaves without one.</summary>
public sealed class RevealLicenceKeyHandler(IAppDbContext db, ICurrentActor actor, ILicenceKeyProtector protector, IAuditLog audit)
{
    public async Task<LicenceKeyResult> HandleAsync(RevealLicenceKeyCommand command, CancellationToken cancellationToken)
    {
        await LicenceQueries.EnsureAdminAsync(db, actor, cancellationToken);
        var protectedKey = await db.Licences.AsNoTracking().Where(l => l.Id == command.LicenceId).Select(l => l.ProtectedKey)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AuditEntityTypes.Licence);
        var key = protector.TryUnprotect(protectedKey)
            ?? throw new DomainException(ErrorCodes.LicenceKeyUnreadable, "The stored key cannot be opened with the current key ring.");
        audit.Record(AuditActions.LicenceKeyShown, AuditEntityTypes.Licence, command.LicenceId);
        await db.SaveChangesAsync(cancellationToken);
        return new LicenceKeyResult(key);
    }
}
