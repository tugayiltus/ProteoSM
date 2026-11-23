using Microsoft.EntityFrameworkCore;
using Tgysm.Application.Abstractions;
using Tgysm.Application.Common;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Common;

namespace Tgysm.Application.Features.Services;

/// <summary>A service change on a record: whether it cleared the service, and the timeline/audit payload.</summary>
internal sealed record ServiceChange(bool Cleared, object Data);

/// <summary>
/// Shared rules of the <c>set-service</c> action on tickets, changes and problems: a new service must exist and be
/// active, clearing is always allowed, and the same value again is a no-op (no event, no audit).
/// </summary>
internal static class ServiceAssignment
{
    private const string InactiveMessage = "Only an active service can be set.";

    /// <summary>
    /// Checks the requested service and applies it through <paramref name="apply"/>. Returns the old and new names for the
    /// timeline event and audit row, or null when the record already had this value.
    /// </summary>
    public static async Task<ServiceChange?> ApplyAsync(
        IAppDbContext db, Guid? currentId, Guid? requestedId, Func<Guid?, bool> apply, CancellationToken cancellationToken)
    {
        if (currentId == requestedId)
        {
            return null;
        }

        string? newName = null;
        if (requestedId is { } id)
        {
            var service = await db.Services.AsNoTracking().Where(s => s.Id == id).Select(s => new { s.Name, s.IsActive })
                .SingleOrDefaultAsync(cancellationToken) ?? throw new NotFoundException(AuditEntityTypes.Service);
            if (!service.IsActive)
            {
                throw new DomainException(ErrorCodes.ServiceInactive, InactiveMessage);
            }

            newName = service.Name;
        }

        var oldName = currentId is { } oldId
            ? await db.Services.AsNoTracking().Where(s => s.Id == oldId).Select(s => s.Name).SingleOrDefaultAsync(cancellationToken)
            : null;
        apply(requestedId);
        return new ServiceChange(requestedId is null, new { oldServiceId = currentId, oldName, newServiceId = requestedId, newName });
    }

    /// <summary>The service reference shown on a record detail, or null when the record has none.</summary>
    public static async Task<ServiceRef?> RefAsync(IAppDbContext db, Guid? serviceId, CancellationToken cancellationToken) =>
        serviceId is { } id
            ? await db.Services.AsNoTracking().Where(s => s.Id == id).Select(s => new ServiceRef(s.Id, s.Name, s.IsActive)).SingleOrDefaultAsync(cancellationToken)
            : null;
}
