namespace Proteone.Application.Features.Tickets;

public sealed record GetTicketQuery(string Number);

public sealed class GetTicketHandler(Proteone.Application.Abstractions.IAppDbContext db, TicketScope scope, Proteone.Application.Sla.TicketSlaReader sla)
{
    public async Task<TicketDetail> HandleAsync(GetTicketQuery query, CancellationToken cancellationToken)
    {
        var (ticket, viewer) = await scope.LoadAsync(query.Number, cancellationToken);
        return await TicketQueries.LoadDetailAsync(db, sla, ticket.Id, viewer, cancellationToken);
    }
}
