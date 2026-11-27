using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Api.IntegrationTests.Tickets;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Problems;

namespace ProteoSM.Api.IntegrationTests.Problems;

[Collection(ApiCollection.Name)]
public sealed class ProblemApiTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Desk_creates_a_problem_in_its_own_team_with_number_event_and_audit()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, ownerId: world.DeskUser.Id);

        Assert.Matches("^PRB-[0-9]{4}-000001$", ProblemWorld.NumberOf(problem));
        Assert.Equal("open", problem.GetProperty("status").GetString());
        Assert.Equal("Servis Masası", problem.GetProperty("ownerTeamName").GetString());
        Assert.Equal(world.DeskUser.Id, problem.GetProperty("ownerId").GetGuid());
        Assert.True(problem.GetProperty("canWork").GetBoolean());
        await using var db = factory.Database.CreateDbContext();
        var id = problem.GetProperty("id").GetGuid();
        Assert.Equal(ProblemEventKind.Created, (await db.ProblemEvents.SingleAsync(e => e.ProblemId == id, Ct)).Kind);
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.ProblemCreated && a.EntityId == id, Ct));
    }

    [Fact]
    public async Task Create_for_a_team_the_creator_is_not_in_is_refused_and_saves_nothing()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Desk.PostAsync(
            ProblemWorld.ProblemsPath,
            new { title = "X", description = "Y", impact = "low", ownerTeamId = world.FieldTeam.Id });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("problems.owner_team_not_allowed", await ApiClient.ReadCodeAsync(response));
        await using var db = factory.Database.CreateDbContext();
        Assert.Empty(await db.Problems.ToListAsync(Ct));
        Assert.Empty(await db.ProblemTicketLinks.ToListAsync(Ct));
    }

    [Fact]
    public async Task Admin_creates_for_any_team_and_owner_must_be_a_member()
    {
        var world = await TicketWorld.CreateAsync(factory);

        var problem = await ProblemWorld.CreateForTeamAsync(world.Admin, world.FieldTeam.Id, ownerId: world.FieldUser.Id);
        using var wrongOwner = await world.Admin.PostAsync(
            ProblemWorld.ProblemsPath,
            new { title = "X", description = "Y", impact = "low", ownerTeamId = world.FieldTeam.Id, ownerId = world.DeskUser.Id });

        Assert.Equal("Saha Destek", problem.GetProperty("ownerTeamName").GetString());
        Assert.Equal(HttpStatusCode.Conflict, wrongOwner.StatusCode);
        Assert.Equal("problems.owner_not_in_team", await ApiClient.ReadCodeAsync(wrongOwner));
    }

    [Fact]
    public async Task Unknown_incident_number_on_create_is_404_and_saves_nothing()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var response = await world.Desk.PostAsync(
            ProblemWorld.ProblemsPath,
            new { title = "X", description = "Y", impact = "low", ownerTeamId = world.ServiceDesk.Id, incidentNumber = "INC-2026-999999" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        Assert.Empty(await db.Problems.ToListAsync(Ct));
    }

    [Fact]
    public async Task Create_validates_title_and_impact()
    {
        var world = await TicketWorld.CreateAsync(factory);

        using var blank = await world.Desk.PostAsync(
            ProblemWorld.ProblemsPath, new { title = " ", description = "Y", impact = "low", ownerTeamId = world.ServiceDesk.Id });
        using var tooLong = await world.Desk.PostAsync(
            ProblemWorld.ProblemsPath, new { title = new string('a', 201), description = "Y", impact = "low", ownerTeamId = world.ServiceDesk.Id });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
    }

    [Fact]
    public async Task Other_team_staff_reads_but_cannot_work_and_employee_gets_not_found_everywhere()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        var seen = await TicketWorld.ReadAsync(world.Field, ProblemWorld.PathOf(problem));
        using var edit = await world.Field.PutAsync(ProblemWorld.PathOf(problem), ProblemWorld.EditBody(problem));
        using var start = await world.Field.PostAsync(ProblemWorld.PathOf(problem, "start"), new { version = ProblemWorld.VersionOf(problem) });
        using var note = await world.Field.PostAsync(ProblemWorld.PathOf(problem, "notes"), new { text = "hi" });

        Assert.False(seen.GetProperty("canWork").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, edit.StatusCode);
        Assert.Equal("common.forbidden", await ApiClient.ReadCodeAsync(edit));
        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, note.StatusCode);

        var path = ProblemWorld.PathOf(problem);
        var version = ProblemWorld.VersionOf(problem);
        var responses = new[]
        {
            await world.Employee.PostAsync(ProblemWorld.ProblemsPath, new { title = "X", description = "Y", impact = "low", ownerTeamId = world.ServiceDesk.Id }),
            await world.Employee.GetAsync(ProblemWorld.ProblemsPath),
            await world.Employee.GetAsync(path),
            await world.Employee.PutAsync(path, ProblemWorld.EditBody(problem)),
            await world.Employee.GetAsync(path + "/timeline"),
            await world.Employee.PostAsync(path + "/notes", new { text = "hi" }),
            await world.Employee.PostAsync(path + "/start", new { version }),
            await world.Employee.PostAsync(path + "/known-error", new { version }),
            await world.Employee.PostAsync(path + "/resolve", new { version, resolution = "x" }),
            await world.Employee.PostAsync(path + "/cancel", new { version, reason = "x" }),
            await world.Employee.PostAsync(path + "/transfer", new { version, teamId = world.FieldTeam.Id, reason = "x" }),
            await world.Employee.PostAsync(path + "/owner", new { version, ownerId = (Guid?)null }),
        };
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
    }

    [Fact]
    public async Task Lifecycle_start_known_error_needs_root_cause_and_workaround_then_resolve()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        using var started = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "start"), new { version = ProblemWorld.VersionOf(problem) });
        var investigating = await world.Desk.ReadJsonAsync<JsonElement>(started);
        using var tooEarly = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "known-error"), new { version = ProblemWorld.VersionOf(investigating) });
        using var saved = await world.Desk.PutAsync(
            ProblemWorld.PathOf(problem), ProblemWorld.EditBody(investigating, rootCause: "Sürücü hatası", workaround: "Eski sürücüye dön"));
        var edited = await world.Desk.ReadJsonAsync<JsonElement>(saved);
        using var known = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "known-error"), new { version = ProblemWorld.VersionOf(edited) });
        var knownError = await world.Desk.ReadJsonAsync<JsonElement>(known);
        using var noResolution = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "resolve"), new { version = ProblemWorld.VersionOf(knownError), resolution = " " });
        using var resolved = await world.Desk.PostAsync(
            ProblemWorld.PathOf(problem, "resolve"), new { version = ProblemWorld.VersionOf(knownError), resolution = "Sürücü güncellendi" });
        var done = await world.Desk.ReadJsonAsync<JsonElement>(resolved);

        Assert.Equal("investigating", investigating.GetProperty("status").GetString());
        Assert.Equal(HttpStatusCode.Conflict, tooEarly.StatusCode);
        Assert.Equal("problems.known_error_incomplete", await ApiClient.ReadCodeAsync(tooEarly));
        Assert.Equal("known_error", knownError.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, knownError.GetProperty("knownErrorAt").ValueKind);
        Assert.Equal(HttpStatusCode.BadRequest, noResolution.StatusCode);
        Assert.Equal("resolved", done.GetProperty("status").GetString());
        Assert.Equal("Sürücü güncellendi", done.GetProperty("resolution").GetString());
        await using var db = factory.Database.CreateDbContext();
        var id = problem.GetProperty("id").GetGuid();
        var kinds = await db.ProblemEvents.Where(e => e.ProblemId == id).OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).Select(e => e.Kind).ToListAsync(Ct);
        Assert.Equal(
            [ProblemEventKind.Created, ProblemEventKind.Started, ProblemEventKind.Updated, ProblemEventKind.KnownError, ProblemEventKind.Resolved], kinds);
        Assert.Equal(3, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ProblemStatusChanged && a.EntityId == id, Ct));
    }

    [Fact]
    public async Task Cancel_needs_a_reason_and_a_closed_problem_cannot_be_edited_or_moved()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        using var noReason = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "cancel"), new { version = ProblemWorld.VersionOf(problem), reason = "" });
        using var cancelled = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "cancel"), new { version = ProblemWorld.VersionOf(problem), reason = "Yanlış alarm" });
        var closed = await world.Desk.ReadJsonAsync<JsonElement>(cancelled);
        using var edit = await world.Desk.PutAsync(ProblemWorld.PathOf(problem), ProblemWorld.EditBody(closed));
        using var start = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "start"), new { version = ProblemWorld.VersionOf(closed) });

        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        Assert.Equal("cancelled", closed.GetProperty("status").GetString());
        Assert.Equal("Yanlış alarm", closed.GetProperty("cancelReason").GetString());
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        Assert.Equal("problems.closed", await ApiClient.ReadCodeAsync(edit));
        Assert.Equal(HttpStatusCode.Conflict, start.StatusCode);
        Assert.Equal("problems.invalid_transition", await ApiClient.ReadCodeAsync(start));
    }

    [Fact]
    public async Task Stale_version_is_a_concurrency_conflict()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        using var first = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "start"), new { version = ProblemWorld.VersionOf(problem) });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var stale = await world.Desk.PutAsync(ProblemWorld.PathOf(problem), ProblemWorld.EditBody(problem, title: "Başka başlık"));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("concurrency.conflict", await ApiClient.ReadCodeAsync(stale));
    }

    [Fact]
    public async Task Note_adds_a_history_row_without_a_version_and_shows_in_the_timeline()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        using var note = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "notes"), new { text = "  Sürücüyü denedim  " });
        using var blank = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "notes"), new { text = " " });
        var timeline = await TicketWorld.ReadAsync(world.Field, ProblemWorld.PathOf(problem, "timeline"));

        Assert.Equal(HttpStatusCode.NoContent, note.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        var rows = timeline.EnumerateArray().ToList();
        Assert.Equal(["created", "commented"], rows.Select(r => r.GetProperty("kind").GetString()));
        Assert.Equal("Sürücüyü denedim", rows[1].GetProperty("data").GetProperty("text").GetString());
    }

    [Fact]
    public async Task Transfer_clears_the_owner_records_the_event_and_hands_the_work_over()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, ownerId: world.DeskUser.Id);

        using var response = await world.Desk.PostAsync(
            ProblemWorld.PathOf(problem, "transfer"),
            new { version = ProblemWorld.VersionOf(problem), teamId = world.FieldTeam.Id, reason = "Saha ekibi inceler" });
        var moved = await world.Desk.ReadJsonAsync<JsonElement>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Saha Destek", moved.GetProperty("ownerTeamName").GetString());
        Assert.Equal(JsonValueKind.Null, moved.GetProperty("ownerId").ValueKind);
        Assert.False(moved.GetProperty("canWork").GetBoolean());
        await using var db = factory.Database.CreateDbContext();
        var id = problem.GetProperty("id").GetGuid();
        Assert.Contains(ProblemEventKind.Transferred, await db.ProblemEvents.Where(e => e.ProblemId == id).Select(e => e.Kind).ToListAsync(Ct));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.ProblemTransferred && a.EntityId == id, Ct));
        var fieldView = await TicketWorld.ReadAsync(world.Field, ProblemWorld.PathOf(problem));
        Assert.True(fieldView.GetProperty("canWork").GetBoolean());
    }

    [Fact]
    public async Task Transfer_to_the_current_team_is_refused_and_keeps_the_owner()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, ownerId: world.DeskUser.Id);

        using var same = await world.Desk.PostAsync(
            ProblemWorld.PathOf(problem, "transfer"),
            new { version = ProblemWorld.VersionOf(problem), teamId = world.ServiceDesk.Id, reason = "Aynı ekip" });
        using var blankReason = await world.Desk.PostAsync(
            ProblemWorld.PathOf(problem, "transfer"),
            new { version = ProblemWorld.VersionOf(problem), teamId = world.FieldTeam.Id, reason = " " });

        Assert.Equal(HttpStatusCode.Conflict, same.StatusCode);
        Assert.Equal("problems.transfer_same_team", await ApiClient.ReadCodeAsync(same));
        Assert.Equal(HttpStatusCode.BadRequest, blankReason.StatusCode);
        var detail = await TicketWorld.ReadAsync(world.Desk, ProblemWorld.PathOf(problem));
        Assert.Equal(world.DeskUser.Id, detail.GetProperty("ownerId").GetGuid());
    }

    [Fact]
    public async Task Owner_can_be_set_and_cleared_only_inside_the_owner_team()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var problem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);

        using var set = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "owner"), new { version = ProblemWorld.VersionOf(problem), ownerId = world.DeskUser.Id });
        var withOwner = await world.Desk.ReadJsonAsync<JsonElement>(set);
        using var outsider = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "owner"), new { version = ProblemWorld.VersionOf(withOwner), ownerId = world.FieldUser.Id });
        using var cleared = await world.Desk.PostAsync(ProblemWorld.PathOf(problem, "owner"), new { version = ProblemWorld.VersionOf(withOwner), ownerId = (Guid?)null });
        var withoutOwner = await world.Desk.ReadJsonAsync<JsonElement>(cleared);

        Assert.Equal(world.DeskUser.Id, withOwner.GetProperty("ownerId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, outsider.StatusCode);
        Assert.Equal("problems.owner_not_in_team", await ApiClient.ReadCodeAsync(outsider));
        Assert.Equal(JsonValueKind.Null, withoutOwner.GetProperty("ownerId").ValueKind);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(2, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ProblemOwnerChanged, Ct));
    }

    [Fact]
    public async Task Queues_filters_and_search_narrow_the_list()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var mine = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "Işık sorunu", world.DeskUser.Id, "high");
        var team = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "Ağ kesintisi");
        var other = await ProblemWorld.CreateForTeamAsync(world.Field, world.FieldTeam.Id, "Yazıcı arızası", impact: "low");
        using var started = await world.Desk.PostAsync(ProblemWorld.PathOf(team, "start"), new { version = ProblemWorld.VersionOf(team) });
        var investigating = await world.Desk.ReadJsonAsync<JsonElement>(started);
        using var saved = await world.Desk.PutAsync(ProblemWorld.PathOf(team), ProblemWorld.EditBody(investigating, "Kablo", "Yedek hat"));
        var edited = await world.Desk.ReadJsonAsync<JsonElement>(saved);
        using var known = await world.Desk.PostAsync(ProblemWorld.PathOf(team, "known-error"), new { version = ProblemWorld.VersionOf(edited) });
        using var cancelled = await world.Field.PostAsync(ProblemWorld.PathOf(other, "cancel"), new { version = ProblemWorld.VersionOf(other), reason = "Gereksiz" });
        Assert.Equal(HttpStatusCode.OK, known.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelled.StatusCode);

        Assert.Equal([ProblemWorld.NumberOf(mine)], await ListAsync(world.Desk, "queue=mine"));
        Assert.Equal(2, (await ListAsync(world.Desk, "queue=team")).Count);
        Assert.Equal(2, (await ListAsync(world.Desk, "queue=open")).Count);
        Assert.Equal([ProblemWorld.NumberOf(team)], await ListAsync(world.Desk, "queue=known_errors"));
        Assert.Equal([ProblemWorld.NumberOf(other)], await ListAsync(world.Admin, "status=cancelled"));
        Assert.Equal([ProblemWorld.NumberOf(mine)], await ListAsync(world.Field, "impact=high"));
        Assert.Equal([ProblemWorld.NumberOf(mine)], await ListAsync(world.Desk, "search=" + Uri.EscapeDataString("IŞIK")));
        Assert.Equal([ProblemWorld.NumberOf(team)], await ListAsync(world.Field, "search=" + Uri.EscapeDataString(ProblemWorld.NumberOf(team))));
        Assert.Equal(3, (await ListAsync(world.Field, "")).Count);
        using var badQueue = await world.Desk.GetAsync(ProblemWorld.ProblemsPath + "?queue=nope");
        Assert.Equal(HttpStatusCode.BadRequest, badQueue.StatusCode);
    }

    [Fact]
    public async Task Team_open_queue_lists_only_the_open_problems_of_my_teams()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var open = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "Açık sorun");
        var investigating = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "İncelenen sorun");
        var knownError = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "Bilinen sorun");
        var resolved = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "Çözülen sorun");
        var cancelled = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, "İptal sorun");
        await ProblemWorld.CreateForTeamAsync(world.Field, world.FieldTeam.Id, "Saha sorunu");
        using var started = await world.Desk.PostAsync(ProblemWorld.PathOf(investigating, "start"), new { version = ProblemWorld.VersionOf(investigating) });
        using var saved = await world.Desk.PutAsync(ProblemWorld.PathOf(knownError), ProblemWorld.EditBody(knownError, "Kablo", "Yedek hat"));
        var edited = await world.Desk.ReadJsonAsync<JsonElement>(saved);
        using var known = await world.Desk.PostAsync(ProblemWorld.PathOf(knownError, "known-error"), new { version = ProblemWorld.VersionOf(edited) });
        using var resolve = await world.Desk.PostAsync(ProblemWorld.PathOf(resolved, "resolve"), new { version = ProblemWorld.VersionOf(resolved), resolution = "Düzeltildi" });
        using var cancel = await world.Desk.PostAsync(ProblemWorld.PathOf(cancelled, "cancel"), new { version = ProblemWorld.VersionOf(cancelled), reason = "Gereksiz" });
        Assert.All(new[] { started, known, resolve, cancel }, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        var teamOpen = await ListAsync(world.Desk, "queue=team_open");

        Assert.Equal(
            new[] { open, investigating, knownError }.Select(ProblemWorld.NumberOf).Order(StringComparer.Ordinal),
            teamOpen.Order(StringComparer.Ordinal));
        Assert.Equal(5, (await ListAsync(world.Desk, "queue=team")).Count);
    }

    [Fact]
    public async Task List_rows_show_team_owner_and_incident_count()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id, ownerId: world.DeskUser.Id);

        var rows = await TicketWorld.ReadAsync(world.Field, ProblemWorld.ProblemsPath);

        var row = Assert.Single(rows.GetProperty("items").EnumerateArray());
        Assert.Equal("Servis Masası", row.GetProperty("ownerTeamName").GetString());
        Assert.Equal(0, row.GetProperty("incidentCount").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("ownerName").GetString()));
    }

    private static async Task<List<string>> ListAsync(ApiClient client, string query)
    {
        var page = await TicketWorld.ReadAsync(client, ProblemWorld.ProblemsPath + "?" + query);
        return [.. page.GetProperty("items").EnumerateArray().Select(p => p.GetProperty("number").GetString()!)];
    }
}
