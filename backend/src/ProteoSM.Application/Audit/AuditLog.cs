using System.Text.Json;
using System.Text.Json.Serialization;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Audit;

namespace ProteoSM.Application.Audit;

internal sealed class AuditLog(IAppDbContext db, ICurrentActor actor, TimeProvider clock) : IAuditLog
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    private const string UpdatedSuffix = ".updated";

    public void Record(
        string action,
        string entityType,
        Guid? entityId,
        object? oldValues = null,
        object? newValues = null,
        string? reason = null,
        Guid? actorUserId = null)
    {
        var (before, after) = (Serialize(oldValues), Serialize(newValues));
        if (action.EndsWith(UpdatedSuffix, StringComparison.Ordinal) && before is not null && before == after && reason is null)
        {
            // Saving a form without a change is not an event.
            return;
        }

        db.AuditEvents.Add(AuditEvent.Create(
            clock.GetUtcNow(),
            actorUserId ?? actor.UserId,
            action,
            entityType,
            entityId,
            reason,
            before,
            after,
            actor.CorrelationId));
    }

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
}
