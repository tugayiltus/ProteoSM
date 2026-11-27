using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Notifications;

namespace ProteoSM.Application.Features.Licences;

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
            var noticeEnd = today.AddDays(await db.SystemSettings.Select(s => s.LicenceNoticeDays).SingleAsync(ct));
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
