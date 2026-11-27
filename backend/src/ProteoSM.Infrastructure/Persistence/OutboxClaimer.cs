using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Notifications;

namespace ProteoSM.Infrastructure.Persistence;

/// <summary><see cref="IOutboxClaimer"/> over <see cref="RowLease"/> (ADR-0014).</summary>
internal sealed class OutboxClaimer(IAppDbContext db) : IOutboxClaimer
{
    private const string PendingPredicate = "status = 'pending' AND (locked_until IS NULL OR locked_until < now())";

    public async Task<IReadOnlyList<OutboxEvent>> ClaimAsync(int batch, TimeSpan lease, CancellationToken cancellationToken) =>
        await RowLease.ClaimAsync(db.OutboxEvents, PendingPredicate, batch, lease, cancellationToken);
}
