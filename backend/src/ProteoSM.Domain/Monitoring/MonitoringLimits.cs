namespace ProteoSM.Domain.Monitoring;

/// <summary>Limits of the monitoring webhook (ADR-0041).</summary>
public static class MonitoringLimits
{
    public const int NameMaxLength = 100;
    public const int EventIdMaxLength = 100;
    public const int AlertKeyMaxLength = 200;

    /// <summary>The largest request body, read before anything is checked.</summary>
    public const int MaxBodyBytes = 16 * 1024;

    public const int SecretBytes = 32;
    public const int IncidentsPerHour = 30;

    /// <summary>How far the request time may be from the server time, in both directions.</summary>
    public static readonly TimeSpan ClockTolerance = TimeSpan.FromMinutes(5);

    /// <summary>An open alert that fires again gets at most one note in this time.</summary>
    public static readonly TimeSpan NoteInterval = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan IncidentWindow = TimeSpan.FromHours(1);
}
