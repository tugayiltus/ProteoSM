using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Users;

namespace ProteoSM.Api.IntegrationTests.Sla;

[Collection(ApiCollection.Name)]
public sealed class ProblemTargetsTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Path = "/api/v1/admin/sla/problem-targets";
    private const int ChangedMinutes = 600;
    private const int ChangedPercent = 70;
    private const int ImpactCount = 3;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();

        // Reset does not truncate the seeded rows, so every test starts from the defaults.
        await using var db = factory.Database.CreateDbContext();
        foreach (var target in await db.ProblemSlaTargets.ToListAsync(Ct))
        {
            var (_, minutes, percent) = ProblemSlaTarget.Defaults.Single(d => d.Impact == target.Impact);
            target.Change(minutes, percent);
        }

        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<ApiClient> AdminAsync() =>
        (await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com")).Client;

    private static object Item(string impact, int minutes = ChangedMinutes, int percent = ChangedPercent) =>
        new { impact, targetMinutes = minutes, warningPercent = percent };

    private static object Body(uint version, params object[] targets) => new { targets, version };

    private static async Task<JsonElement> GetAsync(ApiClient admin)
    {
        using var response = await admin.GetAsync(Path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await admin.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task Admin_reads_default_targets()
    {
        var admin = await AdminAsync();

        var result = await GetAsync(admin);

        var targets = result.GetProperty("targets").EnumerateArray().ToList();
        Assert.Equal(ImpactCount, targets.Count);
        foreach (var (impact, minutes, percent) in ProblemSlaTarget.Defaults)
        {
            var row = targets.Single(t => t.GetProperty("impact").GetString() == impact.ToString().ToLowerInvariant());
            Assert.Equal(minutes, row.GetProperty("targetMinutes").GetInt32());
            Assert.Equal(percent, row.GetProperty("warningPercent").GetInt32());
        }
    }

    [Fact]
    public async Task Admin_updates_targets_and_audit_records_old_and_new()
    {
        var admin = await AdminAsync();
        var before = await GetAsync(admin);
        var version = before.GetProperty("version").GetUInt32();

        using var response = await admin.PutAsync(Path, Body(version, Item("high"), Item("medium", 1000, 60), Item("low", 2000, 50)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await admin.ReadJsonAsync<JsonElement>(response);
        Assert.NotEqual(version, after.GetProperty("version").GetUInt32());
        var high = after.GetProperty("targets").EnumerateArray().Single(t => t.GetProperty("impact").GetString() == "high");
        Assert.Equal(ChangedMinutes, high.GetProperty("targetMinutes").GetInt32());

        await using var db = factory.Database.CreateDbContext();
        var audit = await db.AuditEvents.SingleAsync(a => a.Action == AuditActions.ProblemSlaTargetsUpdated, Ct);
        Assert.Contains("960", audit.OldValues);
        Assert.Contains("600", audit.NewValues);
    }

    [Fact]
    public async Task Unchanged_save_keeps_the_version_usable()
    {
        var admin = await AdminAsync();
        var current = await GetAsync(admin);
        var body = Body(
            current.GetProperty("version").GetUInt32(),
            Item("high", 960, 80),
            Item("medium", 2400, 80),
            Item("low", 4800, 80));

        using var first = await admin.PutAsync(Path, body);
        var again = await admin.ReadJsonAsync<JsonElement>(first);
        using var second = await admin.PutAsync(Path, Body(again.GetProperty("version").GetUInt32(), Item("high"), Item("medium"), Item("low")));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task Missing_impact_is_400()
    {
        var admin = await AdminAsync();
        var version = (await GetAsync(admin)).GetProperty("version").GetUInt32();

        using var response = await admin.PutAsync(Path, Body(version, Item("high"), Item("medium")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([ErrorCodes.InvalidValue], (await ApiClient.ReadFieldCodesAsync(response))["targets"]);
    }

    [Fact]
    public async Task Stale_version_is_409()
    {
        var admin = await AdminAsync();
        var version = (await GetAsync(admin)).GetProperty("version").GetUInt32();
        var body = Body(version, Item("high"), Item("medium"), Item("low"));

        using var first = await admin.PutAsync(Path, body);
        using var second = await admin.PutAsync(Path, body);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(second));
    }

    [Fact]
    public async Task Non_admin_is_403()
    {
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var read = await it.GetAsync(Path);
        using var write = await it.PutAsync(Path, Body(1, Item("high"), Item("medium"), Item("low")));

        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }

    [Fact]
    public async Task Null_target_is_400()
    {
        var admin = await AdminAsync();
        var version = (await GetAsync(admin)).GetProperty("version").GetUInt32();

        using var response = await admin.PutAsync(Path, Body(version, Item("high"), Item("medium"), Item("low"), null!));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal([ErrorCodes.InvalidValue], (await ApiClient.ReadFieldCodesAsync(response))["targets"]);
    }

    [Fact]
    public async Task Unchanged_values_write_no_audit_event()
    {
        var admin = await AdminAsync();
        var current = await GetAsync(admin);
        var version = current.GetProperty("version").GetUInt32();

        using var response = await admin.PutAsync(Path, Body(version, Item("high", 960, 80), Item("medium", 2400, 80), Item("low", 4800, 80)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(version, (await admin.ReadJsonAsync<JsonElement>(response)).GetProperty("version").GetUInt32());
        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.ProblemSlaTargetsUpdated, Ct));
    }
}
