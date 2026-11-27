using ProteoSM.Domain.Common;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Domain.Monitoring;

/// <summary>What an admin enters for a source; the secret is set apart.</summary>
public sealed record MonitoringSourceDraft(
    string Name, Guid TeamId, Guid RequesterId, Guid? ServiceId, Guid? CategoryId, Level Impact, Level Urgency, bool IsActive);

/// <summary>A monitoring tool that may open incidents (ADR-0041). Never deleted; an inactive source is refused like an unknown one.</summary>
public sealed class MonitoringSource : Entity
{
    private MonitoringSource()
    {
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>Turkish-insensitive key of the name: unique.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public Guid TeamId { get; private set; }

    /// <summary>The IT user in whose name the incidents are opened.</summary>
    public Guid RequesterId { get; private set; }

    public Guid? ServiceId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Level Impact { get; private set; }
    public Level Urgency { get; private set; }
    public bool IsActive { get; private set; }

    /// <summary>The signing secret, protected with Data Protection; shown once when it is set.</summary>
    public string ProtectedSecret { get; private set; } = string.Empty;

    public DateTimeOffset SecretSetAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static MonitoringSource Create(MonitoringSourceDraft draft, string protectedSecret, DateTimeOffset now)
    {
        var source = new MonitoringSource { CreatedAt = now };
        source.Update(draft, now);
        source.ReplaceSecret(protectedSecret, now);
        return source;
    }

    public void Update(MonitoringSourceDraft draft, DateTimeOffset now)
    {
        Name = draft.Name.Trim();
        NormalizedName = TurkishText.ToSearchKey(Name);
        TeamId = draft.TeamId;
        RequesterId = draft.RequesterId;
        ServiceId = draft.ServiceId;
        CategoryId = draft.CategoryId;
        Impact = draft.Impact;
        Urgency = draft.Urgency;
        IsActive = draft.IsActive;
        UpdatedAt = now;
    }

    /// <summary>The former secret stops at once.</summary>
    public void ReplaceSecret(string protectedSecret, DateTimeOffset now)
    {
        ProtectedSecret = protectedSecret;
        SecretSetAt = now;
        UpdatedAt = now;
    }
}
