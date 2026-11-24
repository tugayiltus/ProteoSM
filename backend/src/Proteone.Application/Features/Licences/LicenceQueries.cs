using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Licences;
using Proteone.Domain.Users;

namespace Proteone.Application.Features.Licences;

internal static class LicenceQueries
{
    public const string SoftwareNameTakenMessage = "Another software record uses this vendor and name.";
    public const string AlreadyAssignedMessage = "The target already has a seat of this licence.";

    public static IRuleBuilderOptions<T, string> ValidLicenceName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(LicenceLimits.NameMaxLength);

    public static IRuleBuilderOptions<T, string> ValidVendor<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(LicenceLimits.VendorMaxLength);

    public static IRuleBuilderOptions<T, int> ValidQuantity<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(LicenceLimits.MinQuantity, LicenceLimits.MaxQuantity);

    public static IRuleBuilderOptions<T, string?> ValidLicenceNotes<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(LicenceLimits.NotesMaxLength);

    /// <summary>Licence routes are for IT staff and admins; employees get not found, as on service routes (ADR-0036).</summary>
    public static async Task<(Guid UserId, UserRole Role)> EnsureStaffAsync(IAppDbContext db, ICurrentActor actor, CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        var role = await db.Users.Where(u => u.Id == userId).Select(u => u.Role).SingleAsync(cancellationToken);
        return role == UserRole.Employee ? throw new NotFoundException(AuditEntityTypes.Licence) : (userId, role);
    }

    /// <summary>Licence keys are for admins only.</summary>
    public static async Task EnsureAdminAsync(IAppDbContext db, ICurrentActor actor, CancellationToken cancellationToken)
    {
        var (_, role) = await EnsureStaffAsync(db, actor, cancellationToken);
        if (role != UserRole.Admin)
        {
            throw new ForbiddenException();
        }
    }

    public static async Task EnsureOwnerTeamActiveAsync(IAppDbContext db, Guid teamId, CancellationToken cancellationToken)
    {
        if (!await db.Teams.AnyAsync(t => t.Id == teamId && t.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.LicenceOwnerTeamInactive, "A licence needs an active owner team.");
        }
    }

    public static Task<int> UsedSeatsAsync(IAppDbContext db, Guid licenceId, CancellationToken cancellationToken) =>
        db.LicenceAssignments.CountAsync(a => a.LicenceId == licenceId && a.ReleasedAt == null, cancellationToken);

    public static async Task<Licence> LoadAsync(IAppDbContext db, Guid licenceId, CancellationToken cancellationToken) =>
        await db.Licences.SingleOrDefaultAsync(l => l.Id == licenceId, cancellationToken)
        ?? throw new NotFoundException(AuditEntityTypes.Licence);

    public static async Task<LicenceDetail> LoadDetailAsync(IAppDbContext db, Guid licenceId, DateOnly today, CancellationToken cancellationToken) =>
        await (
            from licence in db.Licences.AsNoTracking()
            where licence.Id == licenceId
            join software in db.Software on licence.SoftwareId equals software.Id
            join team in db.Teams on licence.OwnerTeamId equals team.Id
            select new LicenceDetail(
                licence.Id, licence.Name, software.Id, software.Name, software.Vendor, licence.Kind, licence.Quantity,
                db.LicenceAssignments.Count(a => a.LicenceId == licence.Id && a.ReleasedAt == null),
                team.Id, team.Name, licence.ExpiresOn, licence.ExpiresOn < today, licence.Notes, licence.ProtectedKey != null, licence.Version))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(AuditEntityTypes.Licence);

    public static async Task<SoftwareItem> LoadSoftwareAsync(IAppDbContext db, Guid softwareId, CancellationToken cancellationToken) =>
        await db.Software.AsNoTracking().Where(s => s.Id == softwareId)
            .Select(s => new SoftwareItem(s.Id, s.Name, s.Vendor, s.IsActive, db.Licences.Count(l => l.SoftwareId == s.Id), s.Version))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(AuditEntityTypes.Software);
}
