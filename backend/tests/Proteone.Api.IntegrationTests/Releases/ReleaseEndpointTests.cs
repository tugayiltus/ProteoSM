using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proteone.Api.IntegrationTests.Changes;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Api.IntegrationTests.Tickets;
using Proteone.Application.Common;
using Proteone.Domain.Changes;
using Proteone.Domain.Common;

namespace Proteone.Api.IntegrationTests.Releases;

[Collection(ApiCollection.Name)]
public sealed class ReleaseEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ReleasesPath = "/api/v1/releases";
    private const string Reason = "Vazgeçildi";

    private TicketWorld _world = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        _world = await TicketWorld.CreateAsync(factory);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string NumberOf(JsonElement release) => release.GetProperty("number").GetString()!;

    private static uint VersionOf(JsonElement release) => release.GetProperty("version").GetUInt32();

    private static string StatusOf(JsonElement release) => release.GetProperty("status").GetString()!;

    private static string PathOf(JsonElement release, string? action = null) => $"{ReleasesPath}/{NumberOf(release)}" + (action is null ? "" : "/" + action);

    private static async Task<JsonElement> JsonAsync(ApiClient client, Task<HttpResponseMessage> call, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await call;
        Assert.Equal(expected, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static async Task AssertProblemAsync(Task<HttpResponseMessage> call, HttpStatusCode status, string? code = null)
    {
        using var response = await call;
        Assert.Equal(status, response.StatusCode);
        if (code is not null)
        {
            Assert.Equal(code, await ApiClient.ReadCodeAsync(response));
        }
    }

    private Task<JsonElement> CreateAsync(string title = "Kasım sürümü") =>
        JsonAsync(_world.Desk, _world.Desk.PostAsync(ReleasesPath, Draft(title)), HttpStatusCode.Created);

    private object Draft(string title, Guid? teamId = null, object? window = null) =>
        new { title, description = "Birlikte yayına çıkanlar", ownerTeamId = teamId ?? _world.ServiceDesk.Id, windowStart = (string?)null, windowEnd = (string?)null, notes = window };

    private Task<HttpResponseMessage> AddAsync(ApiClient client, JsonElement release, JsonElement change) =>
        client.PostAsync(PathOf(release, "changes"), new { changeNumber = ChangeWorld.NumberOf(change) });

    private static Task<HttpResponseMessage> ActAsync(ApiClient client, JsonElement release, string action) =>
        client.PostAsync(PathOf(release, action), new { version = VersionOf(release) });

    private Task SetStatusAsync(JsonElement change, ChangeStatus status) =>
        TestData.SaveAsync(factory, db => db.Entry(db.ChangeRequests.Single(c => c.Number == ChangeWorld.NumberOf(change))).Property(c => c.Status).CurrentValue = status);

    [Fact]
    public async Task A_release_runs_from_planned_to_completed_once_its_changes_are_finished()
    {
        var change = await ChangeWorld.CreateAsync(_world.Desk, _world);
        var release = await CreateAsync();
        Assert.Matches("^REL-[0-9]{4}-000001$", NumberOf(release));
        Assert.Equal("planned", StatusOf(release));

        release = await JsonAsync(_world.Desk, AddAsync(_world.Desk, release, change));
        Assert.Equal(ChangeWorld.NumberOf(change), release.GetProperty("changes")[0].GetProperty("number").GetString());
        var detail = await ChangeWorld.DetailAsync(_world.Desk, change);
        Assert.Equal(NumberOf(release), detail.GetProperty("release").GetProperty("number").GetString());
        Assert.Equal("draft", detail.GetProperty("status").GetString());

        await AssertProblemAsync(ActAsync(_world.Desk, release, "complete"), HttpStatusCode.Conflict, ErrorCodes.ReleaseInvalidTransition);
        release = await JsonAsync(_world.Desk, ActAsync(_world.Desk, release, "start"));
        await AssertProblemAsync(ActAsync(_world.Desk, release, "complete"), HttpStatusCode.Conflict, ErrorCodes.ReleaseOpenChanges);
        await SetStatusAsync(change, ChangeStatus.Closed);
        release = await JsonAsync(_world.Desk, ActAsync(_world.Desk, release, "complete"));
        Assert.Equal("completed", StatusOf(release));

        await AssertProblemAsync(
            _world.Desk.DeleteAsync(PathOf(release, $"changes/{ChangeWorld.NumberOf(change)}")), HttpStatusCode.Conflict, ErrorCodes.ReleaseClosed);
        var timeline = await TicketWorld.ReadAsync(_world.Desk, PathOf(release, "timeline"));
        Assert.Equal(["created", "change_added", "started", "completed"], timeline.EnumerateArray().Select(e => e.GetProperty("kind").GetString()));
    }

    [Fact]
    public async Task An_empty_release_cannot_be_completed()
    {
        var release = await JsonAsync(_world.Desk, ActAsync(_world.Desk, await CreateAsync(), "start"));
        await AssertProblemAsync(ActAsync(_world.Desk, release, "complete"), HttpStatusCode.Conflict, ErrorCodes.ReleaseOpenChanges);
    }

    [Fact]
    public async Task A_change_is_in_one_release_and_a_finished_change_cannot_join()
    {
        var change = await ChangeWorld.CreateAsync(_world.Desk, _world);
        var first = await CreateAsync();
        var second = await CreateAsync("İkinci");
        await JsonAsync(_world.Desk, AddAsync(_world.Desk, first, change));
        await AssertProblemAsync(AddAsync(_world.Desk, second, change), HttpStatusCode.Conflict, ErrorCodes.ReleaseChangeTaken);

        await JsonAsync(_world.Desk, _world.Desk.DeleteAsync(PathOf(first, $"changes/{ChangeWorld.NumberOf(change)}")));
        await AssertProblemAsync(_world.Desk.DeleteAsync(PathOf(first, $"changes/{ChangeWorld.NumberOf(change)}")), HttpStatusCode.NotFound);
        await SetStatusAsync(change, ChangeStatus.Cancelled);
        await AssertProblemAsync(AddAsync(_world.Desk, second, change), HttpStatusCode.Conflict, ErrorCodes.ReleaseChangeFinished);
    }

    [Fact]
    public async Task Joining_a_release_keeps_the_plan_and_the_approval()
    {
        var (change, _) = await ChangeWorld.ScheduledAsync(_world);
        var before = await ChangeWorld.DetailAsync(_world.Desk, change);
        await JsonAsync(_world.Desk, AddAsync(_world.Desk, await CreateAsync(), change));
        var after = await ChangeWorld.DetailAsync(_world.Desk, change);
        Assert.Equal("scheduled", after.GetProperty("status").GetString());
        Assert.Equal(before.GetProperty("approval").GetRawText(), after.GetProperty("approval").GetRawText());
        await ChangeWorld.ActAsync(_world.Desk, after, "start-implementation");
    }

    [Fact]
    public async Task Cancelling_frees_the_changes()
    {
        var change = await ChangeWorld.CreateAsync(_world.Desk, _world);
        var release = await JsonAsync(_world.Desk, AddAsync(_world.Desk, await CreateAsync(), change));
        await AssertProblemAsync(_world.Desk.PostAsync(PathOf(release, "cancel"), new { version = VersionOf(release), reason = " " }), HttpStatusCode.BadRequest);
        release = await JsonAsync(_world.Desk, _world.Desk.PostAsync(PathOf(release, "cancel"), new { version = VersionOf(release), reason = Reason }));
        Assert.Equal("cancelled", StatusOf(release));
        Assert.Equal(0, release.GetProperty("changes").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, (await ChangeWorld.DetailAsync(_world.Desk, change)).GetProperty("release").ValueKind);
        var events = await TicketWorld.ReadAsync(_world.Desk, ChangeWorld.PathOf(change, "timeline"));
        Assert.Contains(events.EnumerateArray(), e => e.GetProperty("kind").GetString() == "release_cleared");
        await JsonAsync(_world.Desk, AddAsync(_world.Desk, await CreateAsync("Yeni"), change));
    }

    [Fact]
    public async Task Other_staff_read_with_limited_rows_and_cannot_write()
    {
        var change = await ChangeWorld.CreateAsync(_world.Desk, _world);
        var release = await JsonAsync(_world.Desk, AddAsync(_world.Desk, await CreateAsync(), change));
        var seen = await JsonAsync(_world.Field, _world.Field.GetAsync(PathOf(release)));
        Assert.False(seen.GetProperty("canWork").GetBoolean());
        Assert.False(seen.GetProperty("changes")[0].GetProperty("canOpen").GetBoolean());

        await AssertProblemAsync(ActAsync(_world.Field, release, "start"), HttpStatusCode.Forbidden);
        await AssertProblemAsync(_world.Field.DeleteAsync(PathOf(release, $"changes/{ChangeWorld.NumberOf(change)}")), HttpStatusCode.Forbidden);
        await AssertProblemAsync(_world.Field.PostAsync(ReleasesPath, Draft("Başkasının")), HttpStatusCode.Conflict, ErrorCodes.ReleaseOwnerTeamNotAllowed);

        // A release of the field team cannot pull in a change the field team cannot work on.
        var own = await JsonAsync(_world.Field, _world.Field.PostAsync(ReleasesPath, Draft("Saha", _world.FieldTeam.Id)), HttpStatusCode.Created);
        await AssertProblemAsync(AddAsync(_world.Field, own, change), HttpStatusCode.NotFound);
        Assert.True((await JsonAsync(_world.Admin, _world.Admin.GetAsync(PathOf(own)))).GetProperty("canWork").GetBoolean());
    }

    [Fact]
    public async Task Employees_get_not_found_everywhere()
    {
        var release = await CreateAsync();
        var employee = _world.Employee;
        foreach (var call in new[]
        {
            employee.GetAsync(ReleasesPath), employee.PostAsync(ReleasesPath, Draft("X")), employee.GetAsync(PathOf(release)),
            employee.PutAsync(PathOf(release), new { version = 1, title = "X", description = "Y", ownerTeamId = _world.ServiceDesk.Id }),
            employee.GetAsync(PathOf(release, "timeline")), ActAsync(employee, release, "start"), ActAsync(employee, release, "complete"),
            employee.PostAsync(PathOf(release, "cancel"), new { version = 1, reason = "x" }),
            employee.PostAsync(PathOf(release, "changes"), new { changeNumber = "CHG-2026-000001" }),
            employee.DeleteAsync(PathOf(release, "changes/CHG-2026-000001")),
        })
        {
            await AssertProblemAsync(call, HttpStatusCode.NotFound);
        }
    }

    [Fact]
    public async Task Edits_check_the_version_and_the_window()
    {
        var release = await CreateAsync();
        object Body(uint version, string? end) => new
        {
            version,
            title = "Yeni ad",
            description = "Açıklama",
            ownerTeamId = _world.ServiceDesk.Id,
            windowStart = "2026-12-02T20:00:00Z",
            windowEnd = end,
        };
        await AssertProblemAsync(_world.Desk.PutAsync(PathOf(release), Body(VersionOf(release), "2026-12-02T19:00:00Z")), HttpStatusCode.BadRequest);
        var edited = await JsonAsync(_world.Desk, _world.Desk.PutAsync(PathOf(release), Body(VersionOf(release), "2026-12-02T23:00:00Z")));
        Assert.Equal("Yeni ad", edited.GetProperty("title").GetString());
        await AssertProblemAsync(_world.Desk.PutAsync(PathOf(release), Body(VersionOf(release), null)), HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task List_and_search_find_a_release_by_number_and_turkish_text()
    {
        var release = await CreateAsync("İstanbul yayını");
        async Task<int> CountAsync(string query) => (await TicketWorld.ReadAsync(_world.Field, $"{ReleasesPath}?{query}")).GetProperty("totalCount").GetInt32();
        Assert.Equal(1, await CountAsync("q=istanbul"));
        Assert.Equal(1, await CountAsync($"q={NumberOf(release).ToLowerInvariant()}"));
        Assert.Equal(0, await CountAsync("q=%25"));
        Assert.Equal(1, await CountAsync("open=true&status=planned"));
        Assert.Equal(0, await CountAsync("status=completed"));

        var found = await TicketWorld.ReadAsync(_world.Field, $"/api/v1/search?q={NumberOf(release)}");
        Assert.Equal("release", found.GetProperty("exact").GetProperty("type").GetString());
        var employee = await TicketWorld.ReadAsync(_world.Employee, $"/api/v1/search?q={NumberOf(release)}");
        Assert.Equal(JsonValueKind.Null, employee.GetProperty("exact").ValueKind);
    }

    [Fact]
    public async Task Completion_waits_for_the_change_lock()
    {
        var change = await ChangeWorld.CreateAsync(_world.Desk, _world);
        var release = await JsonAsync(_world.Desk, AddAsync(_world.Desk, await CreateAsync(), change));
        release = await JsonAsync(_world.Desk, ActAsync(_world.Desk, release, "start"));
        await SetStatusAsync(change, ChangeStatus.Cancelled);
        var late = await ChangeWorld.CreateAsync(_world.Desk, _world);

        await using var other = factory.Database.CreateDbContext();
        await using var transaction = await other.Database.BeginTransactionAsync(Ct);
        await other.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SerializationLocks.ChangeWork})", Ct);
        var row = await other.ChangeRequests.SingleAsync(c => c.Number == ChangeWorld.NumberOf(late), Ct);
        row.AddToRelease(release.GetProperty("id").GetGuid(), DateTimeOffset.UtcNow);
        await other.SaveChangesAsync(Ct);

        var call = ActAsync(_world.Desk, release, "complete");
        await AdvisoryLocks.WaitUntilBlockedOrDoneAsync(factory, call, Ct);
        await transaction.CommitAsync(Ct);
        await AssertProblemAsync(call, HttpStatusCode.Conflict, ErrorCodes.ReleaseOpenChanges);
    }
}
