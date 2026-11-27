using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Api.IntegrationTests.Problems;
using Proteone.Api.IntegrationTests.Tickets;
using Proteone.Application.Notifications;
using Proteone.Domain.Notifications;
using Proteone.Domain.Organization;
using Proteone.Domain.Problems;
using Proteone.Domain.Users;

namespace Proteone.Api.IntegrationTests.Notifications;

/// <summary>
/// Problem email events end to end: the action runs through the API, the outbox is dispatched and the assertions read the
/// email deliveries (recipient and rendered text). Team: Desk (the actor), Colleague and Other in Servis Masası.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class ProblemNotificationTests(ApiFactory factory) : IAsyncLifetime
{
    private const int Batch = 100;
    private const string ProblemNumberPrefix = "PRB-";
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private TicketWorld _world = null!;
    private User _colleague = null!;
    private User _other = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _world = await TicketWorld.CreateAsync(factory);
        _colleague = await AddMemberAsync("colleague@example.com", _world.ServiceDesk.Id);
        _other = await AddMemberAsync("other@example.com", _world.ServiceDesk.Id);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<User> AddMemberAsync(string email, Guid teamId)
    {
        var user = await TestUsers.CreateAsync(factory, UserRole.ItStaff, email);
        await using var db = factory.Database.CreateDbContext();
        db.TeamMemberships.Add(TeamMembership.Create(teamId, user.Id, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(Ct);
        return user;
    }

    private async Task DispatchAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(Batch, Lease, Ct);
    }

    private async Task<List<EmailDelivery>> DeliveriesAsync(string eventType)
    {
        await DispatchAsync();
        await using var db = factory.Database.CreateDbContext();
        return await db.EmailDeliveries.AsNoTracking().Where(d => d.TemplateType == eventType).ToListAsync(Ct);
    }

    private async Task<int> OutboxCountAsync(string eventType)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.OutboxEvents.CountAsync(e => e.EventType == eventType, Ct);
    }

    private static List<Guid> Sorted(params Guid[] ids) => [.. ids.Order()];

    private static List<Guid> RecipientsOf(IEnumerable<EmailDelivery> deliveries) => [.. deliveries.Select(d => d.RecipientUserId).Order()];

    private async Task<JsonElement> StartedProblemAsync(Guid? ownerId = null, Guid? teamId = null)
    {
        var problem = await ProblemWorld.CreateForTeamAsync(_world.Desk, teamId ?? _world.ServiceDesk.Id, ownerId: ownerId);
        return await PostAsync(problem, "start", new { version = ProblemWorld.VersionOf(problem) });
    }

    private async Task<JsonElement> PostAsync(JsonElement problem, string action, object body)
    {
        using var response = await _world.Desk.PostAsync(ProblemWorld.PathOf(problem, action), body);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return await _world.Desk.ReadJsonAsync<JsonElement>(response);
    }

    private async Task<JsonElement> KnownErrorAsync(JsonElement investigating)
    {
        using var saved = await _world.Desk.PutAsync(
            ProblemWorld.PathOf(investigating), ProblemWorld.EditBody(investigating, rootCause: "Sürücü hatası", workaround: "Eski sürücüye dön"));
        var edited = await _world.Desk.ReadJsonAsync<JsonElement>(saved);
        return await PostAsync(edited, "known-error", new { version = ProblemWorld.VersionOf(edited) });
    }

    private async Task<JsonElement> LinkedIncidentAsync(JsonElement problem, string subject, Guid? assigneeId = null)
    {
        var incident = await TicketWorld.CreateIncidentAsync(_world.Employee, subject);
        if (assigneeId is { } assignee)
        {
            await using var db = factory.Database.CreateDbContext();
            var ticket = await db.Tickets.SingleAsync(t => t.Id == incident.GetProperty("id").GetGuid(), Ct);
            ticket.Assign(_world.ServiceDesk.Id, assignee, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(Ct);
        }

        using var link = await _world.Desk.PostAsync(ProblemWorld.PathOf(problem, "tickets"), new { ticketNumber = TicketWorld.NumberOf(incident) });
        Assert.True(link.IsSuccessStatusCode, await link.Content.ReadAsStringAsync(Ct));
        return incident;
    }

    [Fact]
    public async Task Created_mails_team_members_except_actor()
    {
        await ProblemWorld.CreateForTeamAsync(_world.Desk, _world.ServiceDesk.Id);

        var deliveries = await DeliveriesAsync(NotificationEventTypes.ProblemCreated);

        Assert.Equal(Sorted(_colleague.Id, _other.Id), RecipientsOf(deliveries));
        Assert.All(deliveries, d => Assert.Contains(ProblemNumberPrefix, d.Subject, StringComparison.Ordinal));
        Assert.All(deliveries, d => Assert.DoesNotContain("{{", d.Body!, StringComparison.Ordinal));
        Assert.Empty(await DeliveriesAsync(NotificationEventTypes.ProblemAssigned));
    }

    [Fact]
    public async Task Created_with_owner_sends_owner_only_assigned()
    {
        await ProblemWorld.CreateForTeamAsync(_world.Desk, _world.ServiceDesk.Id, ownerId: _colleague.Id);

        var created = await DeliveriesAsync(NotificationEventTypes.ProblemCreated);
        var assigned = await DeliveriesAsync(NotificationEventTypes.ProblemAssigned);

        Assert.Equal([_other.Id], RecipientsOf(created));
        Assert.Equal([_colleague.Id], RecipientsOf(assigned));
    }

    [Fact]
    public async Task Owner_change_mails_new_owner()
    {
        var problem = await ProblemWorld.CreateForTeamAsync(_world.Desk, _world.ServiceDesk.Id);
        await DispatchAsync();

        await PostAsync(problem, "owner", new { version = ProblemWorld.VersionOf(problem), ownerId = _other.Id });

        var assigned = await DeliveriesAsync(NotificationEventTypes.ProblemAssigned);
        Assert.Equal([_other.Id], RecipientsOf(assigned));
    }

    [Fact]
    public async Task Clearing_the_owner_mails_nobody()
    {
        var problem = await ProblemWorld.CreateForTeamAsync(_world.Desk, _world.ServiceDesk.Id, ownerId: _colleague.Id);

        await PostAsync(problem, "owner", new { version = ProblemWorld.VersionOf(problem), ownerId = (Guid?)null });

        Assert.Equal(1, await OutboxCountAsync(NotificationEventTypes.ProblemAssigned));
    }

    [Fact]
    public async Task Transfer_mails_new_team()
    {
        var problem = await ProblemWorld.CreateForTeamAsync(_world.Desk, _world.ServiceDesk.Id);
        await DispatchAsync();

        await PostAsync(problem, "transfer", new { version = ProblemWorld.VersionOf(problem), teamId = _world.FieldTeam.Id, reason = "Saha işi" });

        var assigned = await DeliveriesAsync(NotificationEventTypes.ProblemAssigned);
        Assert.Equal([_world.FieldUser.Id], RecipientsOf(assigned));
    }

    [Fact]
    public async Task Known_error_mails_linked_incident_assignees_once_each()
    {
        var problem = await StartedProblemAsync();
        await LinkedIncidentAsync(problem, "Yazıcı A", assigneeId: _colleague.Id);
        await LinkedIncidentAsync(problem, "Yazıcı B", assigneeId: _colleague.Id);
        await LinkedIncidentAsync(problem, "Yazıcı C");

        await KnownErrorAsync(problem);

        var deliveries = await DeliveriesAsync(NotificationEventTypes.ProblemKnownError);
        // Colleague holds two incidents and is also a member of the team of the unassigned third: one mail only.
        Assert.Equal(Sorted(_colleague.Id, _other.Id), RecipientsOf(deliveries));
        Assert.Equal(deliveries.Count, deliveries.Select(d => d.RecipientUserId).Distinct().Count());
    }

    [Fact]
    public async Task Known_error_mails_open_incident_requesters_without_problem_data()
    {
        var problem = await StartedProblemAsync();
        var first = await LinkedIncidentAsync(problem, "Yazıcı A");
        var second = await LinkedIncidentAsync(problem, "Yazıcı B");

        await KnownErrorAsync(problem);

        var deliveries = await DeliveriesAsync(NotificationEventTypes.TicketCauseFound);
        Assert.Equal(2, deliveries.Count);
        Assert.All(deliveries, d => Assert.Equal(_world.EmployeeUser.Id, d.RecipientUserId));
        Assert.All(deliveries, d => Assert.DoesNotContain(ProblemNumberPrefix, d.Subject + d.Body, StringComparison.Ordinal));
        Assert.All(deliveries, d => Assert.DoesNotContain("/problems/", d.Body!, StringComparison.Ordinal));
        Assert.All(deliveries, d => Assert.DoesNotContain("Sürücü hatası", d.Body!, StringComparison.Ordinal));
        Assert.All(deliveries, d => Assert.DoesNotContain("{{", d.Body!, StringComparison.Ordinal));
        foreach (var number in new[] { TicketWorld.NumberOf(first), TicketWorld.NumberOf(second) })
        {
            Assert.Single(deliveries, d => d.Body!.Contains($"/tickets/{number}", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Completed_incident_gets_no_cause_mail()
    {
        var problem = await StartedProblemAsync();
        var done = await LinkedIncidentAsync(problem, "Yazıcı A", assigneeId: _world.FieldUser.Id);
        var open = await LinkedIncidentAsync(problem, "Yazıcı B");
        // The assignment and the link moved the ticket on, so the version from creation is stale.
        var current = await TicketWorld.ReadAsync(_world.Desk, TicketWorld.PathOf(done));
        using var completed = await _world.Desk.PostAsync(
            TicketWorld.PathOf(done, "complete"),
            new { version = TicketWorld.VersionOf(current), resolution = "Çözüldü", outcome = "resolved" });
        Assert.True(completed.IsSuccessStatusCode, await completed.Content.ReadAsStringAsync(Ct));

        await KnownErrorAsync(problem);

        var cause = await DeliveriesAsync(NotificationEventTypes.TicketCauseFound);
        var only = Assert.Single(cause);
        Assert.Contains($"/tickets/{TicketWorld.NumberOf(open)}", only.Body!, StringComparison.Ordinal);
        // The completed incident's assignee (outside the team) gets no problem mail through it; the open one has no assignee, so its team is told.
        var known = await DeliveriesAsync(NotificationEventTypes.ProblemKnownError);
        Assert.Equal(Sorted(_colleague.Id, _other.Id), RecipientsOf(known));
    }

    [Fact]
    public async Task Resolve_after_known_error_sends_resolved_pair()
    {
        var problem = await StartedProblemAsync();
        await LinkedIncidentAsync(problem, "Yazıcı A", assigneeId: _colleague.Id);
        var knownError = await KnownErrorAsync(problem);

        await PostAsync(knownError, "resolve", new { version = ProblemWorld.VersionOf(knownError), resolution = "Sürücü güncellendi" });

        var resolved = await DeliveriesAsync(NotificationEventTypes.ProblemResolved);
        var cause = await DeliveriesAsync(NotificationEventTypes.TicketCauseResolved);
        Assert.Equal([_colleague.Id], RecipientsOf(resolved));
        Assert.Equal([_world.EmployeeUser.Id], RecipientsOf(cause));
        Assert.DoesNotContain(ProblemNumberPrefix, cause[0].Subject + cause[0].Body, StringComparison.Ordinal);
        Assert.Equal(1, await OutboxCountAsync(NotificationEventTypes.ProblemKnownError));
    }

    [Fact]
    public async Task Resolve_straight_from_investigating_sends_only_the_resolved_pair()
    {
        var problem = await StartedProblemAsync();
        await LinkedIncidentAsync(problem, "Yazıcı A", assigneeId: _colleague.Id);

        await PostAsync(problem, "resolve", new { version = ProblemWorld.VersionOf(problem), resolution = "Sürücü güncellendi" });

        Assert.Equal(1, await OutboxCountAsync(NotificationEventTypes.ProblemResolved));
        Assert.Equal(1, await OutboxCountAsync(NotificationEventTypes.TicketCauseResolved));
        Assert.Equal(0, await OutboxCountAsync(NotificationEventTypes.ProblemKnownError));
        Assert.Equal(0, await OutboxCountAsync(NotificationEventTypes.TicketCauseFound));
    }

    [Fact]
    public async Task Sla_event_with_an_inactive_owner_falls_back_to_the_team_members()
    {
        var problem = await ProblemWorld.CreateForTeamAsync(_world.Desk, _world.ServiceDesk.Id, ownerId: _colleague.Id);
        await DispatchAsync();
        await using (var db = factory.Database.CreateDbContext())
        {
            var owner = await db.Users.SingleAsync(u => u.Id == _colleague.Id, Ct);
            owner.Deactivate(DateTimeOffset.UtcNow);
            db.OutboxEvents.Add(OutboxEvent.CreateForSubject(
                NotificationEventTypes.ProblemSlaBreached, OutboxSubjectTypes.Problem, problem.GetProperty("id").GetGuid(), null,
                JsonSerializer.Serialize(new Dictionary<string, string> { [OutboxEvent.SlaDueAtKey] = DateTimeOffset.UtcNow.ToString("O") }), DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(Ct);
        }

        var deliveries = await DeliveriesAsync(NotificationEventTypes.ProblemSlaBreached);

        Assert.Equal(Sorted(_world.DeskUser.Id, _other.Id), RecipientsOf(deliveries));
    }

    [Fact]
    public async Task Templates_exist_for_all_new_events()
    {
        string[] eventTypes =
        [
            NotificationEventTypes.ProblemCreated, NotificationEventTypes.ProblemAssigned, NotificationEventTypes.ProblemKnownError,
            NotificationEventTypes.ProblemResolved, NotificationEventTypes.TicketCauseFound, NotificationEventTypes.TicketCauseResolved,
        ];
        await using var db = factory.Database.CreateDbContext();

        var templates = await db.EmailTemplates.AsNoTracking().Where(t => eventTypes.Contains(t.EventType)).ToListAsync(Ct);

        Assert.Equal(eventTypes.Order(), templates.Select(t => t.EventType).Order());
        foreach (var template in templates.Where(t => t.EventType.StartsWith("ticket_cause", StringComparison.Ordinal)))
        {
            Assert.DoesNotContain("problem", template.Subject + template.Body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
