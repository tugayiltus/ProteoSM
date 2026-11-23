using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;
using Tgysm.Domain.Services;
using Tgysm.Domain.Users;

namespace Tgysm.Application.Features.Services;

internal static class ServiceQueries
{
    public const string NameTakenMessage = "Another service uses this name.";
    public const string LinkExistsMessage = "The asset is already linked to this service.";

    public static IRuleBuilderOptions<T, string> ValidServiceName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(ServiceLimits.NameMaxLength);

    public static IRuleBuilderOptions<T, string?> ValidServiceDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(ServiceLimits.DescriptionMaxLength);

    public static async Task<ServiceDetail> LoadDetailAsync(IAppDbContext db, Guid serviceId, CancellationToken cancellationToken) =>
        await (
            from service in db.Services.AsNoTracking()
            where service.Id == serviceId
            join team in db.Teams on service.OwnerTeamId equals team.Id
            select new ServiceDetail(service.Id, service.Name, service.Description, service.OwnerTeamId, team.Name, service.IsActive, service.Version))
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw new NotFoundException(AuditEntityTypes.Service);

    /// <summary>Name check before the save; the unique index still decides a lost race (translated to the same code).</summary>
    public static async Task EnsureNameFreeAsync(IAppDbContext db, string name, Guid? exceptId, CancellationToken cancellationToken)
    {
        var key = TurkishText.ToSearchKey(name);
        if (await db.Services.AnyAsync(s => s.NormalizedName == key && s.Id != exceptId, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ServiceNameTaken, NameTakenMessage);
        }
    }

    /// <summary>An unknown team counts as not active: either way it cannot own a service.</summary>
    public static async Task EnsureOwnerTeamActiveAsync(IAppDbContext db, Guid teamId, CancellationToken cancellationToken)
    {
        if (!await db.Teams.AnyAsync(t => t.Id == teamId && t.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.ServiceOwnerTeamInactive, "A service needs an active owner team.");
        }
    }

    /// <summary>Service reads are for IT staff and admins; employees get not found, as on problem routes (ADR-0034).</summary>
    public static async Task EnsureStaffAsync(IAppDbContext db, ICurrentActor actor, CancellationToken cancellationToken)
    {
        var userId = actor.UserId ?? throw new AuthenticationFailedException();
        var role = await db.Users.Where(u => u.Id == userId).Select(u => u.Role).SingleAsync(cancellationToken);
        if (role == UserRole.Employee)
        {
            throw new NotFoundException(AuditEntityTypes.Service);
        }
    }
}
