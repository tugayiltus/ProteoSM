using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Problems;

namespace ProteoSM.Application.Features.Problems;

internal static class ProblemHistory
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) },
    };

    public static void Record(IAppDbContext db, Problem problem, ProblemEventKind kind, Guid? actorUserId, DateTimeOffset now, object? data = null) =>
        db.ProblemEvents.Add(ProblemEvent.Record(problem.Id, kind, actorUserId, JsonSerializer.Serialize(data ?? new { }, Json), now));
}

public sealed record GetProblemTimelineQuery(string Number);

public sealed record ProblemTimelineItem(Guid Id, ProblemEventKind Kind, string? ActorName, JsonElement Data, DateTimeOffset OccurredAt);

/// <summary>Problem history and notes in order. Problems are visible to all staff, so the scope is the only gate.</summary>
public sealed class GetProblemTimelineHandler(IAppDbContext db, ProblemScope scope)
{
    private const string NameSeparator = " ";

    public async Task<IReadOnlyList<ProblemTimelineItem>> HandleAsync(GetProblemTimelineQuery query, CancellationToken cancellationToken)
    {
        var (problem, _) = await scope.LoadAsync(query.Number, cancellationToken);
        var rows = await db.ProblemEvents.AsNoTracking().Where(e => e.ProblemId == problem.Id)
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .Select(e => new
            {
                e.Id,
                e.Kind,
                e.DataJson,
                e.OccurredAt,
                Actor = db.Users.Where(u => u.Id == e.ActorUserId).Select(u => u.FirstName + NameSeparator + u.LastName).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);
        return [.. rows.Select(r => new ProblemTimelineItem(r.Id, r.Kind, r.Actor, JsonSerializer.Deserialize<JsonElement>(r.DataJson), r.OccurredAt))];
    }
}
