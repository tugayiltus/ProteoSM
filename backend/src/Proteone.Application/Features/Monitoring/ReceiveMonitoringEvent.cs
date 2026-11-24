using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Proteone.Application.Abstractions;
using Proteone.Application.Common;
using Proteone.Application.Features.Tickets;
using Proteone.Application.Sla;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Monitoring;
using Proteone.Domain.Notifications;
using Proteone.Domain.Tickets;
using Proteone.Domain.Users;

namespace Proteone.Application.Features.Monitoring;

/// <summary>The raw request: the signature covers the bytes as they arrived, so nothing is parsed before it is checked.</summary>
public sealed record ReceiveMonitoringEventCommand(Guid SourceId, string? Timestamp, string? Signature, byte[] Body);

public enum MonitoringEventResult
{
    Accepted,

    /// <summary>Unknown or inactive source, bad time or bad signature: the caller gives one answer for all.</summary>
    Unauthorized,

    /// <summary>The source opened its hourly number of incidents.</summary>
    Limited,
}

internal enum MonitoringState
{
    Firing,
    Resolved,
}

/// <summary>
/// The monitoring webhook (ADR-0041). Order: source, time and signature; then the body; then one lock per source, under
/// which a repeated event id, the open incident of an alert and the hourly limit are decided. Events are never an
/// authenticated user: records carry the requester of the source, never the caller.
/// </summary>
public sealed class ReceiveMonitoringEventHandler(
    IAppDbContext db, IMonitoringSecretProtector protector, ITicketNumberGenerator numbers, SlaTracker sla, INotificationOutbox outbox,
    IAuditLog audit, ICompanyClock companyClock, TimeProvider clock)
{
    private const char SignedPartsSeparator = '.';
    private const string FiringState = "firing";
    private const string ResolvedState = "resolved";
    private const string FiringNote = "İzleme: uyarı yeniden geldi.";
    private const string ResolvedNote = "İzleme: uyarı düzeldi.";
    private const char Space = ' ';

    // Used for an unknown source, so its answer takes the same work as a wrong signature.
    private static readonly byte[] NoSecret = new byte[MonitoringLimits.SecretBytes];

    private sealed record Payload(string? EventId, string? AlertKey, string? State, string? Title, string? Description);

    private sealed record Alert(string EventId, string AlertKey, MonitoringState State, string Title, string Description);

    public async Task<MonitoringEventResult> HandleAsync(ReceiveMonitoringEventCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var source = await db.MonitoringSources.AsNoTracking().SingleOrDefaultAsync(s => s.Id == command.SourceId, cancellationToken);
        var secret = source is null ? null : protector.TryUnprotect(source.ProtectedSecret);
        var signed = IsSigned(command, secret is null ? NoSecret : Encoding.UTF8.GetBytes(secret));
        if (source is not { IsActive: true } || secret is null || !signed || !IsCurrent(command.Timestamp, now))
        {
            await CountFailureAsync(command.SourceId, now, cancellationToken);
            return MonitoringEventResult.Unauthorized;
        }

        Alert alert;
        try
        {
            alert = Parse(command.Body);
        }
        catch (ValidationException)
        {
            await CountFailureAsync(source.Id, now, cancellationToken);
            throw;
        }

        try
        {
            var result = MonitoringEventResult.Accepted;
            await db.RunSerializedAsync(LockKeyOf(source.Id), async ct => result = await ApplyAsync(source, alert, ct), cancellationToken);
            return result;
        }
        catch (DomainException unusable) when (unusable.Code == ErrorCodes.MonitoringSourceUnusable)
        {
            await CountFailureAsync(source.Id, now, cancellationToken);
            throw;
        }
    }

    private static bool IsSigned(ReceiveMonitoringEventCommand command, byte[] secret)
    {
        var prefix = Encoding.UTF8.GetBytes((command.Timestamp ?? string.Empty) + SignedPartsSeparator);
        byte[] signed = [.. prefix, .. command.Body];
        var expected = HMACSHA256.HashData(secret, signed);
        Span<byte> given = stackalloc byte[HMACSHA256.HashSizeInBytes];
        return command.Signature is { Length: HMACSHA256.HashSizeInBytes * 2 } text
            && Convert.FromHexString(text, given, out _, out _) == System.Buffers.OperationStatus.Done
            && CryptographicOperations.FixedTimeEquals(expected, given);
    }

    private static bool IsCurrent(string? timestamp, DateTimeOffset now) =>
        long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
        && seconds <= DateTimeOffset.MaxValue.ToUnixTimeSeconds()
        && (now - DateTimeOffset.FromUnixTimeSeconds(seconds)).Duration() <= MonitoringLimits.ClockTolerance;

    /// <summary>One advisory lock per source. Negative keys never meet the fixed keys of <see cref="SerializationLocks"/>.</summary>
    private static long LockKeyOf(Guid sourceId) => BitConverter.ToInt64(sourceId.ToByteArray(), sizeof(long)) | long.MinValue;

    private static Alert Parse(byte[] body)
    {
        Payload payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(body, JsonSerializerOptions.Web) ?? throw Invalid(nameof(body), ErrorCodes.InvalidValue);
        }
        catch (JsonException)
        {
            throw Invalid(nameof(body), ErrorCodes.InvalidValue);
        }

        var state = payload.State switch
        {
            FiringState => MonitoringState.Firing,
            ResolvedState => MonitoringState.Resolved,
            _ => throw Invalid(nameof(Payload.State), ErrorCodes.InvalidValue),
        };
        return new Alert(
            Text(payload.EventId, MonitoringLimits.EventIdMaxLength, nameof(Payload.EventId)),
            Text(payload.AlertKey, MonitoringLimits.AlertKeyMaxLength, nameof(Payload.AlertKey)),
            state,
            Text(payload.Title, Ticket.SubjectMaxLength, nameof(Payload.Title)),
            Text(payload.Description, Ticket.DescriptionMaxLength, nameof(Payload.Description), keepLines: true));
    }

    /// <summary>Plain text: control characters (also NUL, which the database refuses) become spaces, line breaks stay in a description.</summary>
    private static string Text(string? value, int maxLength, string field, bool keepLines = false)
    {
        var clean = string.Concat((value ?? string.Empty).Select(c => char.IsControl(c) && !(keepLines && c is '\n' or '\t') ? Space : c)).Trim();
        return clean.Length switch
        {
            0 => throw Invalid(field, ErrorCodes.Required),
            _ when clean.Length > maxLength => throw Invalid(field, ErrorCodes.TooLong),
            _ => clean,
        };
    }

    private static ValidationException Invalid(string field, string code) =>
        new([new ValidationFailure(JsonNamingPolicy.CamelCase.ConvertName(field), "Invalid monitoring event.") { ErrorCode = code }]);

    private Task<int> CountFailureAsync(Guid sourceId, DateTimeOffset now, CancellationToken cancellationToken) =>
        db.MonitoringSourceStatuses.Where(s => s.SourceId == sourceId).ExecuteUpdateAsync(
            set => set.SetProperty(s => s.FailedCount, s => s.FailedCount + 1).SetProperty(s => s.LastFailedAt, now), cancellationToken);

    private async Task<MonitoringEventResult> ApplyAsync(MonitoringSource checkedSource, Alert alert, CancellationToken cancellationToken)
    {
        // Read again under the lock: the source may have been stopped, or its secret replaced, while the request waited.
        var sourceId = checkedSource.Id;
        var source = await db.MonitoringSources.AsNoTracking().SingleAsync(s => s.Id == sourceId, cancellationToken);
        var now = clock.GetUtcNow();
        if (!source.IsActive || source.SecretSetAt != checkedSource.SecretSetAt)
        {
            return MonitoringEventResult.Unauthorized;
        }

        if (!await db.MonitoringEvents.AnyAsync(e => e.SourceId == sourceId && e.EventId == alert.EventId, cancellationToken))
        {
            var open = await (
                from a in db.MonitoringAlerts
                join t in db.Tickets on a.TicketId equals t.Id
                where a.SourceId == sourceId && a.AlertKey == alert.AlertKey
                    && t.Status != TicketStatus.Completed && t.Status != TicketStatus.Cancelled
                orderby a.CreatedAt descending
                select new { Alert = a, Ticket = t }).FirstOrDefaultAsync(cancellationToken);
            if (open is not null)
            {
                await NoteAsync(source, open.Alert, open.Ticket, alert, now, cancellationToken);
            }
            else if (alert.State == MonitoringState.Firing)
            {
                var since = now - MonitoringLimits.IncidentWindow;
                if (await db.MonitoringAlerts.CountAsync(a => a.SourceId == sourceId && a.CreatedAt > since, cancellationToken) >= MonitoringLimits.IncidentsPerHour)
                {
                    await AuditLimitAsync(sourceId, since, now, cancellationToken);
                    return MonitoringEventResult.Limited;
                }

                await OpenIncidentAsync(source, alert, now, cancellationToken);
            }

            db.MonitoringEvents.Add(MonitoringEvent.Create(sourceId, alert.EventId, now));
            await db.SaveChangesAsync(cancellationToken);
        }

        await db.MonitoringSourceStatuses.Where(s => s.SourceId == sourceId).ExecuteUpdateAsync(
            set => set.SetProperty(s => s.LastEventAt, now).SetProperty(s => s.FailedCount, 0), cancellationToken);
        return MonitoringEventResult.Accepted;
    }

    private async Task AuditLimitAsync(Guid sourceId, DateTimeOffset since, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var claimed = await db.MonitoringSourceStatuses
            .Where(s => s.SourceId == sourceId && (s.LimitAuditedAt == null || s.LimitAuditedAt <= since))
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.LimitAuditedAt, now), cancellationToken);
        if (claimed == 1)
        {
            audit.Record(AuditActions.MonitoringLimitReached, AuditEntityTypes.MonitoringSource, sourceId, newValues: new { MonitoringLimits.IncidentsPerHour });
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task NoteAsync(MonitoringSource source, MonitoringAlert record, Ticket ticket, Alert alert, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var firing = alert.State == MonitoringState.Firing;
        if (!(firing ? record.FireAgain(now) : record.Resolve()))
        {
            return;
        }

        var text = $"{(firing ? FiringNote : ResolvedNote)}\n{alert.Title}\n{alert.Description}";
        var body = text.Length <= TicketComment.BodyMaxLength ? text : text[..TicketComment.BodyMaxLength];
        var comment = TicketComment.Create(ticket.Id, source.RequesterId, body, Visibility.Internal, now);
        db.TicketComments.Add(comment);
        TicketTimeline.Record(db, ticket, TicketEventKind.InternalNote, Visibility.Internal, source.RequesterId, now, new { commentId = comment.Id, files = 0 });
        await TicketActivity.TouchAsync(db, ticket.Id, now, cancellationToken);
    }

    private async Task OpenIncidentAsync(MonitoringSource source, Alert alert, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await EnsureUsableAsync(source, cancellationToken);
        var categoryId = await db.Categories.Where(c => c.Id == source.CategoryId && c.IsActive).Select(c => (Guid?)c.Id).SingleOrDefaultAsync(cancellationToken);
        var serviceId = await db.Services.Where(s => s.Id == source.ServiceId && s.IsActive).Select(s => (Guid?)s.Id).SingleOrDefaultAsync(cancellationToken);
        var year = companyClock.YearOf(now);
        var ticket = Ticket.CreateIncident(
            TicketNumber.Format(TicketKind.Incident, year, await numbers.NextAsync(TicketKind.Incident, year, cancellationToken)),
            alert.Title, alert.Description, categoryId, locationId: null, source.RequesterId, source.TeamId, now);
        ticket.ChangePriority(source.Impact, source.Urgency, now);
        ticket.SetService(serviceId, now);
        db.Tickets.Add(ticket);
        db.MonitoringAlerts.Add(MonitoringAlert.Open(source.Id, alert.AlertKey, ticket.Id, now));
        await sla.OnCreatedAsync(ticket, cancellationToken);
        TicketTimeline.Record(db, ticket, TicketEventKind.Created, Visibility.Public, source.RequesterId, now);
        audit.Record(
            AuditActions.TicketCreated, AuditEntityTypes.Ticket, ticket.Id,
            newValues: new { ticket.Number, ticket.Kind, ticket.Status, MonitoringSourceId = source.Id }, actorUserId: source.RequesterId);
        outbox.Add(NotificationEventTypes.TicketCreated, ticket.Id, userId: null);
    }

    /// <summary>The requester left IT or the team was closed after the source was saved: an admin has to fix the source.</summary>
    private async Task EnsureUsableAsync(MonitoringSource source, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(u => u.Id == source.RequesterId && u.IsActive && u.Role != UserRole.Employee, cancellationToken)
            || !await db.Teams.AnyAsync(t => t.Id == source.TeamId && t.IsActive, cancellationToken))
        {
            throw new DomainException(ErrorCodes.MonitoringSourceUnusable, "The requester or the team of the source is not active.");
        }
    }
}
