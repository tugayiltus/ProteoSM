using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Api.IntegrationTests.Tickets;
using Proteone.Api.Security;
using Proteone.Application.Operations;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Monitoring;
using Proteone.Domain.Tickets;
using Proteone.Domain.Users;

namespace Proteone.Api.IntegrationTests.Monitoring;

[Collection(ApiCollection.Name)]
public sealed class MonitoringWebhookTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SourcesPath = "/api/v1/admin/monitoring-sources";
    private const int ParallelEvents = 6;

    private TicketWorld _world = null!;
    private HttpClient _http = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _world = await TicketWorld.CreateAsync(factory);
        _http = factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed record Source(Guid Id, string Secret, JsonElement Item);

    private object Draft(string name = "Zabbix", bool isActive = true, Guid? requesterId = null) => new
    {
        name,
        teamId = _world.FieldTeam.Id,
        requesterId = requesterId ?? _world.DeskUser.Id,
        serviceId = (Guid?)null,
        categoryId = (Guid?)null,
        impact = "high",
        urgency = "high",
        isActive,
    };

    private async Task<Source> SourceAsync(string name = "Zabbix")
    {
        using var response = await _world.Admin.PostAsync(SourcesPath, Draft(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var body = await _world.Admin.ReadJsonAsync<JsonElement>(response);
        var item = body.GetProperty("source");
        return new Source(item.GetProperty("id").GetGuid(), body.GetProperty("secret").GetString()!, item);
    }

    private static string Event(string eventId, string state = "firing", string alertKey = "disk", string title = "Disk dolu", string description = "sda1 %95") =>
        JsonSerializer.Serialize(new { eventId, alertKey, state, title, description });

    private static string Now(TimeSpan offset = default) => (DateTimeOffset.UtcNow + offset).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    private static string Sign(string secret, string timestamp, string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")));

    private Task<HttpResponseMessage> SendAsync(Guid sourceId, string body, string? timestamp, string? signature)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/monitoring/{sourceId}/events") { Content = new StringContent(body, new MediaTypeHeaderValue("application/json")) };
        if (timestamp is not null)
        {
            request.Headers.Add("X-Proteone-Timestamp", timestamp);
        }

        if (signature is not null)
        {
            request.Headers.Add("X-Proteone-Signature", signature);
        }

        return _http.SendAsync(request, Ct);
    }

    private Task<HttpResponseMessage> SendAsync(Source source, string body)
    {
        var timestamp = Now();
        return SendAsync(source.Id, body, timestamp, Sign(source.Secret, timestamp, body));
    }

    private async Task AcceptedAsync(Source source, string body)
    {
        using var response = await SendAsync(source, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<List<Ticket>> TicketsAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.Tickets.AsNoTracking().OrderBy(t => t.Number).ToListAsync(Ct);
    }

    private async Task<List<TicketComment>> CommentsAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.TicketComments.AsNoTracking().OrderBy(c => c.CreatedAt).ToListAsync(Ct);
    }

    private async Task<JsonElement> ItemAsync(Guid id) =>
        (await TicketWorld.ReadAsync(_world.Admin, SourcesPath)).EnumerateArray().Single(s => s.GetProperty("id").GetGuid() == id);

    [Fact]
    public async Task A_firing_event_opens_an_incident_with_the_values_of_the_source()
    {
        var source = await SourceAsync();
        await AcceptedAsync(source, Event("e-1"));

        var ticket = Assert.Single(await TicketsAsync());
        Assert.Equal((TicketKind.Incident, "Disk dolu", "sda1 %95"), (ticket.Kind, ticket.Subject, ticket.Description));
        Assert.Equal((_world.FieldTeam.Id, _world.DeskUser.Id, Priority.P1), (ticket.TeamId, ticket.RequesterId, ticket.Priority));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(2, await db.SlaInstances.CountAsync(i => i.TicketId == ticket.Id, Ct));
        Assert.Equal(_world.DeskUser.Id, (await db.AuditEvents.SingleAsync(e => e.Action == AuditActions.TicketCreated, Ct)).ActorUserId);

        var seen = await TicketWorld.ReadAsync(_world.Field, $"/api/v1/tickets/{ticket.Number}");
        Assert.Equal("Zabbix", seen.GetProperty("monitoringSource").GetString());
        Assert.NotEqual(JsonValueKind.Null, (await ItemAsync(source.Id)).GetProperty("lastEventAt").ValueKind);
    }

    [Fact]
    public async Task Every_unauthorized_request_gets_the_same_answer_and_has_no_effect()
    {
        var source = await SourceAsync();
        var inactive = await SourceAsync("Pasif");
        using (var stop = await _world.Admin.PutAsync($"{SourcesPath}/{inactive.Id}", WithVersion(inactive.Item, isActive: false)))
        {
            Assert.Equal(HttpStatusCode.OK, stop.StatusCode);
        }

        var body = Event("e-1");
        var now = Now();
        var good = Sign(source.Secret, now, body);
        var (late, early) = (Now(TimeSpan.FromMinutes(-6)), Now(TimeSpan.FromMinutes(6)));
        var answers = new List<(HttpStatusCode, string)>();
        foreach (var call in new[]
        {
            SendAsync(Guid.NewGuid(), body, now, good),
            SendAsync(inactive.Id, body, now, Sign(inactive.Secret, now, body)),
            SendAsync(source.Id, body, now, Sign(inactive.Secret, now, body)),
            SendAsync(source.Id, body + " ", now, good),
            SendAsync(source.Id, body, (long.Parse(now, CultureInfo.InvariantCulture) + 1).ToString(CultureInfo.InvariantCulture), good),
            SendAsync(source.Id, body, late, Sign(source.Secret, late, body)),
            SendAsync(source.Id, body, early, Sign(source.Secret, early, body)),
            SendAsync(source.Id, body, "soon", Sign(source.Secret, "soon", body)),
            SendAsync(source.Id, body, "-1", Sign(source.Secret, "-1", body)),
            SendAsync(source.Id, body, "9223372036854775807", Sign(source.Secret, "9223372036854775807", body)),
            SendAsync(source.Id, body, now, null),
            SendAsync(source.Id, body, null, good),
            SendAsync(source.Id, body, now, "zz"),
            SendAsync(source.Id, body, now, new string('z', 64)),
            SendAsync(source.Id, body, now, good + "00"),
            SendAsync(source.Id, "not json", now, good),
        })
        {
            using var response = await call;
            answers.Add((response.StatusCode, await response.Content.ReadAsStringAsync(Ct)));
        }

        Assert.True(answers.Distinct().Count() == 1, string.Join("\n", answers.Distinct()));
        Assert.Equal(HttpStatusCode.Unauthorized, answers[0].Item1);
        Assert.Contains(ErrorCodes.MonitoringUnauthorized, answers[0].Item2, StringComparison.Ordinal);
        Assert.Empty(await TicketsAsync());
        Assert.True((await ItemAsync(source.Id)).GetProperty("failedCount").GetInt32() > 0);

        await AcceptedAsync(source, body);
        Assert.Equal(0, (await ItemAsync(source.Id)).GetProperty("failedCount").GetInt32());
    }

    private object WithVersion(JsonElement item, bool isActive = true, string? name = null, Guid? requesterId = null) => new
    {
        version = item.GetProperty("version").GetUInt32(),
        name = name ?? item.GetProperty("name").GetString(),
        teamId = _world.FieldTeam.Id,
        requesterId = requesterId ?? _world.DeskUser.Id,
        impact = "high",
        urgency = "high",
        isActive,
    };

    [Fact]
    public async Task A_signed_request_with_bad_content_is_400_and_does_not_use_up_the_event_id()
    {
        var source = await SourceAsync();
        foreach (var body in new[]
        {
            "", "null", "[]", "{", "{\"title\":\"\\ud800\"}", Event(""), Event("e-1", state: "broken"), Event("e-1", alertKey: ""), Event("e-1", title: "\u0000\u0007"),
            Event("e-1", title: new string('x', Ticket.SubjectMaxLength + 1)), Event("e-1", description: new string('x', Ticket.DescriptionMaxLength + 1)),
            Event(new string('x', MonitoringLimits.EventIdMaxLength + 1)),
        })
        {
            using var response = await SendAsync(source, body);
            Assert.Equal((HttpStatusCode.BadRequest, body), (response.StatusCode, body));
        }

        using (var large = await SendAsync(source, Event("e-1", description: new string('x', MonitoringLimits.MaxBodyBytes))))
        {
            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, large.StatusCode);
        }

        await AcceptedAsync(source, Event("e-1", title: "Satır\r\nsonu\u0000", description: "<b>bir</b>\niki\u0000"));
        var ticket = Assert.Single(await TicketsAsync());
        Assert.Equal(("Satır  sonu", "<b>bir</b>\niki"), (ticket.Subject, ticket.Description));
    }

    [Fact]
    public async Task A_repeated_event_has_no_effect()
    {
        var source = await SourceAsync();
        await AcceptedAsync(source, Event("e-1"));
        await CloseAsync();
        await AcceptedAsync(source, Event("e-1"));
        Assert.Single(await TicketsAsync());
        await AcceptedAsync(source, Event("e-2"));
        Assert.Equal(2, (await TicketsAsync()).Count);

        // Another source has its own event ids and alert keys.
        await AcceptedAsync(await SourceAsync("Nagios"), Event("e-1"));
        Assert.Equal(3, (await TicketsAsync()).Count);
    }

    private Task CloseAsync() => TestData.SaveAsync(factory, db =>
    {
        foreach (var ticket in db.Tickets)
        {
            if (ticket.Status == TicketStatus.Open)
            {
                ticket.Complete("Tamam", TicketOutcome.Resolved, hasRelatedRequest: false, isApproved: false, DateTimeOffset.UtcNow);
            }
        }
    });

    private Task AgeAlertsAsync(TimeSpan by) => TestData.SaveAsync(factory, db =>
    {
        foreach (var alert in db.MonitoringAlerts)
        {
            db.Entry(alert).Property(a => a.LastFiringAt).CurrentValue -= by;
            db.Entry(alert).Property(a => a.CreatedAt).CurrentValue -= by;
        }
    });

    [Fact]
    public async Task An_open_alert_gets_one_note_in_five_minutes_and_resolved_never_closes()
    {
        var source = await SourceAsync();
        await AcceptedAsync(source, Event("e-1"));
        await AcceptedAsync(source, Event("e-2"));
        Assert.Empty(await CommentsAsync());

        await AgeAlertsAsync(MonitoringLimits.NoteInterval);
        await AcceptedAsync(source, Event("e-3"));
        await AcceptedAsync(source, Event("e-4"));
        var note = Assert.Single(await CommentsAsync());
        Assert.Equal((Visibility.Internal, _world.DeskUser.Id), (note.Visibility, note.AuthorId));

        await AcceptedAsync(source, Event("e-5", "resolved"));
        await AcceptedAsync(source, Event("e-6", "resolved"));
        Assert.Equal(2, (await CommentsAsync()).Count);
        Assert.Equal(TicketStatus.Open, Assert.Single(await TicketsAsync()).Status);
    }

    [Fact]
    public async Task Resolved_without_an_open_incident_has_no_effect()
    {
        var source = await SourceAsync();
        await AcceptedAsync(source, Event("e-1", "resolved"));
        await AcceptedAsync(source, Event("e-2"));
        await CloseAsync();
        await AcceptedAsync(source, Event("e-3", "resolved"));
        Assert.Single(await TicketsAsync());
        Assert.Empty(await CommentsAsync());
    }

    [Fact]
    public async Task Parallel_events_of_one_alert_open_one_incident()
    {
        var source = await SourceAsync();
        await Task.WhenAll(Enumerable.Range(0, ParallelEvents).Select(i => AcceptedAsync(source, Event($"e-{i}"))));
        Assert.Single(await TicketsAsync());
        await Task.WhenAll(Enumerable.Range(0, ParallelEvents).Select(_ => AcceptedAsync(source, Event("same", alertKey: "cpu"))));
        Assert.Equal(2, (await TicketsAsync()).Count);
    }

    [Fact]
    public async Task The_hourly_limit_is_429_with_one_audit_row_and_keeps_the_event_for_later()
    {
        var source = await SourceAsync();
        for (var i = 0; i < MonitoringLimits.IncidentsPerHour; i++)
        {
            await AcceptedAsync(source, Event($"e-{i}", alertKey: $"k-{i}"));
        }

        for (var i = 0; i < 2; i++)
        {
            using var limited = await SendAsync(source, Event("late", alertKey: "late"));
            Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        }

        // An open incident still gets its notes.
        await AcceptedAsync(source, Event("resolve", "resolved", "k-1"));
        await using (var db = factory.Database.CreateDbContext())
        {
            Assert.Equal(1, await db.AuditEvents.CountAsync(e => e.Action == AuditActions.MonitoringLimitReached, Ct));
        }

        await AgeAlertsAsync(MonitoringLimits.IncidentWindow);
        await AcceptedAsync(source, Event("late", alertKey: "late"));
        Assert.Equal(MonitoringLimits.IncidentsPerHour + 1, (await TicketsAsync()).Count);
    }

    [Fact]
    public async Task A_replaced_secret_stops_at_once()
    {
        var source = await SourceAsync();
        using var replaced = await _world.Admin.PostAsync($"{SourcesPath}/{source.Id}/secret", new { version = source.Item.GetProperty("version").GetUInt32() });
        Assert.Equal(HttpStatusCode.OK, replaced.StatusCode);
        var fresh = (await _world.Admin.ReadJsonAsync<JsonElement>(replaced)).GetProperty("secret").GetString()!;
        Assert.NotEqual(source.Secret, fresh);
        using (var old = await SendAsync(source, Event("e-1")))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
        }

        await AcceptedAsync(source with { Secret = fresh }, Event("e-1"));
    }

    [Fact]
    public async Task The_secret_is_stored_protected_and_never_listed_or_audited()
    {
        var source = await SourceAsync();
        Assert.Matches("^[0-9a-f]{64}$", source.Secret);
        Assert.DoesNotContain("secret\"", (await TicketWorld.ReadAsync(_world.Admin, SourcesPath)).GetRawText(), StringComparison.OrdinalIgnoreCase);
        await using var db = factory.Database.CreateDbContext();
        Assert.DoesNotContain(source.Secret, (await db.MonitoringSources.SingleAsync(Ct)).ProtectedSecret, StringComparison.Ordinal);
        var audits = await db.AuditEvents.Where(e => e.EntityType == AuditEntityTypes.MonitoringSource).ToListAsync(Ct);
        Assert.NotEmpty(audits);
        Assert.DoesNotContain(audits, e => $"{e.OldValues}{e.NewValues}".Contains(source.Secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Sources_are_for_admins_and_need_an_active_it_requester_and_a_free_name()
    {
        var source = await SourceAsync("İzleme");
        foreach (var client in new[] { _world.Desk, _world.Employee })
        {
            foreach (var call in new[]
            {
                client.GetAsync(SourcesPath), client.PostAsync(SourcesPath, Draft("X")), client.PutAsync($"{SourcesPath}/{source.Id}", WithVersion(source.Item)),
                client.PostAsync($"{SourcesPath}/{source.Id}/secret", new { version = 1 }),
            })
            {
                using var denied = await call;
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            }
        }

        using var taken = await _world.Admin.PostAsync(SourcesPath, Draft("izleme"));
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.MonitoringNameTaken), (taken.StatusCode, await ApiClient.ReadCodeAsync(taken)));
        var (_, employee) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "requester@example.com");
        using var requester = await _world.Admin.PostAsync(SourcesPath, Draft("Yeni", requesterId: employee.Id));
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.MonitoringRequesterInvalid), (requester.StatusCode, await ApiClient.ReadCodeAsync(requester)));
    }

    [Fact]
    public async Task A_source_whose_requester_left_is_409_and_counted()
    {
        var source = await SourceAsync();
        await TestData.SaveAsync(factory, db => db.Users.Single(u => u.Id == _world.DeskUser.Id).Deactivate(DateTimeOffset.UtcNow));
        using var response = await SendAsync(source, Event("e-1"));
        Assert.Equal((HttpStatusCode.Conflict, ErrorCodes.MonitoringSourceUnusable), (response.StatusCode, await ApiClient.ReadCodeAsync(response)));
        Assert.Empty(await TicketsAsync());
        Assert.Equal(1, (await ItemAsync(source.Id)).GetProperty("failedCount").GetInt32());
    }

    [Fact]
    public async Task Events_do_not_make_an_admin_edit_fail()
    {
        var source = await SourceAsync();
        await AcceptedAsync(source, Event("e-1"));
        using var edit = await _world.Admin.PutAsync($"{SourcesPath}/{source.Id}", WithVersion(source.Item, name: "Yeni ad"));
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
    }

    [Fact]
    public async Task Cleanup_removes_event_ids_after_thirty_days()
    {
        var source = await SourceAsync();
        await AcceptedAsync(source, Event("old"));
        await AcceptedAsync(source, Event("new"));
        await TestData.SaveAsync(factory, db => db.Entry(db.MonitoringEvents.Single(e => e.EventId == "old")).Property(e => e.ReceivedAt).CurrentValue -= TimeSpan.FromDays(31));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<CleanupHandler>().HandleAsync(Ct);
        }

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal("new", (await db.MonitoringEvents.SingleAsync(Ct)).EventId);
    }

    [Fact]
    public void Only_the_webhook_skips_the_antiforgery_check()
    {
        var skipped = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Where(e => e.Metadata.GetMetadata<SkipCsrfMetadata>() is not null).Cast<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText);
        Assert.Equal(["/api/v1/monitoring/{sourceId:guid}/events"], skipped);
    }
}
