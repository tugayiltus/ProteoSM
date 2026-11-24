using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Application.Common;
using Proteone.Application.Features.Licences;
using Proteone.Application.Notifications;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Licences;
using Proteone.Domain.Notifications;
using Proteone.Domain.Users;

namespace Proteone.Api.IntegrationTests.Licences;

[Collection(ApiCollection.Name)]
public sealed class LicenceEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SoftwarePath = "/api/v1/software";
    private const string LicencesPath = "/api/v1/licences";
    private const string MySoftwarePath = "/api/v1/me/software";
    private const string SecretKey = "ABCD-1234-SECRET";
    private const string PerUser = "per_user";
    private const string PerDevice = "per_device";

    private ApiClient _admin = null!;
    private ApiClient _tech = null!;
    private ApiClient _employee = null!;
    private User _techUser = null!;
    private User _employeeUser = null!;
    private Guid _teamId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        (_admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        (_tech, _techUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "tech@example.com");
        (_employee, _employeeUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "employee@example.com");
        _teamId = (await TestData.TeamAsync(factory, "Altyapı", false, _techUser.Id)).Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static Guid Id(JsonElement item) => item.GetProperty("id").GetGuid();

    private static uint Version(JsonElement item) => item.GetProperty("version").GetUInt32();

    private static async Task<JsonElement> JsonAsync(ApiClient client, HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using (response)
        {
            Assert.Equal(expected, response.StatusCode);
            return await client.ReadJsonAsync<JsonElement>(response);
        }
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        {
            Assert.Equal(status, response.StatusCode);
            Assert.Equal(code, await ApiClient.ReadCodeAsync(response));
        }
    }

    private async Task<JsonElement> SoftwareAsync(string name = "Office", string vendor = "Microsoft") =>
        await JsonAsync(_tech, await _tech.PostAsync(SoftwarePath, new { name, vendor }), HttpStatusCode.Created);

    private async Task<JsonElement> LicenceAsync(Guid softwareId, string kind = PerUser, int quantity = 1, DateOnly? expiresOn = null, string name = "Yıllık") =>
        await JsonAsync(
            _tech,
            await _tech.PostAsync(LicencesPath, new { softwareId, name, kind, quantity, ownerTeamId = _teamId, expiresOn, notes = (string?)null }),
            HttpStatusCode.Created);

    private Task<HttpResponseMessage> AssignUserAsync(Guid licenceId, Guid userId) =>
        _tech.PostAsync($"{LicencesPath}/{licenceId}/assignments", new { userId });

    private Task<HttpResponseMessage> UpdateAsync(JsonElement licence, int quantity, DateOnly? expiresOn = null) =>
        _tech.PutAsync(
            $"{LicencesPath}/{Id(licence)}",
            new { version = Version(licence), name = "Yıllık", quantity, ownerTeamId = _teamId, expiresOn, notes = (string?)null });

    private DateOnly Today()
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ICompanyClock>().DateOf(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Software_is_unique_per_vendor_and_name_ignoring_turkish_case()
    {
        var first = await SoftwareAsync("İstatistik", "Yazılım");
        await AssertProblemAsync(
            await _tech.PostAsync(SoftwarePath, new { name = "istatistik", vendor = "YAZILIM" }), HttpStatusCode.Conflict, ErrorCodes.SoftwareNameTaken);
        await SoftwareAsync("İstatistik", "Başka");
        await SoftwareAsync("Office 365", "Microsoft");
        await SoftwareAsync("365", "Microsoft Office");

        var updated = await JsonAsync(_tech, await _tech.PutAsync(
            $"{SoftwarePath}/{Id(first)}", new { version = Version(first), name = "İstatistik", vendor = "Yazılım", isActive = false }));
        Assert.False(updated.GetProperty("isActive").GetBoolean());
        await AssertProblemAsync(
            await _tech.PostAsync(LicencesPath, new { softwareId = Id(first), name = "X", kind = PerUser, quantity = 1, ownerTeamId = _teamId }),
            HttpStatusCode.Conflict, ErrorCodes.SoftwareInactive);
    }

    [Fact]
    public async Task A_full_licence_refuses_and_a_released_seat_is_free_again()
    {
        var licence = await LicenceAsync(Id(await SoftwareAsync()));
        Assert.Equal(1, (await JsonAsync(_tech, await AssignUserAsync(Id(licence), _employeeUser.Id))).GetProperty("used").GetInt32());

        await AssertProblemAsync(await AssignUserAsync(Id(licence), _techUser.Id), HttpStatusCode.Conflict, ErrorCodes.LicenceFull);
        await AssertProblemAsync(await AssignUserAsync(Id(licence), _employeeUser.Id), HttpStatusCode.Conflict, ErrorCodes.LicenceAlreadyAssigned);

        var seats = await JsonAsync(_tech, await _tech.GetAsync($"{LicencesPath}/{Id(licence)}/assignments"));
        var seat = seats.GetProperty("items")[0];
        Assert.True(seat.GetProperty("targetAvailable").GetBoolean());
        var seatUrl = $"{LicencesPath}/{Id(licence)}/assignments/{Id(seat)}";
        using (var released = await _tech.DeleteAsync(seatUrl))
        {
            Assert.Equal(HttpStatusCode.NoContent, released.StatusCode);
        }

        using (var again = await _tech.DeleteAsync(seatUrl))
        {
            Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        }

        await JsonAsync(_tech, await AssignUserAsync(Id(licence), _techUser.Id));
        var history = await JsonAsync(_tech, await _tech.GetAsync($"{LicencesPath}/{Id(licence)}/assignments?released=true"));
        Assert.Equal(1, history.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Quantity_never_falls_below_the_used_seats()
    {
        var licence = await LicenceAsync(Id(await SoftwareAsync()), quantity: 2);
        await JsonAsync(_tech, await AssignUserAsync(Id(licence), _employeeUser.Id));
        await JsonAsync(_tech, await AssignUserAsync(Id(licence), _techUser.Id));

        await AssertProblemAsync(await UpdateAsync(licence, 1), HttpStatusCode.Conflict, ErrorCodes.LicenceQuantityBelowUse);
        Assert.Equal(3, (await JsonAsync(_tech, await UpdateAsync(licence, 3))).GetProperty("quantity").GetInt32());
        await AssertProblemAsync(await UpdateAsync(licence, 4), HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task An_assignment_waits_for_the_seat_lock()
    {
        var licence = await LicenceAsync(Id(await SoftwareAsync()));
        await using var lockDb = factory.Database.CreateDbContext();
        await using var transaction = await lockDb.Database.BeginTransactionAsync(Ct);
        await lockDb.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SerializationLocks.LicenceSeats})", Ct);
        var row = await lockDb.Licences.SingleAsync(l => l.Id == Id(licence), Ct);
        lockDb.LicenceAssignments.Add(row.AssignTo(await lockDb.Users.SingleAsync(u => u.Id == _techUser.Id, Ct), 0, Today(), _techUser.Id, DateTimeOffset.UtcNow));
        await lockDb.SaveChangesAsync(Ct);

        var call = AssignUserAsync(Id(licence), _employeeUser.Id);
        await AdvisoryLocks.WaitUntilBlockedOrDoneAsync(factory, call, Ct);
        await transaction.CommitAsync(Ct);

        await AssertProblemAsync(await call, HttpStatusCode.Conflict, ErrorCodes.LicenceFull);
    }

    [Fact]
    public async Task Ended_licences_and_wrong_targets_are_refused()
    {
        var software = Id(await SoftwareAsync());
        var ended = await LicenceAsync(software, expiresOn: Today().AddDays(-1));
        await AssertProblemAsync(await AssignUserAsync(Id(ended), _employeeUser.Id), HttpStatusCode.Conflict, ErrorCodes.LicenceExpired);

        var lastDay = await LicenceAsync(software, expiresOn: Today());
        await JsonAsync(_tech, await AssignUserAsync(Id(lastDay), _employeeUser.Id));

        var device = await LicenceAsync(software, PerDevice);
        await AssertProblemAsync(await AssignUserAsync(Id(device), _employeeUser.Id), HttpStatusCode.Conflict, ErrorCodes.LicenceKindMismatch);

        var open = await LicenceAsync(software);
        await TestData.SaveAsync(factory, db => db.Users.Single(u => u.Id == _employeeUser.Id).Deactivate(DateTimeOffset.UtcNow));
        await AssertProblemAsync(await AssignUserAsync(Id(open), _employeeUser.Id), HttpStatusCode.Conflict, ErrorCodes.LicenceTargetNotAssignable);
        var seats = await JsonAsync(_tech, await _tech.GetAsync($"{LicencesPath}/{Id(lastDay)}/assignments"));
        Assert.False(seats.GetProperty("items")[0].GetProperty("targetAvailable").GetBoolean());

        foreach (var body in new object[] { new { }, new { userId = _techUser.Id, assetId = Guid.NewGuid() } })
        {
            using var invalid = await _tech.PostAsync($"{LicencesPath}/{Id(open)}/assignments", body);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        }
    }

    [Fact]
    public async Task A_device_seat_shows_on_the_asset()
    {
        var location = await TestData.LocationAsync(factory, "Depo");
        var assetId = await TestData.LegacyAssetAsync(factory, "LIC-0001", location.Id);
        var licence = await LicenceAsync(Id(await SoftwareAsync()), PerDevice);
        await JsonAsync(_tech, await _tech.PostAsync($"{LicencesPath}/{Id(licence)}/assignments", new { assetId }));

        var rows = await JsonAsync(_tech, await _tech.GetAsync("/api/v1/assets/LIC-0001/licences"));
        Assert.Equal("Office", rows[0].GetProperty("softwareName").GetString());
    }

    [Fact]
    public async Task List_filters_by_text_state_and_kind()
    {
        var software = Id(await SoftwareAsync("Çizim", "Autodesk"));
        await LicenceAsync(software, name: "Dolu", quantity: 1, expiresOn: Today().AddDays(LicenceLimits.ExpiryNoticeDays));
        await LicenceAsync(software, name: "Bitmiş", expiresOn: Today().AddDays(-1));
        var device = await LicenceAsync(software, PerDevice, name: "Cihaz", expiresOn: Today().AddDays(LicenceLimits.ExpiryNoticeDays + 1));
        var full = (await JsonAsync(_tech, await _tech.GetAsync($"{LicencesPath}?q=dolu"))).GetProperty("items")[0];
        await JsonAsync(_tech, await AssignUserAsync(Id(full), _employeeUser.Id));

        async Task<List<string?>> NamesAsync(string query) =>
            (await JsonAsync(_tech, await _tech.GetAsync($"{LicencesPath}?{query}"))).GetProperty("items").EnumerateArray()
                .Select(i => i.GetProperty("name").GetString()).ToList();

        Assert.Equal(["Bitmiş", "Cihaz", "Dolu"], await NamesAsync("q=çizim"));
        Assert.Empty(await NamesAsync("q=%25"));
        Assert.Equal(["Dolu"], await NamesAsync("state=full"));
        Assert.Equal(["Dolu"], await NamesAsync("state=expiring"));
        Assert.Equal(["Bitmiş"], await NamesAsync("state=expired"));
        Assert.Equal([Id(device)], (await JsonAsync(_tech, await _tech.GetAsync($"{LicencesPath}?kind=per_device"))).GetProperty("items").EnumerateArray().Select(Id).ToList());
    }

    [Fact]
    public async Task Only_an_admin_sets_and_shows_the_key_and_every_showing_is_audited()
    {
        var licence = await LicenceAsync(Id(await SoftwareAsync()));
        var keyUrl = $"{LicencesPath}/{Id(licence)}/key";
        using (var none = await _admin.PostAsync($"{keyUrl}/reveal"))
        {
            Assert.Equal(HttpStatusCode.NotFound, none.StatusCode);
        }

        using (var denied = await _tech.PutAsync(keyUrl, new { version = Version(licence), key = SecretKey }))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        using (var set = await _admin.PutAsync(keyUrl, new { version = Version(licence), key = SecretKey }))
        {
            Assert.Equal(HttpStatusCode.OK, set.StatusCode);
            Assert.DoesNotContain(SecretKey, await set.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        }

        using (var denied = await _tech.PostAsync($"{keyUrl}/reveal"))
        {
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        }

        using (var shown = await _admin.PostAsync($"{keyUrl}/reveal"))
        {
            Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
            Assert.True(shown.Headers.CacheControl?.NoStore);
            Assert.Equal(SecretKey, (await _admin.ReadJsonAsync<JsonElement>(shown)).GetProperty("key").GetString());
        }

        var detail = await JsonAsync(_tech, await _tech.GetAsync($"{LicencesPath}/{Id(licence)}"));
        Assert.True(detail.GetProperty("hasKey").GetBoolean());
        await using (var db = factory.Database.CreateDbContext())
        {
            Assert.DoesNotContain(SecretKey, (await db.Licences.SingleAsync(Ct)).ProtectedKey!, StringComparison.Ordinal);
            var events = await db.AuditEvents.Where(e => e.EntityType == AuditEntityTypes.Licence).ToListAsync(Ct);
            Assert.Single(events, e => e.Action == AuditActions.LicenceKeyShown);
            Assert.DoesNotContain(events, e => $"{e.OldValues}{e.NewValues}".Contains(SecretKey, StringComparison.Ordinal));
        }

        await TestData.SaveAsync(factory, db => db.Licences.Single().SetProtectedKey("broken", DateTimeOffset.UtcNow));
        await AssertProblemAsync(await _admin.PostAsync($"{keyUrl}/reveal"), HttpStatusCode.Conflict, ErrorCodes.LicenceKeyUnreadable);
        detail = await JsonAsync(_tech, await _tech.GetAsync($"{LicencesPath}/{Id(licence)}"));
        var cleared = await JsonAsync(_admin, await _admin.PutAsync(keyUrl, new { version = Version(detail), key = " " }));
        Assert.False(cleared.GetProperty("hasKey").GetBoolean());
    }

    [Fact]
    public async Task Employees_get_not_found_and_see_only_the_names_of_their_own_software()
    {
        var licence = await LicenceAsync(Id(await SoftwareAsync()));
        await SoftwareAsync("Gizli", "Başka");
        await JsonAsync(_tech, await AssignUserAsync(Id(licence), _employeeUser.Id));
        var id = Id(licence);
        var calls = new[]
        {
            await _employee.GetAsync(SoftwarePath),
            await _employee.PostAsync(SoftwarePath, new { name = "X", vendor = "Y" }),
            await _employee.PutAsync($"{SoftwarePath}/{id}", new { version = 1, name = "X", vendor = "Y", isActive = true }),
            await _employee.GetAsync(LicencesPath),
            await _employee.PostAsync(LicencesPath, new { softwareId = id, name = "X", kind = PerUser, quantity = 1, ownerTeamId = _teamId }),
            await _employee.GetAsync($"{LicencesPath}/{id}"),
            await _employee.PutAsync($"{LicencesPath}/{id}", new { version = 1, name = "X", quantity = 1, ownerTeamId = _teamId }),
            await _employee.GetAsync($"{LicencesPath}/{id}/assignments"),
            await _employee.PostAsync($"{LicencesPath}/{id}/assignments", new { userId = _employeeUser.Id }),
            await _employee.DeleteAsync($"{LicencesPath}/{id}/assignments/{id}"),
            await _employee.PutAsync($"{LicencesPath}/{id}/key", new { version = 1, key = "x" }),
            await _employee.PostAsync($"{LicencesPath}/{id}/key/reveal"),
            await _employee.GetAsync("/api/v1/assets/ANY/licences"),
        };
        foreach (var call in calls)
        {
            using (call)
            {
                Assert.Equal(HttpStatusCode.NotFound, call.StatusCode);
            }
        }

        var mine = await JsonAsync(_employee, await _employee.GetAsync(MySoftwarePath));
        Assert.Equal("""[{"name":"Office","vendor":"Microsoft"}]""", mine.GetRawText());
        Assert.Equal(0, (await JsonAsync(_tech, await _tech.GetAsync(MySoftwarePath))).GetArrayLength());
    }

    [Fact]
    public async Task The_owner_team_is_told_once_per_end_date()
    {
        var software = Id(await SoftwareAsync());
        var due = await LicenceAsync(software, expiresOn: Today().AddDays(LicenceLimits.ExpiryNoticeDays));
        await LicenceAsync(software, expiresOn: Today().AddDays(LicenceLimits.ExpiryNoticeDays + 1));
        await LicenceAsync(software, expiresOn: Today().AddDays(-1));
        await LicenceAsync(software);

        await NotifyAsync();
        await NotifyAsync();
        Assert.Equal(1, await ExpiryEventsAsync());

        var current = await JsonAsync(_tech, await _tech.GetAsync($"{LicencesPath}/{Id(due)}"));
        await JsonAsync(_tech, await UpdateAsync(current, 1, Today().AddDays(10)));
        await NotifyAsync();
        Assert.Equal(2, await ExpiryEventsAsync());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(50, TimeSpan.FromMinutes(5), Ct);
        }

        await using var db = factory.Database.CreateDbContext();
        var mails = await db.EmailDeliveries.Where(d => d.TemplateType == NotificationEventTypes.LicenceExpiring).ToListAsync(Ct);
        Assert.Equal(2, mails.Count);
        Assert.All(mails, m => Assert.Contains($"/licences/{Id(due)}", m.Body, StringComparison.Ordinal));
        Assert.All(mails, m => Assert.DoesNotContain("{{", m.Body, StringComparison.Ordinal));
    }

    private async Task NotifyAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<NotifyExpiringLicencesHandler>().HandleAsync(Ct);
    }

    private async Task<int> ExpiryEventsAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.OutboxEvents.CountAsync(e => e.EventType == NotificationEventTypes.LicenceExpiring, Ct);
    }
}
