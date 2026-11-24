using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Domain.Licences;
using Proteone.Domain.Notifications;

namespace Proteone.Application.Features.Licences;

/// <summary>
/// Writes one <c>licence_expiring</c> event per licence and end date, once the end is within the notice period. Runs
/// under the seat lock: two workers, or an edit of the end date, never announce or skip a date by racing.
/// </summary>
public sealed class NotifyExpiringLicencesHandler(IAppDbContext db, INotificationOutbox outbox, ICompanyClock companyClock, TimeProvider clock)
{
    public Task HandleAsync(CancellationToken cancellationToken) =>
        db.RunSerializedAsync(SerializationLocks.LicenceSeats, async ct =>
        {
            var today = companyClock.DateOf(clock.GetUtcNow());
            var noticeEnd = today.AddDays(LicenceLimits.ExpiryNoticeDays);
            var due = await db.Licences
                .Where(l => l.ExpiresOn >= today && l.ExpiresOn <= noticeEnd && (l.ExpiryNotifiedFor == null || l.ExpiryNotifiedFor != l.ExpiresOn))
                .ToListAsync(ct);
            foreach (var licence in due)
            {
                licence.MarkExpiryNotified();
                outbox.AddForSubject(NotificationEventTypes.LicenceExpiring, OutboxSubjectTypes.Licence, licence.Id, userId: null);
            }

            await db.SaveChangesAsync(ct);
        }, cancellationToken);
}
