using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Domain.Assets;
using Tgysm.Domain.Users;

namespace Tgysm.Api.IntegrationTests.Services;

/// <summary>Shared setup of the service API tests: an admin, an owner team and helpers for services and assets.</summary>
internal sealed class ServiceWorld
{
    public const string AdminServicesPath = "/api/v1/admin/services";
    public const string ServicesPath = "/api/v1/services";
    public const string AssetsPath = "/api/v1/assets";
    public const string RetireReason = "Hurdaya ayrıldı";

    private ServiceWorld(ApiFactory factory, ApiClient admin, User adminUser, Guid teamId)
    {
        Factory = factory;
        Admin = admin;
        AdminUser = adminUser;
        TeamId = teamId;
    }

    public ApiFactory Factory { get; }
    public ApiClient Admin { get; }
    public User AdminUser { get; }
    public Guid TeamId { get; }

    public static async Task<ServiceWorld> CreateAsync(ApiFactory factory)
    {
        var (admin, adminUser) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        var team = await TestData.TeamAsync(factory, "Altyapı");
        return new ServiceWorld(factory, admin, adminUser, team.Id);
    }

    public async Task<ApiClient> LoginAsync(UserRole role, string email) =>
        (await ApiClient.LoginAsNewUserAsync(Factory, role, email)).Client;

    public async Task<JsonElement> CreateServiceAsync(string name, Guid? ownerTeamId = null, string? description = null)
    {
        using var response = await Admin.PostAsync(AdminServicesPath, new { name, description, ownerTeamId = ownerTeamId ?? TeamId });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await Admin.ReadJsonAsync<JsonElement>(response);
    }

    public async Task<(Guid Id, string Tag)> AssetAsync(string tag)
    {
        var location = await TestData.LocationAsync(Factory, "Depo " + tag);
        return (await TestData.LegacyAssetAsync(Factory, tag, location.Id), tag);
    }

    /// <summary>Retires the asset straight in the database (no lifecycle race involved).</summary>
    public Task RetireInDatabaseAsync(Guid assetId) =>
        TestData.SaveAsync(Factory, db =>
        {
            var asset = db.Assets.Single(a => a.Id == assetId);
            var change = asset.Retire(null, AdminUser.Id, RetireReason, DateTimeOffset.UtcNow);
            db.AssetMovements.Add(change.Movement);
        });

    public async Task<uint> AssetVersionAsync(Guid assetId)
    {
        using var response = await Admin.GetAsync($"{AssetsPath}/{assetId}");
        return (await Admin.ReadJsonAsync<JsonElement>(response)).GetProperty("version").GetUInt32();
    }

    public static Guid Id(JsonElement item) => item.GetProperty("id").GetGuid();

    public static uint Version(JsonElement item) => item.GetProperty("version").GetUInt32();
}
