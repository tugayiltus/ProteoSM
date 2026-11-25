using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Domain.Audit;
using Proteone.Domain.Settings;
using Proteone.Domain.Users;

namespace Proteone.Api.IntegrationTests.Settings;

/// <summary>Company-wide settings and announcements (ADR-0042).</summary>
[Collection(ApiCollection.Name)]
public sealed class SettingsAndAnnouncementTests(ApiFactory factory) : IAsyncLifetime
{
    private const string SettingsPath = "/api/v1/admin/system-settings";
    private const string AdminPath = "/api/v1/admin/announcements";
    private const string UserPath = "/api/v1/announcements";

    private ApiClient _admin = null!;

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        (_admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "yonetici@example.com");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<JsonElement> JsonAsync(ApiClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static object Draft(string title, int startHours, int? endHours = null, uint version = 0) => new
    {
        title,
        body = "Bakım",
        level = "warning",
        startsAt = DateTimeOffset.UtcNow.AddHours(startHours),
        endsAt = endHours is { } hours ? DateTimeOffset.UtcNow.AddHours(hours) : (DateTimeOffset?)null,
        version,
    };

    [Fact]
    public async Task An_admin_changes_the_settings_once_per_version_and_it_is_audited()
    {
        var before = await JsonAsync(_admin, SettingsPath);
        var version = before.GetProperty("version").GetUInt32();
        Assert.Equal(SystemSettings.DefaultLicenceNoticeDays, before.GetProperty("licenceNoticeDays").GetInt32());

        using var saved = await _admin.PutAsync(SettingsPath, new { licenceNoticeDays = 45, warrantyWindowDays = 120, version });
        using var stale = await _admin.PutAsync(SettingsPath, new { licenceNoticeDays = 10, warrantyWindowDays = 10, version });

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.Conflict), (saved.StatusCode, stale.StatusCode));
        var after = await JsonAsync(_admin, SettingsPath);
        Assert.Equal((45, 120), (after.GetProperty("licenceNoticeDays").GetInt32(), after.GetProperty("warrantyWindowDays").GetInt32()));
        Assert.Equal((await _admin.ReadJsonAsync<JsonElement>(saved)).GetProperty("version").GetUInt32(), after.GetProperty("version").GetUInt32());
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.AuditEvents.CountAsync(e => e.Action == AuditActions.SystemSettingsUpdated, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(SystemSettings.MaxDays + 1)]
    public async Task Days_out_of_range_are_refused(int days)
    {
        var version = (await JsonAsync(_admin, SettingsPath)).GetProperty("version").GetUInt32();
        using var response = await _admin.PutAsync(SettingsPath, new { licenceNoticeDays = days, warrantyWindowDays = 90, version });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Everyone_sees_only_current_announcements_and_an_end_takes_one_down()
    {
        var (employee, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Employee, "calisan@example.com");
        foreach (var draft in new[] { Draft("Şimdi", -1), Draft("Sonra", 1), Draft("Eski", -3, -2) })
        {
            using var created = await _admin.PostAsync(AdminPath, draft);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        var shown = Assert.Single((await JsonAsync(employee, UserPath)).EnumerateArray());
        Assert.Equal("Şimdi", shown.GetProperty("title").GetString());
        var item = (await JsonAsync(_admin, AdminPath)).EnumerateArray().Single(x => x.GetProperty("title").GetString() == "Şimdi");
        using var ended = await _admin.PutAsync($"{AdminPath}/{shown.GetProperty("id").GetGuid()}", Draft("Şimdi", -3, -1, item.GetProperty("version").GetUInt32()));
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        Assert.Empty((await JsonAsync(employee, UserPath)).EnumerateArray());
    }

    [Fact]
    public async Task An_end_before_the_start_is_refused()
    {
        using var response = await _admin.PostAsync(AdminPath, Draft("Ters", 2, 1));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Only_admins_manage_them()
    {
        var (staff, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        using var settings = await staff.GetAsync(SettingsPath);
        using var list = await staff.GetAsync(AdminPath);
        using var create = await staff.PostAsync(AdminPath, Draft("Yetkisiz", -1));
        Assert.All([settings, list, create], r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }
}
