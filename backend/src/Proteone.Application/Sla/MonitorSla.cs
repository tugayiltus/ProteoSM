using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Domain.Notifications;
using Proteone.Domain.Sla;

namespace Proteone.Application.Sla;

/// <summary>
/// One SLA monitor sweep (ADR-0014, ADR-0018, spec §5.4): marks running instances whose warning or due moment has
/// passed and records the matching event and notification. Each mark is a conditional <c>ExecuteUpdateAsync</c>
/// (still unmarked, running, not stopped), so of several concurrent runs exactly one wins a row and only the winner
/// writes the <see cref="SlaEvent"/> and outbox rows, in the same (per-instance) transaction as the update. It writes only the
/// warning/breach columns, never the rest of the instance, so it cannot fail a user's ticket action.
/// A breach on an instance never warned also warns first (the warning moment is never later than the due moment).
/// </summary>
public sealed class MonitorSlaHandler(IAppDbContext db, INotificationOutbox outbox, TimeProvider clock)
{
    public async Task<(int Warned, int Breached)> HandleAsync(int batch, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await db.SlaInstances.AsNoTracking()
            .Where(i => i.StoppedAt == null && i.RunningSince != null
                && ((i.WarnedAt == null && i.WarnAt <= now) || (i.BreachedAt == null && i.DueAt <= now)))
            .OrderBy(i => i.Id)
            .Take(batch)
            .Select(i => new { i.Id, i.TicketId, i.Metric })
            .ToListAsync(cancellationToken);

        var warned = 0;
        var breached = 0;
        foreach (var candidate in due)
        {
            // One short transaction per instance: row locks are held for one claim only, never across the sweep, so a
            // ticket action touching the same ticket cannot deadlock with the monitor.
            await db.RunInTransactionAsync(async ct =>
            {
                if (await TryWarnAsync(candidate.Id, now, ct) is { } warnedDueAt)
                {
                    warned++;
                    Record(candidate.Id, candidate.TicketId, candidate.Metric, SlaEventKind.Warning, NotificationEventTypes.SlaWarning, now, warnedDueAt);
                }

                if (await TryBreachAsync(candidate.Id, now, ct) is { } breachedDueAt)
                {
                    breached++;
                    Record(candidate.Id, candidate.TicketId, candidate.Metric, SlaEventKind.Breached, NotificationEventTypes.SlaBreached, now, breachedDueAt);
                }

                await db.SaveChangesAsync(ct);
            }, cancellationToken);
        }

        // Problem timers share the batch size and the sweep; their counts are not part of the ticket result.
        await HandleProblemsAsync(batch, now, cancellationToken);
        return (warned, breached);
    }

    /// <summary>Same claim-then-record pass over running problem timers: one conditional update per mark, outbox row in the same per-timer transaction.</summary>
    private async Task HandleProblemsAsync(int batch, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var due = await db.ProblemSlaTimers.AsNoTracking()
            .Where(t => t.StoppedAt == null
                && ((t.WarnedAt == null && t.WarnAt <= now) || (t.BreachedAt == null && t.DueAt <= now)))
            .OrderBy(t => t.Id)
            .Take(batch)
            .Select(t => new { t.Id, t.ProblemId })
            .ToListAsync(cancellationToken);

        foreach (var candidate in due)
        {
            await db.RunInTransactionAsync(async ct =>
            {
                if (await TryWarnProblemAsync(candidate.Id, now, ct) is { } warnedDueAt)
                {
                    RecordProblem(candidate.ProblemId, NotificationEventTypes.ProblemSlaWarning, warnedDueAt);
                }

                if (await TryBreachProblemAsync(candidate.Id, now, ct) is { } breachedDueAt)
                {
                    RecordProblem(candidate.ProblemId, NotificationEventTypes.ProblemSlaBreached, breachedDueAt);
                }

                await db.SaveChangesAsync(ct);
            }, cancellationToken);
        }
    }

    private async Task<DateTimeOffset?> TryWarnProblemAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var won = await db.ProblemSlaTimers
            .Where(t => t.Id == id && t.WarnedAt == null && t.WarnAt <= now && t.StoppedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.WarnedAt, now), cancellationToken);
        return won == 1 ? await ProblemDueAtAsync(id, cancellationToken) : null;
    }

    private async Task<DateTimeOffset?> TryBreachProblemAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var won = await db.ProblemSlaTimers
            .Where(t => t.Id == id && t.BreachedAt == null && t.DueAt <= now && t.StoppedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.BreachedAt, now), cancellationToken);
        return won == 1 ? await ProblemDueAtAsync(id, cancellationToken) : null;
    }

    private async Task<DateTimeOffset> ProblemDueAtAsync(Guid id, CancellationToken cancellationToken) =>
        await db.ProblemSlaTimers.AsNoTracking().Where(t => t.Id == id).Select(t => t.DueAt).SingleAsync(cancellationToken);

    private void RecordProblem(Guid problemId, string eventType, DateTimeOffset dueAt) =>
        outbox.AddForSubject(eventType, OutboxSubjectTypes.Problem, problemId, userId: null, new Dictionary<string, string>
        {
            [OutboxEvent.SlaDueAtKey] = dueAt.ToString("O", CultureInfo.InvariantCulture),
        });

    // The conditional update is the claim; the returned due time is read in the same transaction, after the row is locked by it.
    private async Task<DateTimeOffset?> TryWarnAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var won = await db.SlaInstances
            .Where(i => i.Id == id && i.WarnedAt == null && i.WarnAt <= now && i.StoppedAt == null && i.RunningSince != null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.WarnedAt, now), cancellationToken);
        return won == 1 ? await DueAtAsync(id, cancellationToken) : null;
    }

    private async Task<DateTimeOffset?> TryBreachAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var won = await db.SlaInstances
            .Where(i => i.Id == id && i.BreachedAt == null && i.DueAt <= now && i.StoppedAt == null && i.RunningSince != null)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.BreachedAt, now), cancellationToken);
        return won == 1 ? await DueAtAsync(id, cancellationToken) : null;
    }

    private async Task<DateTimeOffset> DueAtAsync(Guid id, CancellationToken cancellationToken) =>
        await db.SlaInstances.AsNoTracking().Where(i => i.Id == id).Select(i => i.DueAt!.Value).SingleAsync(cancellationToken);

    private void Record(Guid instanceId, Guid ticketId, SlaMetric metric, SlaEventKind kind, string eventType, DateTimeOffset now, DateTimeOffset dueAt)
    {
        db.SlaEvents.Add(SlaEvent.Record(instanceId, kind, now));
        outbox.Add(eventType, ticketId, userId: null, new Dictionary<string, string>
        {
            [OutboxEvent.SlaMetricKey] = SlaMetricLabels.KeyOf(metric),
            [OutboxEvent.SlaDueAtKey] = dueAt.ToString("O", CultureInfo.InvariantCulture),
        });
    }
}
