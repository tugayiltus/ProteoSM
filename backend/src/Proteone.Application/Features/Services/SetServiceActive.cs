using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Audit;

namespace Proteone.Application.Features.Services;

public sealed record SetServiceActiveCommand(Guid Id, uint Version, bool IsActive);

/// <summary>Activates or deactivates a service; asking for the current state is a no-op without an audit row.</summary>
public sealed class SetServiceActiveHandler(IAppDbContext db, IAuditLog audit, TimeProvider clock)
{
    public async Task<ServiceDetail> HandleAsync(SetServiceActiveCommand command, CancellationToken cancellationToken)
    {
        await db.RunSerializedAsync(SerializationLocks.ServiceCatalog, async ct =>
        {
            var service = await db.Services.SingleOrDefaultAsync(s => s.Id == command.Id, ct)
                ?? throw new NotFoundException(AuditEntityTypes.Service);
            if (service.IsActive == command.IsActive)
            {
                return;
            }

            db.SetExpectedVersion(service, command.Version);
            service.SetActive(command.IsActive, clock.GetUtcNow());
            audit.Record(command.IsActive ? AuditActions.ServiceActivated : AuditActions.ServiceDeactivated, AuditEntityTypes.Service, service.Id);
            await db.SaveChangesAsync(ct);
        }, cancellationToken);
        return await ServiceQueries.LoadDetailAsync(db, command.Id, cancellationToken);
    }
}
