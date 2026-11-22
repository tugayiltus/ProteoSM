using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Tgysm.Api.IntegrationTests.Changes;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Tickets;
using Tgysm.Domain.Audit;
using Tgysm.Domain.Changes;
using Tgysm.Domain.Problems;
using Tgysm.Domain.Tickets;

namespace Tgysm.Api.IntegrationTests.Problems;

[Collection(ApiCollection.Name)]
public sealed class ProblemLinkTests(ApiFactory factory) : IAsyncLifetime
{
    private const string TicketsSegment = "tickets";
    private const string ChangesSegment = "changes";
    private const string ProblemSegment = "problem";
    private const string ProblemsSegment = "problems";
    private const string AlreadyLinkedCode = "problems.ticket_already_linked";
    private const string NotIncidentCode = "problems.ticket_not_incident";
    private const string ClosedCode = "problems.closed";
    private const string ChangeLinkExistsCode = "problems.change_link_exists";
    private const string ForbiddenCode = "common.forbidden";
    private const string ServiceDeskName = "Servis Masası";
    private const string FieldTeamName = "Saha Destek";
    private const int ParallelProblems = 4;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> AddTicketAsync(ApiClient client, JsonElement problem, JsonElement ticket) =>
        client.PostAsync(ProblemWorld.PathOf(problem, TicketsSegment), new { ticketNumber = TicketWorld.NumberOf(ticket) });

    private static async Task<JsonElement> LinkedTicketAsync(ApiClient client, JsonElement problem, JsonElement ticket)
    {
        using var response = await AddTicketAsync(client, problem, ticket);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static Task<HttpResponseMessage> AddChangeAsync(ApiClient client, JsonElement problem, JsonElement change) =>
        client.PostAsync(ProblemWorld.PathOf(problem, ChangesSegment), new { changeNumber = ChangeWorld.NumberOf(change) });

    private static async Task<JsonElement> FieldChangeAsync(TicketWorld world)
    {
        using var response = await world.Admin.PostAsync(ChangeWorld.ChangesPath, new
        {
            draft = ChangeWorld.DraftBody("Saha anahtarı değişimi"),
            ownerTeamId = world.FieldTeam.Id,
            ownerId = world.FieldUser.Id,
            assetIds = Array.Empty<Guid>(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await world.Admin.ReadJsonAsync<JsonElement>(response);
    }

    private async Task<DateTimeOffset> TicketUpdatedAtAsync(Guid ticketId)
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.Tickets.Where(t => t.Id == ticketId).Select(t => t.UpdatedAt).SingleAsync(Ct);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) => await ApiClient.ReadCodeAsync(response);

    [Fact]
    public async Task Create_with_an_incident_number_links_it_in_the_same_save()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);

        var problem = await ProblemWorld.CreateAsync(world.Desk, new
        {
            title = "Yazıcı",
            description = "Tekrarlıyor",
            impact = "low",
            ownerTeamId = world.ServiceDesk.Id,
            incidentNumber = TicketWorld.NumberOf(incident),
        });

        var tickets = await TicketWorld.ReadAsync(world.Desk, ProblemWorld.PathOf(problem, TicketsSegment));
        var item = Assert.Single(tickets.EnumerateArray());
        Assert.Equal(TicketWorld.NumberOf(incident), item.GetProperty("number").GetString());
        var problemId = problem.GetProperty("id").GetGuid();
        var ticketId = incident.GetProperty("id").GetGuid();
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.ProblemTicketLinks.CountAsync(l => l.ProblemId == problemId && l.TicketId == ticketId, Ct));
        Assert.Equal(1, await db.ProblemEvents.CountAsync(e => e.ProblemId == problemId && e.Kind == ProblemEventKind.TicketLinked, Ct));
        Assert.Equal(1, await db.TicketEvents.CountAsync(e => e.TicketId == ticketId && e.Kind == TicketEventKind.ProblemLinked, Ct));
    }

    [Fact]
    public async Task Create_with_an_incident_already_on_a_problem_is_409_and_saves_no_problem()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var body = new { title = "Yazıcı", description = "Tekrarlıyor", impact = "low", ownerTeamId = world.ServiceDesk.Id, incidentNumber = TicketWorld.NumberOf(incident) };
        await ProblemWorld.CreateAsync(world.Desk, body);

        using var second = await world.Desk.PostAsync(ProblemWorld.ProblemsPath, body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(AlreadyLinkedCode, await CodeOfAsync(second));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.Problems.CountAsync(Ct));
        Assert.Equal(1, await db.ProblemTicketLinks.CountAsync(Ct));
    }

    [Fact]
    public async Task Create_from_an_incident_for_a_team_the_creator_is_not_in_saves_neither_problem_nor_link()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);

        using var response = await world.Desk.PostAsync(ProblemWorld.ProblemsPath, new
        {
            title = "Yazıcı",
            description = "Tekrarlıyor",
            impact = "low",
            ownerTeamId = world.FieldTeam.Id,
            incidentNumber = TicketWorld.NumberOf(incident),
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("problems.owner_team_not_allowed", await CodeOfAsync(response));
        await using var db = factory.Database.CreateDbContext();
        Assert.Empty(await db.Problems.ToListAsync(Ct));
        Assert.Empty(await db.ProblemTicketLinks.ToListAsync(Ct));
    }

    [Fact]
    public async Task Desk_links_its_incident_to_a_field_team_problem_but_cannot_edit_the_problem()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Field, world.FieldTeam.Id);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var ticketId = incident.GetProperty("id").GetGuid();
        var problemId = problem.GetProperty("id").GetGuid();
        var updatedBefore = await TicketUpdatedAtAsync(ticketId);

        var list = await LinkedTicketAsync(world.Desk, problem, incident);
        using var edit = await world.Desk.PutAsync(ProblemWorld.PathOf(problem), ProblemWorld.EditBody(problem, title: "Değişti"));

        var item = Assert.Single(list.EnumerateArray());
        Assert.Equal(TicketWorld.NumberOf(incident), item.GetProperty("number").GetString());
        Assert.Equal("Yazıcı çalışmıyor", item.GetProperty("title").GetString());
        Assert.Equal(ServiceDeskName, item.GetProperty("teamName").GetString());
        Assert.True(item.GetProperty("canOpen").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal(ForbiddenCode, await CodeOfAsync(edit));
        Assert.True(await TicketUpdatedAtAsync(ticketId) > updatedBefore);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.ProblemEvents.CountAsync(e => e.ProblemId == problemId && e.Kind == ProblemEventKind.TicketLinked, Ct));
        var ticketEvent = await db.TicketEvents.SingleAsync(e => e.TicketId == ticketId && e.Kind == TicketEventKind.ProblemLinked, Ct);
        Assert.Equal(Visibility.Internal, ticketEvent.Visibility);
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ProblemTicketLinked && a.EntityId == problemId, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketProblemLinked && a.EntityId == ticketId, Ct));
        var after = await TicketWorld.ReadAsync(world.Field, ProblemWorld.PathOf(problem));
        Assert.Equal(ProblemWorld.VersionOf(problem), ProblemWorld.VersionOf(after));
    }

    [Fact]
    public async Task Owner_team_links_another_teams_incident_it_can_see_but_not_one_it_cannot()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Field, world.FieldTeam.Id);
        var ownIncident = await TicketWorld.CreateIncidentAsync(world.Field, "Saha yazıcısı");
        var hiddenIncident = await TicketWorld.CreateIncidentAsync(world.Employee);

        var list = await LinkedTicketAsync(world.Field, problem, ownIncident);
        using var hidden = await AddTicketAsync(world.Field, problem, hiddenIncident);

        Assert.Equal(TicketWorld.NumberOf(ownIncident), Assert.Single(list.EnumerateArray()).GetProperty("number").GetString());
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Staff_who_works_neither_the_incident_nor_the_problem_is_forbidden()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var fieldRequested = await TicketWorld.CreateIncidentAsync(world.Field, "Saha yazıcısı");

        using var response = await AddTicketAsync(world.Field, problem, fieldRequested);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ForbiddenCode, await CodeOfAsync(response));
    }

    [Fact]
    public async Task Incident_on_problem_a_linked_to_problem_b_is_409_already_linked()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var first = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "A");
        var second = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "B");
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        await LinkedTicketAsync(world.Desk, first, incident);

        using var again = await AddTicketAsync(world.Desk, second, incident);
        using var same = await AddTicketAsync(world.Desk, first, incident);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(AlreadyLinkedCode, await CodeOfAsync(again));
        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
        Assert.Equal(AlreadyLinkedCode, await CodeOfAsync(same));
    }

    [Fact]
    public async Task Parallel_links_of_one_incident_to_several_problems_give_one_201_and_the_rest_409_never_500()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problems = new List<JsonElement>();
        for (var i = 0; i < ParallelProblems; i++)
        {
            problems.Add(await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, $"Problem {i}"));
        }

        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);

        var responses = await Task.WhenAll(problems.Select(p => AddTicketAsync(world.Desk, p, incident)));

        var statuses = responses.Select(r => r.StatusCode).ToList();
        var conflictCodes = new List<string?>();
        foreach (var response in responses)
        {
            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                conflictCodes.Add(await CodeOfAsync(response));
            }

            response.Dispose();
        }

        Assert.Equal(1, statuses.Count(c => c == HttpStatusCode.Created));
        Assert.Equal(ParallelProblems - 1, statuses.Count(c => c == HttpStatusCode.Conflict));
        Assert.All(conflictCodes, code => Assert.Equal(AlreadyLinkedCode, code));
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.ProblemTicketLinks.CountAsync(Ct));
    }

    [Fact]
    public async Task Request_is_409_not_incident()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var request = await world.CreateRequestAsync(world.Employee, approval: false);

        using var response = await AddTicketAsync(world.Desk, problem, request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(NotIncidentCode, await CodeOfAsync(response));
    }

    [Fact]
    public async Task Resolved_problem_takes_no_new_link_but_existing_links_can_be_removed()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var linked = await TicketWorld.CreateIncidentAsync(world.Employee);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await LinkedTicketAsync(world.Desk, problem, linked);
        using (var changeLink = await AddChangeAsync(world.Desk, problem, change))
        {
            Assert.Equal(HttpStatusCode.Created, changeLink.StatusCode);
        }

        using (var resolve = await world.Desk.PostAsync(
            ProblemWorld.PathOf(problem, "resolve"), new { version = ProblemWorld.VersionOf(problem), resolution = "Sürücü güncellendi" }))
        {
            Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        }

        var other = await TicketWorld.CreateIncidentAsync(world.Employee, "Başka arıza");
        var ticketId = linked.GetProperty("id").GetGuid();
        var updatedLinked = await TicketUpdatedAtAsync(ticketId);

        using var add = await AddTicketAsync(world.Desk, problem, other);
        using var addChange = await AddChangeAsync(world.Desk, problem, await ChangeWorld.CreateAsync(world.Desk, world));
        using var remove = await world.Desk.DeleteAsync($"{ProblemWorld.PathOf(problem, TicketsSegment)}/{TicketWorld.NumberOf(linked)}");
        using var removeChange = await world.Desk.DeleteAsync($"{ProblemWorld.PathOf(problem, ChangesSegment)}/{ChangeWorld.NumberOf(change)}");

        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
        Assert.Equal(ClosedCode, await CodeOfAsync(add));
        Assert.Equal(HttpStatusCode.Conflict, addChange.StatusCode);
        Assert.Equal(ClosedCode, await CodeOfAsync(addChange));
        Assert.Equal(HttpStatusCode.OK, remove.StatusCode);
        Assert.Equal(0, (await world.Desk.ReadJsonAsync<JsonElement>(remove)).GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, removeChange.StatusCode);
        Assert.Equal(0, (await world.Desk.ReadJsonAsync<JsonElement>(removeChange)).GetArrayLength());
        Assert.True(await TicketUpdatedAtAsync(ticketId) > updatedLinked);
        var problemId = problem.GetProperty("id").GetGuid();
        var changeId = change.GetProperty("id").GetGuid();
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.ProblemEvents.CountAsync(e => e.ProblemId == problemId && e.Kind == ProblemEventKind.TicketUnlinked, Ct));
        Assert.Equal(1, await db.ProblemEvents.CountAsync(e => e.ProblemId == problemId && e.Kind == ProblemEventKind.ChangeUnlinked, Ct));
        Assert.Equal(1, await db.TicketEvents.CountAsync(e => e.TicketId == ticketId && e.Kind == TicketEventKind.ProblemUnlinked, Ct));
        Assert.Equal(1, await db.ChangeEvents.CountAsync(e => e.ChangeId == changeId && e.Kind == ChangeEventKind.ProblemUnlinked, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ProblemTicketUnlinked && a.EntityId == problemId, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketProblemUnlinked && a.EntityId == ticketId, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ProblemChangeUnlinked && a.EntityId == problemId, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ChangeProblemUnlinked && a.EntityId == changeId, Ct));
    }

    [Fact]
    public async Task Owner_team_links_a_visible_change_once_and_an_invisible_change_is_404()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        var hiddenChange = await FieldChangeAsync(world);

        using var added = await AddChangeAsync(world.Desk, problem, change);
        using var duplicate = await AddChangeAsync(world.Desk, problem, change);
        using var hidden = await AddChangeAsync(world.Desk, problem, hiddenChange);
        using var notOwner = await AddChangeAsync(world.Field, problem, hiddenChange);

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        var item = Assert.Single((await world.Desk.ReadJsonAsync<JsonElement>(added)).EnumerateArray());
        Assert.Equal(ChangeWorld.NumberOf(change), item.GetProperty("number").GetString());
        Assert.Equal("Sunucu güncellemesi", item.GetProperty("title").GetString());
        Assert.True(item.GetProperty("canOpen").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(ChangeLinkExistsCode, await CodeOfAsync(duplicate));
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notOwner.StatusCode);
        var problemId = problem.GetProperty("id").GetGuid();
        var changeId = change.GetProperty("id").GetGuid();
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.ProblemChangeLinks.CountAsync(Ct));
        Assert.Equal(1, await db.ProblemEvents.CountAsync(e => e.ProblemId == problemId && e.Kind == ProblemEventKind.ChangeLinked, Ct));
        Assert.Equal(1, await db.ChangeEvents.CountAsync(e => e.ChangeId == changeId && e.Kind == ChangeEventKind.ProblemLinked, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ProblemChangeLinked && a.EntityId == problemId, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ChangeProblemLinked && a.EntityId == changeId, Ct));
    }

    [Fact]
    public async Task Rows_the_viewer_cannot_open_show_only_number_title_status()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Field, world.FieldTeam.Id);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await LinkedTicketAsync(world.Desk, problem, incident);
        using (var changeLink = await AddChangeAsync(world.Admin, problem, change))
        {
            Assert.Equal(HttpStatusCode.Created, changeLink.StatusCode);
        }

        var tickets = await TicketWorld.ReadAsync(world.Field, ProblemWorld.PathOf(problem, TicketsSegment));
        var changes = await TicketWorld.ReadAsync(world.Field, ProblemWorld.PathOf(problem, ChangesSegment));

        var ticket = Assert.Single(tickets.EnumerateArray());
        Assert.False(ticket.GetProperty("canOpen").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ticket.GetProperty("teamName").ValueKind);
        Assert.Equal("Yazıcı çalışmıyor", ticket.GetProperty("title").GetString());
        Assert.Equal("open", ticket.GetProperty("status").GetString());
        Assert.DoesNotContain(ServiceDeskName, tickets.GetRawText(), StringComparison.Ordinal);
        var linkedChange = Assert.Single(changes.EnumerateArray());
        Assert.False(linkedChange.GetProperty("canOpen").GetBoolean());
        Assert.Equal("Sunucu güncellemesi", linkedChange.GetProperty("title").GetString());
        Assert.DoesNotContain("window", changes.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ServiceDeskName, changes.GetRawText(), StringComparison.Ordinal);
        var deskView = await TicketWorld.ReadAsync(world.Desk, ProblemWorld.PathOf(problem, TicketsSegment));
        Assert.Equal(ServiceDeskName, Assert.Single(deskView.EnumerateArray()).GetProperty("teamName").GetString());
    }

    [Fact]
    public async Task Staff_reads_the_incidents_problem_and_an_unlinked_incident_is_204()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Field, world.FieldTeam.Id);
        using (var edit = await world.Field.PutAsync(ProblemWorld.PathOf(problem), ProblemWorld.EditBody(problem, workaround: "Yazıcıyı yeniden başlat")))
        {
            Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        }

        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var unlinked = await TicketWorld.CreateIncidentAsync(world.Employee, "Başka arıza");
        await LinkedTicketAsync(world.Desk, problem, incident);

        var linked = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(incident, ProblemSegment));
        using var none = await world.Desk.GetAsync(TicketWorld.PathOf(unlinked, ProblemSegment));

        Assert.Equal(ProblemWorld.NumberOf(problem), linked.GetProperty("number").GetString());
        Assert.Equal("Yazıcılar sık kapanıyor", linked.GetProperty("title").GetString());
        Assert.Equal("open", linked.GetProperty("status").GetString());
        Assert.Equal("Yazıcıyı yeniden başlat", linked.GetProperty("workaround").GetString());
        Assert.Equal(FieldTeamName, linked.GetProperty("ownerTeamName").GetString());
        Assert.Equal(HttpStatusCode.NoContent, none.StatusCode);
    }

    [Fact]
    public async Task Employee_requester_gets_404_on_problem_routes_and_ticket_detail_has_no_problem_field()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await LinkedTicketAsync(world.Desk, problem, incident);
        using (var changeLink = await AddChangeAsync(world.Desk, problem, change))
        {
            Assert.Equal(HttpStatusCode.Created, changeLink.StatusCode);
        }

        using var ticketProblem = await world.Employee.GetAsync(TicketWorld.PathOf(incident, ProblemSegment));
        using var changeProblems = await world.Employee.GetAsync(ChangeWorld.PathOf(change, ProblemsSegment));
        using var problemTickets = await world.Employee.GetAsync(ProblemWorld.PathOf(problem, TicketsSegment));
        using var link = await AddTicketAsync(world.Employee, problem, incident);
        var detail = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(incident));
        var timeline = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(incident, "timeline"));

        Assert.Equal(HttpStatusCode.NotFound, ticketProblem.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, changeProblems.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, problemTickets.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, link.StatusCode);
        Assert.DoesNotContain(ProblemSegment, detail.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("problem_linked", timeline.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Change_viewer_lists_the_problems_of_a_change()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        using (var changeLink = await AddChangeAsync(world.Desk, problem, change))
        {
            Assert.Equal(HttpStatusCode.Created, changeLink.StatusCode);
        }

        var problems = await TicketWorld.ReadAsync(world.Desk, ChangeWorld.PathOf(change, ProblemsSegment));
        using var hidden = await world.Field.GetAsync(ChangeWorld.PathOf(change, ProblemsSegment));

        var item = Assert.Single(problems.EnumerateArray());
        Assert.Equal(ProblemWorld.NumberOf(problem), item.GetProperty("number").GetString());
        Assert.Equal("Yazıcılar sık kapanıyor", item.GetProperty("title").GetString());
        Assert.Equal("open", item.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Removing_a_link_that_does_not_exist_is_404()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);
        var change = await ChangeWorld.CreateAsync(world.Desk, world);

        using var ticket = await world.Desk.DeleteAsync($"{ProblemWorld.PathOf(problem, TicketsSegment)}/{TicketWorld.NumberOf(incident)}");
        using var changeLink = await world.Desk.DeleteAsync($"{ProblemWorld.PathOf(problem, ChangesSegment)}/{ChangeWorld.NumberOf(change)}");

        Assert.Equal(HttpStatusCode.NotFound, ticket.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, changeLink.StatusCode);
    }
}
