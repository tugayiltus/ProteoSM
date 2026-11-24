namespace Proteone.Domain.Monitoring;

/// <summary>Counters of a source, apart from it: an incoming event must not make an admin's edit fail.</summary>
public sealed class MonitoringSourceStatus
{
    private MonitoringSourceStatus()
    {
    }

    public Guid SourceId { get; private init; }
    public DateTimeOffset? LastEventAt { get; private set; }

    /// <summary>Refused requests since the last accepted one.</summary>
    public int FailedCount { get; private set; }

    public DateTimeOffset? LastFailedAt { get; private set; }

    /// <summary>When the hourly limit was last written to the audit log.</summary>
    public DateTimeOffset? LimitAuditedAt { get; private set; }

    public static MonitoringSourceStatus For(Guid sourceId) => new() { SourceId = sourceId };
}

/// <summary>A processed event id; a repeat has no effect. Deleted after 30 days.</summary>
public sealed class MonitoringEvent
{
    private MonitoringEvent()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid SourceId { get; private init; }
    public string EventId { get; private init; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; private init; }

    public static MonitoringEvent Create(Guid sourceId, string eventId, DateTimeOffset now) =>
        new() { SourceId = sourceId, EventId = eventId, ReceivedAt = now };
}

/// <summary>The incident an alert opened.</summary>
public sealed class MonitoringAlert
{
    private MonitoringAlert()
    {
    }

    public Guid Id { get; private init; } = Guid.CreateVersion7();
    public Guid SourceId { get; private init; }
    public string AlertKey { get; private init; } = string.Empty;
    public Guid TicketId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset LastFiringAt { get; private set; }
    public DateTimeOffset? LastResolvedAt { get; private set; }
    public bool IsResolved { get; private set; }

    public static MonitoringAlert Open(Guid sourceId, string alertKey, Guid ticketId, DateTimeOffset now) =>
        new() { SourceId = sourceId, AlertKey = alertKey, TicketId = ticketId, CreatedAt = now, LastFiringAt = now };

    /// <summary>
    /// True when the incident gets a note: at most one in <see cref="MonitoringLimits.NoteInterval"/>, but always when the
    /// last note said "resolved", so an incident never reads as resolved while its alert is firing.
    /// </summary>
    public bool FireAgain(DateTimeOffset now)
    {
        if (!IsResolved && now - LastFiringAt < MonitoringLimits.NoteInterval)
        {
            return false;
        }

        LastFiringAt = now;
        IsResolved = false;
        return true;
    }

    /// <summary>True when the incident gets a note; a repeated "resolved" does not, nor a second one within the note interval.</summary>
    public bool Resolve(DateTimeOffset now)
    {
        if (IsResolved || now - LastResolvedAt < MonitoringLimits.NoteInterval)
        {
            return false;
        }

        LastResolvedAt = now;
        IsResolved = true;
        return true;
    }
}
