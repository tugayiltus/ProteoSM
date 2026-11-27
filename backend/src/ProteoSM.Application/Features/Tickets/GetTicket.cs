namespace ProteoSM.Application.Features.Tickets;

public sealed record GetTicketQuery(string Number);

public sealed class GetTicketHandler(ProteoSM.Application.Abstractions.IAppDbContext db, TicketScope scope, ProteoSM.Application.Sla.TicketSlaReader sla, TimeProvider clock)
{
    public async Task<TicketDetail> HandleAsync(GetTicketQuery query, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        return await TicketQueries.LoadDetailAsync(db, sla, clock, ticket.Id, viewer, cancellationToken);
    }
}
