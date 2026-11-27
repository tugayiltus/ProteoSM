using System.Net;
using System.Text.Json;
using ProteoSM.Api.IntegrationTests.Assets;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Api.IntegrationTests.Tickets;
using ProteoSM.Application.Features.Catalog;
using ProteoSM.Domain.Users;

namespace ProteoSM.Api.IntegrationTests.Admin;

[Collection(ApiCollection.Name)]
public sealed class DeactivationImpactApiTests(ApiFactory factory) : IAsyncLifetime
{
    private const string AdminPath = "/api/v1/admin";
    private const string Suffix = "/deactivation-impact";

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<Dictionary<string, int>> CountsAsync(ApiClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await client.ReadJsonAsync<JsonElement>(response);
        return body.GetProperty("counts").EnumerateArray().ToDictionary(c => c.GetProperty("key").GetString()!, c => c.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task A_brand_reports_its_models_active_models_and_assets()
    {
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");
        var store = await TestData.LocationAsync(factory, "Depo");
        var modelId = await TestData.ModelAsync(factory, "Lenovo", "T14");
        await TestData.ModelAsync(factory, "Lenovo", "T15", active: false);
        await AssetCatalogueTests.CreateAsync(it, new { assetTag = "LT-1", modelId, locationId = store.Id });
        await AssetCatalogueTests.CreateAsync(it, new { assetTag = "LT-2", modelId, locationId = store.Id });
        await TestData.ModelAsync(factory, "Dell", "X1");
        using var brands = await admin.GetAsync($"{AdminPath}/brands");
        var lenovo = (await admin.ReadJsonAsync<JsonElement>(brands)).EnumerateArray().Single(b => b.GetProperty("name").GetString() == "Lenovo");

        var counts = await CountsAsync(admin, $"{AdminPath}/brands/{lenovo.GetProperty("id").GetGuid()}{Suffix}");

        Assert.Equal(2, counts[DeactivationCounts.Models]);
        Assert.Equal(1, counts[DeactivationCounts.ActiveModels]);
        Assert.Equal(2, counts[DeactivationCounts.Assets]);

        var modelCounts = await CountsAsync(admin, $"{AdminPath}/models/{modelId}{Suffix}");
        Assert.Equal(2, modelCounts[DeactivationCounts.Assets]);
    }

    [Fact]
    public async Task A_category_and_a_request_type_count_only_open_tickets()
    {
        var world = await TicketWorld.CreateAsync(factory);
        await TicketWorld.CreateIncidentAsync(world.Employee, extra: new { subject = "A", description = "B", categoryId = world.Hardware.Id });
        var done = await TicketWorld.CreateIncidentAsync(world.Employee, extra: new { subject = "C", description = "D", categoryId = world.Hardware.Id });
        using var cancel = await world.Desk.PostAsync(TicketWorld.PathOf(done, "cancel"), new { version = TicketWorld.VersionOf(done), reason = "Mükerrer" });
        await world.CreateRequestAsync(world.Employee, approval: false);
        using var child = await world.Admin.PostAsync($"{AdminPath}/categories", new { name = "Yazıcı", parentId = world.Hardware.Id, scope = "both" });
        Assert.Equal(HttpStatusCode.Created, child.StatusCode);

        var category = await CountsAsync(world.Admin, $"{AdminPath}/categories/{world.Hardware.Id}{Suffix}");
        var type = await CountsAsync(world.Admin, $"{AdminPath}/request-types/{world.Software.Id}{Suffix}");
        var unused = await CountsAsync(world.Admin, $"{AdminPath}/request-types/{world.NewLaptop.Id}{Suffix}");

        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        Assert.Equal(1, category[DeactivationCounts.OpenTickets]);
        Assert.Equal(1, category[DeactivationCounts.ActiveChildren]);
        Assert.Equal(1, type[DeactivationCounts.OpenTickets]);
        Assert.Equal(0, unused[DeactivationCounts.OpenTickets]);
    }

    [Fact]
    public async Task An_unknown_entry_is_not_found_and_only_admins_may_ask()
    {
        var (admin, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.Admin, "admin@example.com");
        var (it, _) = await ApiClient.LoginAsNewUserAsync(factory, UserRole.ItStaff, "it@example.com");

        using var missing = await admin.GetAsync($"{AdminPath}/brands/{Guid.NewGuid()}{Suffix}");
        using var forbidden = await it.GetAsync($"{AdminPath}/brands/{Guid.NewGuid()}{Suffix}");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }
}
