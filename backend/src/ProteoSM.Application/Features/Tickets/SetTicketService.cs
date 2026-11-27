using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Application.Features.Services;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Application.Features.Tickets;

public sealed record SetTicketServiceCommand(string Number, Guid? ServiceId, uint Version);

/// <summary>
/// Sets or clears the ticket's service in any state. Any IT staff member or admin who sees the ticket may do it (the
/// same load as change links), not only workers; employees get not found. Status, SLA and completion stay as they are.
/// </summary>
public sealed class SetTicketServiceHandler(IAppDbContext db, TicketWork work, IAuditLog audit)
{
    public async Task<TicketDetail> HandleAsync(SetTicketServiceCommand command, CancellationToken cancellationToken)
    {
        TicketDetail? detail = null;
        // Deactivation takes the same lock, so a service cannot be set while it is being deactivated.
        await db.RunSerializedAsync(SerializationLocks.ServiceCatalog, async ct => detail = await RunAsync(command, ct), cancellationToken);
        return detail!;
    }

    private Task<TicketDetail> RunAsync(SetTicketServiceCommand command, CancellationToken cancellationToken) =>
        work.RunAsync(command.Number, command.Version, requireWorker: false, async (ticket, viewer, now) =>
        {
            if (!viewer.SeesInternal)
            {
                throw new NotFoundException(AuditEntityTypes.Ticket);
            }

            var change = await ServiceAssignment.ApplyAsync(db, ticket.ServiceId, command.ServiceId, id => ticket.SetService(id, now), cancellationToken);
            if (change is null)
            {
                return;
            }

            var kind = change.Cleared ? TicketEventKind.ServiceCleared : TicketEventKind.ServiceSet;
            TicketTimeline.Record(db, ticket, kind, Visibility.Internal, viewer.UserId, now, change.Data);
            audit.Record(AuditActions.TicketServiceSet, AuditEntityTypes.Ticket, ticket.Id, newValues: change.Data);
        }, cancellationToken);
}
