using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Application.Common;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Services;
using Proteone.Domain.Users;
using static Proteone.Api.IntegrationTests.Services.ServiceWorld;

namespace Proteone.Api.IntegrationTests.Services;

[Collection(ApiCollection.Name)]
public sealed class ServiceAdminEndpointTests(ApiFactory factory) : IAsyncLifetime
{

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Admin_creates_updates_and_audits_a_service()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var created = await world.CreateServiceAsync("  E-posta  ", description: "Kurumsal posta");
        Assert.Equal("E-posta", created.GetProperty("name").GetString());
        Assert.Equal("Kurumsal posta", created.GetProperty("description").GetString());
        Assert.Equal(world.TeamId, created.GetProperty("ownerTeamId").GetGuid());
        Assert.Equal("Altyapı", created.GetProperty("ownerTeamName").GetString());
        Assert.True(created.GetProperty("isActive").GetBoolean());

        using var update = await world.Admin.PutAsync($"{AdminServicesPath}/{Id(created)}",
            new { name = "E-posta ve takvim", description = (string?)null, ownerTeamId = world.TeamId, version = Version(created) });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await world.Admin.ReadJsonAsync<JsonElement>(update);
        Assert.Equal("E-posta ve takvim", updated.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, updated.GetProperty("description").ValueKind);

        using var stale = await world.Admin.PutAsync($"{AdminServicesPath}/{Id(created)}",
            new { name = "Eski", description = (string?)null, ownerTeamId = world.TeamId, version = Version(created) });
        Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(stale));

        await using var db = factory.Database.CreateDbContext();
        var actions = await db.AuditEvents.Where(e => e.EntityId == Id(created)).Select(e => e.Action).ToListAsync(Ct);
        Assert.Contains(AuditActions.ServiceCreated, actions);
        Assert.Contains(AuditActions.ServiceUpdated, actions);
    }

    [Fact]
    public async Task Names_are_unique_ignoring_case_and_turkish_dotted_i()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        await world.CreateServiceAsync("İnternet");
        var other = await world.CreateServiceAsync("VPN");

        using var sameInOtherCase = await world.Admin.PostAsync(AdminServicesPath, new { name = "internet", ownerTeamId = world.TeamId });
        using var renameOnto = await world.Admin.PutAsync($"{AdminServicesPath}/{Id(other)}",
            new { name = "INTERNET", description = (string?)null, ownerTeamId = world.TeamId, version = Version(other) });

        Assert.Equal(HttpStatusCode.Conflict, sameInOtherCase.StatusCode);
        Assert.Equal(ErrorCodes.ServiceNameTaken, await ApiClient.ReadCodeAsync(sameInOtherCase));
        Assert.Equal(ErrorCodes.ServiceNameTaken, await ApiClient.ReadCodeAsync(renameOnto));
    }

    [Fact]
    public async Task Invalid_input_is_a_validation_error_not_a_server_error()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        using var blank = await world.Admin.PostAsync(AdminServicesPath, new { name = "   ", ownerTeamId = world.TeamId });
        using var tooLong = await world.Admin.PostAsync(AdminServicesPath,
            new { name = new string('a', ServiceLimits.NameMaxLength + 1), ownerTeamId = world.TeamId });
        using var longDescription = await world.Admin.PostAsync(AdminServicesPath,
            new { name = "Yazıcı", description = new string('a', ServiceLimits.DescriptionMaxLength + 1), ownerTeamId = world.TeamId });
        using var noTeam = await world.Admin.PostAsync(AdminServicesPath, new { name = "Yazıcı", ownerTeamId = Guid.Empty });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longDescription.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noTeam.StatusCode);
    }

    [Fact]
    public async Task Owner_team_must_be_active()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var inactive = await TestData.TeamAsync(factory, "Eski ekip");
        await TestData.SaveAsync(factory, db => db.Teams.Single(t => t.Id == inactive.Id).Deactivate());
        var service = await world.CreateServiceAsync("ERP");

        using var create = await world.Admin.PostAsync(AdminServicesPath, new { name = "CRM", ownerTeamId = inactive.Id });
        using var unknown = await world.Admin.PostAsync(AdminServicesPath, new { name = "CRM", ownerTeamId = Guid.CreateVersion7() });
        using var move = await world.Admin.PutAsync($"{AdminServicesPath}/{Id(service)}",
            new { name = "ERP", description = (string?)null, ownerTeamId = inactive.Id, version = Version(service) });

        Assert.Equal(HttpStatusCode.Conflict, create.StatusCode);
        Assert.Equal(ErrorCodes.ServiceOwnerTeamInactive, await ApiClient.ReadCodeAsync(create));
        Assert.Equal(ErrorCodes.ServiceOwnerTeamInactive, await ApiClient.ReadCodeAsync(unknown));
        Assert.Equal(ErrorCodes.ServiceOwnerTeamInactive, await ApiClient.ReadCodeAsync(move));
    }

    [Fact]
    public async Task Only_admins_reach_the_admin_routes()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var service = await world.CreateServiceAsync("ERP");
        var tech = await world.LoginAsync(UserRole.ItStaff, "tech@example.com");
        var employee = await world.LoginAsync(UserRole.Employee, "employee@example.com");

        using var techCreate = await tech.PostAsync(AdminServicesPath, new { name = "CRM", ownerTeamId = world.TeamId });
        using var techDeactivate = await tech.PostAsync($"{AdminServicesPath}/{Id(service)}/deactivate", new { version = Version(service) });
        using var employeeCreate = await employee.PostAsync(AdminServicesPath, new { name = "CRM", ownerTeamId = world.TeamId });

        Assert.Equal(HttpStatusCode.Forbidden, techCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, techDeactivate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, employeeCreate.StatusCode);
    }

    [Fact]
    public async Task Deactivated_services_leave_the_active_list_and_come_back()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var service = await world.CreateServiceAsync("ERP");
        await world.CreateServiceAsync("CRM");

        using var deactivate = await world.Admin.PostAsync($"{AdminServicesPath}/{Id(service)}/deactivate", new { version = Version(service) });
        var deactivated = await world.Admin.ReadJsonAsync<JsonElement>(deactivate);
        Assert.False(deactivated.GetProperty("isActive").GetBoolean());
        using var activeList = await world.Admin.GetAsync($"{ServicesPath}?active=true");
        var activeNames = (await world.Admin.ReadJsonAsync<JsonElement>(activeList)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("name").GetString()).ToList();
        Assert.Equal(["CRM"], activeNames);

        using var activate = await world.Admin.PostAsync($"{AdminServicesPath}/{Id(service)}/activate", new { version = Version(deactivated) });
        Assert.True((await world.Admin.ReadJsonAsync<JsonElement>(activate)).GetProperty("isActive").GetBoolean());

        await using var db = factory.Database.CreateDbContext();
        var actions = await db.AuditEvents.Where(e => e.EntityId == Id(service)).Select(e => e.Action).ToListAsync(Ct);
        Assert.Contains(AuditActions.ServiceDeactivated, actions);
        Assert.Contains(AuditActions.ServiceActivated, actions);
    }

    [Fact]
    public async Task Links_and_unlinks_assets_with_the_link_rules()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var service = await world.CreateServiceAsync("ERP");
        var (assetId, _) = await world.AssetAsync("SRV-0001");
        var (retiredId, _) = await world.AssetAsync("SRV-0002");
        await world.RetireInDatabaseAsync(retiredId);
        var assetsPath = $"{AdminServicesPath}/{Id(service)}/assets";

        using var link = await world.Admin.PostAsync(assetsPath, new { assetId });
        using var twice = await world.Admin.PostAsync(assetsPath, new { assetId });
        using var retired = await world.Admin.PostAsync(assetsPath, new { assetId = retiredId });
        using var unknownAsset = await world.Admin.PostAsync(assetsPath, new { assetId = Guid.CreateVersion7() });
        using var unknownService = await world.Admin.PostAsync($"{AdminServicesPath}/{Guid.CreateVersion7()}/assets", new { assetId });

        Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        Assert.Equal(assetId, (await world.Admin.ReadJsonAsync<JsonElement>(link)).GetProperty("assetId").GetGuid());
        Assert.Equal(HttpStatusCode.Conflict, twice.StatusCode);
        Assert.Equal(ErrorCodes.ServiceAssetLinkExists, await ApiClient.ReadCodeAsync(twice));
        Assert.Equal(HttpStatusCode.Conflict, retired.StatusCode);
        Assert.Equal(ErrorCodes.ServiceAssetNotLinkable, await ApiClient.ReadCodeAsync(retired));
        Assert.Equal(HttpStatusCode.NotFound, unknownAsset.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownService.StatusCode);

        using var unlink = await world.Admin.DeleteAsync($"{assetsPath}/{assetId}");
        using var unlinkAgain = await world.Admin.DeleteAsync($"{assetsPath}/{assetId}");
        Assert.Equal(HttpStatusCode.NoContent, unlink.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unlinkAgain.StatusCode);

        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.ServiceAssets.AnyAsync(Ct));
        var actions = await db.AuditEvents.Where(e => e.EntityId == Id(service)).Select(e => e.Action).ToListAsync(Ct);
        Assert.Contains(AuditActions.ServiceAssetLinked, actions);
        Assert.Contains(AuditActions.ServiceAssetUnlinked, actions);
    }

    /// <summary>
    /// Deterministic race: a retire holds the asset lifecycle lock and is not yet committed while the link call arrives.
    /// The link must wait for the lock, then see the retired asset. Without the lock it reads the old status and links.
    /// </summary>
    [Fact]
    public async Task A_link_waiting_on_an_uncommitted_retire_sees_the_retired_asset()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var service = await world.CreateServiceAsync("ERP");
        var (assetId, _) = await world.AssetAsync("RACE-0001");

        await using var retireDb = factory.Database.CreateDbContext();
        await using var retire = await retireDb.Database.BeginTransactionAsync(Ct);
        await retireDb.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SerializationLocks.AssetLifecycle})", Ct);
        var asset = await retireDb.Assets.SingleAsync(a => a.Id == assetId, Ct);
        retireDb.AssetMovements.Add(asset.Retire(null, world.AdminUser.Id, RetireReason, DateTimeOffset.UtcNow).Movement);
        await retireDb.SaveChangesAsync(Ct);

        var linkCall = world.Admin.PostAsync($"{AdminServicesPath}/{Id(service)}/assets", new { assetId });
        await AdvisoryLocks.WaitUntilBlockedOrDoneAsync(factory, linkCall, Ct);
        await retire.CommitAsync(Ct);

        using var link = await linkCall;
        Assert.Equal(HttpStatusCode.Conflict, link.StatusCode);
        Assert.Equal(ErrorCodes.ServiceAssetNotLinkable, await ApiClient.ReadCodeAsync(link));
        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.ServiceAssets.AnyAsync(l => l.AssetId == assetId, Ct));
    }
}
