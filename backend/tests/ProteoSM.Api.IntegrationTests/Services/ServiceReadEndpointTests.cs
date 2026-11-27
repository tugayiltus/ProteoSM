using System.Net;
using System.Text.Json;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Domain.Users;
using static ProteoSM.Api.IntegrationTests.Services.ServiceWorld;

namespace ProteoSM.Api.IntegrationTests.Services;

[Collection(ApiCollection.Name)]
public sealed class ServiceReadEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<List<string?>> NamesAsync(ApiClient client, string url)
    {
        using var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await client.ReadJsonAsync<JsonElement>(response)).GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("name").GetString()).ToList();
    }

    [Fact]
    public async Task It_staff_list_and_filter_services()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var otherTeam = await TestData.TeamAsync(factory, "Uygulama");
        var erp = await world.CreateServiceAsync("ERP", description: "Muhasebe ve stok");
        await world.CreateServiceAsync("İnternet");
        await world.CreateServiceAsync("CRM", otherTeam.Id);
        var (assetId, _) = await world.AssetAsync("SRV-0100");
        using (var link = await world.Admin.PostAsync($"{AdminServicesPath}/{Id(erp)}/assets", new { assetId }))
        {
            Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        }

        var tech = await world.LoginAsync(UserRole.ItStaff, "tech@example.com");

        Assert.Equal(["CRM", "ERP", "İnternet"], await NamesAsync(tech, ServicesPath));
        Assert.Equal(["CRM"], await NamesAsync(tech, $"{ServicesPath}?teamId={otherTeam.Id}"));
        Assert.Equal(["ERP"], await NamesAsync(tech, $"{ServicesPath}?q=MUHASEBE"));
        Assert.Equal(["İnternet"], await NamesAsync(tech, $"{ServicesPath}?q=inter"));
        Assert.Empty(await NamesAsync(tech, $"{ServicesPath}?q=%25"));
        Assert.Equal(["ERP"], await NamesAsync(tech, $"{ServicesPath}?active=true&q=erp"));

        using var list = await tech.GetAsync($"{ServicesPath}?teamId={world.TeamId}&q=erp");
        var item = (await tech.ReadJsonAsync<JsonElement>(list)).GetProperty("items")[0];
        Assert.Equal("Altyapı", item.GetProperty("ownerTeamName").GetString());
        Assert.Equal(1, item.GetProperty("assetCount").GetInt32());
        Assert.Equal(0, item.GetProperty("openTicketCount").GetInt32());
        Assert.True(item.GetProperty("isActive").GetBoolean());
    }

    [Fact]
    public async Task It_staff_read_a_service_and_the_services_of_an_asset()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var erp = await world.CreateServiceAsync("ERP", description: "Muhasebe");
        var crm = await world.CreateServiceAsync("CRM");
        var (assetId, tag) = await world.AssetAsync("SRV-0200");
        foreach (var service in new[] { erp, crm })
        {
            using var link = await world.Admin.PostAsync($"{AdminServicesPath}/{Id(service)}/assets", new { assetId });
            Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        }

        using (var deactivate = await world.Admin.PostAsync($"{AdminServicesPath}/{Id(crm)}/deactivate", new { version = Version(crm) }))
        {
            Assert.Equal(HttpStatusCode.OK, deactivate.StatusCode);
        }

        var tech = await world.LoginAsync(UserRole.ItStaff, "tech@example.com");

        using var detail = await tech.GetAsync($"{ServicesPath}/{Id(erp)}");
        var body = await tech.ReadJsonAsync<JsonElement>(detail);
        Assert.Equal("ERP", body.GetProperty("name").GetString());
        Assert.Equal("Muhasebe", body.GetProperty("description").GetString());
        Assert.Equal("Altyapı", body.GetProperty("ownerTeamName").GetString());
        Assert.True(body.TryGetProperty("version", out _));

        using var byAsset = await tech.GetAsync($"{AssetsPath}/{tag.ToLowerInvariant()}/services");
        Assert.Equal(HttpStatusCode.OK, byAsset.StatusCode);
        var refs = (await tech.ReadJsonAsync<JsonElement>(byAsset)).EnumerateArray()
            .Select(r => (r.GetProperty("name").GetString(), r.GetProperty("isActive").GetBoolean())).ToList();
        Assert.Equal([("CRM", false), ("ERP", true)], refs);

        using var unknownService = await tech.GetAsync($"{ServicesPath}/{Guid.CreateVersion7()}");
        using var unknownAsset = await tech.GetAsync($"{AssetsPath}/NOPE-0001/services");
        Assert.Equal(HttpStatusCode.NotFound, unknownService.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknownAsset.StatusCode);
    }

    [Fact]
    public async Task Employees_get_not_found_on_every_read_route()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var erp = await world.CreateServiceAsync("ERP");
        var (_, tag) = await world.AssetAsync("SRV-0300");
        var employee = await world.LoginAsync(UserRole.Employee, "employee@example.com");

        using var list = await employee.GetAsync(ServicesPath);
        using var detail = await employee.GetAsync($"{ServicesPath}/{Id(erp)}");
        using var byAsset = await employee.GetAsync($"{AssetsPath}/{tag}/services");

        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byAsset.StatusCode);
    }

    [Fact]
    public async Task Assets_filter_by_service()
    {
        var world = await ServiceWorld.CreateAsync(factory);
        var service = await world.CreateServiceAsync("ERP");
        var (linked, tag) = await world.AssetAsync("SRV-0001");
        await world.AssetAsync("SRV-0002");
        using var link = await world.Admin.PostAsync($"{ServiceWorld.AdminServicesPath}/{ServiceWorld.Id(service)}/assets", new { assetId = linked });

        using var response = await world.Admin.GetAsync($"{ServiceWorld.AssetsPath}?serviceId={ServiceWorld.Id(service)}");
        var page = await world.Admin.ReadJsonAsync<System.Text.Json.JsonElement>(response);

        Assert.Equal(tag, Assert.Single(page.GetProperty("items").EnumerateArray()).GetProperty("assetTag").GetString());
    }
}
