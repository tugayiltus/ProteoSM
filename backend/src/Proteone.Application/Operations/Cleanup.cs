using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Proteone.Application.Abstractions;
using Proteone.Domain.Notifications;
using Proteone.Domain.Reports;

namespace Proteone.Application.Operations;

/// <summary>Rows removed by one <see cref="CleanupHandler"/> run, plus the export files it deleted.</summary>
public sealed record CleanupResult(int PasswordTokens, int OutboxEvents, int Heartbeats, int ExportFiles, int ImportJobs);

/// <summary>
/// Retention sweep (ADR-0014, ADR-0015, ADR-0017): deletes password tokens expired, finished outbox events and
/// worker heartbeats last run, and asset import jobs, more than <see cref="Retention"/> ago. Email deliveries of a deleted event go with it
/// (FK cascade). Pending and failed events are kept (failed ones stay visible to admins); an event that still has
/// an unfinished delivery is kept. <c>password_reset_requested</c> events carry a requested email address and never
/// have deliveries, so they are removed once old whatever their status, except while still pending. Ready export files whose
/// lifetime is over (ADR-0020) are deleted from the file store and their jobs marked <see cref="ExportJobStatus.Expired"/>.
/// </summary>
public sealed class CleanupHandler(IAppDbContext db, IFileStore files, TimeProvider clock, ILogger<CleanupHandler> logger)
{
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);

    /// <summary>Expired export jobs loaded and saved per round, so a backlog never loads every job at once.</summary>
    public const int ExportExpiryBatchSize = 100;

    public async Task<CleanupResult> HandleAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - Retention;

        var tokens = await db.PasswordTokens
            .Where(t => t.ExpiresAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var events = await db.OutboxEvents
            .Where(e => (e.Status == OutboxStatus.Dispatched || e.Status == OutboxStatus.Skipped)
                && e.DispatchedAt < cutoff
                && e.EventType != InternalEventTypes.PasswordResetRequested
                && !db.EmailDeliveries.Any(d => d.OutboxId == e.Id
                    && d.Status != DeliveryStatus.Sent && d.Status != DeliveryStatus.Failed))
            .ExecuteDeleteAsync(cancellationToken);

        events += await db.OutboxEvents
            .Where(e => e.EventType == InternalEventTypes.PasswordResetRequested
                && e.Status != OutboxStatus.Pending
                && e.CreatedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var heartbeats = await db.WorkerHeartbeats
            .Where(h => h.LastRunAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        // The audit event of a commit stays; the job only holds the uploaded rows.
        var importJobs = await db.AssetImportJobs
            .Where(j => (j.CommittedAt ?? j.CreatedAt) < cutoff)
            .ExecuteDeleteAsync(cancellationToken);

        // A replay older than this is refused by its timestamp, so the event id is no longer needed (ADR-0041).
        await db.MonitoringEvents.Where(e => e.ReceivedAt < cutoff).ExecuteDeleteAsync(cancellationToken);

        var exportFiles = await ExpireExportsAsync(cancellationToken);

        return new CleanupResult(tokens, events, heartbeats, exportFiles, importJobs);
    }

    /// <summary>
    /// Deletes the file before marking the job: a crash in between leaves a ready job without a file (its download answers 410 and the
    /// next run marks it), never a file no job points at. The save is xmin-guarded, so two cleanup runs cannot both mark a job.
    /// Works in rounds of <see cref="ExportExpiryBatchSize"/>. A file that cannot be deleted is logged and its job stays ready (so a
    /// later run retries it); the other jobs still expire.
    /// </summary>
    private async Task<int> ExpireExportsAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var undeletable = new List<Guid>();
        var expiredCount = 0;
        while (true)
        {
            var batch = await db.ExportJobs
                .Where(j => j.Status == ExportJobStatus.Ready && j.ExpiresAt <= now && !undeletable.Contains(j.Id))
                .OrderBy(j => j.ExpiresAt).ThenBy(j => j.Id)
                .Take(ExportExpiryBatchSize)
                .ToListAsync(cancellationToken);
            foreach (var job in batch)
            {
                if (TryDeleteFile(job))
                {
                    job.Expire(now);
                    expiredCount++;
                }
                else
                {
                    undeletable.Add(job.Id);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            if (batch.Count < ExportExpiryBatchSize)
            {
                return expiredCount;
            }
        }
    }

    private bool TryDeleteFile(ExportJob job)
    {
        if (job.FileKey is not { } key)
        {
            return true;
        }

        try
        {
            files.Delete(key);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Deleting the file of expired export job {JobId} failed; the job stays ready until a later run.", job.Id);
            return false;
        }
    }
}
