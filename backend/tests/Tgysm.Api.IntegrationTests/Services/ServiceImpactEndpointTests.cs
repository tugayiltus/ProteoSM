using System.Net;
using System.Text.Json;
using Tgysm.Api.IntegrationTests.Changes;
using Tgysm.Api.IntegrationTests.Infrastructure;
using Tgysm.Api.IntegrationTests.Problems;
using Tgysm.Api.IntegrationTests.Tickets;

namespace Tgysm.Api.IntegrationTests.Services;

[Collection(ApiCollection.Name)]
public sealed class ServiceImpactEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string ServiceSegment = "service";
    private const string ItemsProperty = "items";
    private const string TotalProperty = "total";
    private const string CanOpenProperty = "canOpen";
    private const int OverflowTickets = 25;
    private const int SectionCap = 20;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static string ImpactPath(Guid serviceId) => $"{ServiceWorld.ServicesPath}/{serviceId}/impact";

    private static async Task<Guid> ServiceAsync(TicketWorld world, string name)
    {
        using var created = await world.Admin.PostAsync(ServiceWorld.AdminServicesPath, new { name, ownerTeamId = world.ServiceDesk.Id });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return ServiceWorld.Id(await world.Admin.ReadJsonAsync<JsonElement>(created));
    }

    private static async Task<JsonElement> TagAsync(ApiClient client, string recordPath, Guid serviceId, uint version)
    {
        using var response = await client.PutAsync($"{recordPath}/{ServiceSegment}", new { serviceId, version });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static async Task<JsonElement> ImpactAsync(ApiClient client, Guid serviceId)
    {
        using var response = await client.GetAsync(ImpactPath(serviceId));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    private static async Task<JsonElement> TaggedIncidentAsync(TicketWorld world, Guid serviceId, string subject)
    {
        var incident = await TicketWorld.CreateIncidentAsync(world.Employee, subject);
        return await TagAsync(world.Desk, TicketWorld.PathOf(incident), serviceId, TicketWorld.VersionOf(incident));
    }

    private async Task<(Guid Id, string Tag)> AssetAsync(string tag)
    {
        var location = await TestData.LocationAsync(factory, "Depo " + tag);
        return (await TestData.LegacyAssetAsync(factory, tag, location.Id), tag);
    }

    private static List<string> Numbers(JsonElement section) =>
        [.. section.GetProperty(ItemsProperty).EnumerateArray().Select(i => i.GetProperty("number").GetString()!)];

    [Fact]
    public async Task Sections_list_only_open_records_and_every_linked_asset_with_its_status()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var serviceId = await ServiceAsync(world, "E-posta");
        var open = await TaggedIncidentAsync(world, serviceId, "Açık kayıt");
        var done = await TaggedIncidentAsync(world, serviceId, "Biten kayıt");
        using var complete = await world.Desk.PostAsync(
            TicketWorld.PathOf(done, "complete"), new { version = TicketWorld.VersionOf(done), resolution = "Çözüldü", outcome = "resolved" });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);

        var draft = await ChangeWorld.CreateAsync(world.Desk, world);
        await TagAsync(world.Desk, ChangeWorld.PathOf(draft), serviceId, ChangeWorld.VersionOf(draft));
        var pending = await ChangeWorld.CreateAsync(world.Desk, world);
        await ChangeWorld.CreateTaskAsync(world.Desk, pending, "implementation", world.ServiceDesk.Id, world.DeskUser.Id);
        var submitted = await ChangeWorld.ActAsync(world.Desk, await ChangeWorld.DetailAsync(world.Desk, pending), "submit");
        await TagAsync(world.Desk, ChangeWorld.PathOf(submitted), serviceId, ChangeWorld.VersionOf(submitted));

        var openProblem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        await TagAsync(world.Desk, ProblemWorld.PathOf(openProblem), serviceId, ProblemWorld.VersionOf(openProblem));
        var doneProblem = await ProblemWorld.CreateForTeamAsync(world.Desk, world.ServiceDesk.Id);
        var tagged = await TagAsync(world.Desk, ProblemWorld.PathOf(doneProblem), serviceId, ProblemWorld.VersionOf(doneProblem));
        using var resolve = await world.Desk.PostAsync(
            ProblemWorld.PathOf(tagged, "resolve"), new { version = ProblemWorld.VersionOf(tagged), resolution = "Giderildi" });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);

        var (liveId, liveTag) = await AssetAsync("LIVE-1");
        var (retiredId, retiredTag) = await AssetAsync("OLD-1");
        foreach (var assetId in new[] { liveId, retiredId })
        {
            using var link = await world.Admin.PostAsync($"{ServiceWorld.AdminServicesPath}/{serviceId}/assets", new { assetId });
            Assert.Equal(HttpStatusCode.Created, link.StatusCode);
        }

        await TestData.SaveAsync(factory, db =>
        {
            var asset = db.Assets.Single(a => a.Id == retiredId);
            db.AssetMovements.Add(asset.Retire(null, world.AdminUser.Id, ServiceWorld.RetireReason, DateTimeOffset.UtcNow).Movement);
        });

        var impact = await ImpactAsync(world.Desk, serviceId);

        Assert.Equal([TicketWorld.NumberOf(open)], Numbers(impact.GetProperty("tickets")));
        Assert.Equal(1, impact.GetProperty("tickets").GetProperty(TotalProperty).GetInt32());
        Assert.Equal([ChangeWorld.NumberOf(submitted)], Numbers(impact.GetProperty("changes")));
        Assert.Equal([ProblemWorld.NumberOf(openProblem)], Numbers(impact.GetProperty("problems")));
        var assets = impact.GetProperty("assets");
        Assert.Equal(2, assets.GetProperty(TotalProperty).GetInt32());
        var statusByTag = assets.GetProperty(ItemsProperty).EnumerateArray()
            .ToDictionary(a => a.GetProperty("tag").GetString()!, a => a.GetProperty("status").GetString()!);
        Assert.NotEqual("retired", statusByTag[liveTag]);
        Assert.Equal("retired", statusByTag[retiredTag]);
    }

    [Fact]
    public async Task Section_is_capped_with_the_full_total()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var serviceId = await ServiceAsync(world, "Ağ");
        for (var i = 0; i < OverflowTickets; i++)
        {
            await TaggedIncidentAsync(world, serviceId, $"Kayıt {i}");
        }

        var tickets = (await ImpactAsync(world.Desk, serviceId)).GetProperty("tickets");

        Assert.Equal(SectionCap, tickets.GetProperty(ItemsProperty).GetArrayLength());
        Assert.Equal(OverflowTickets, tickets.GetProperty(TotalProperty).GetInt32());
    }

    [Fact]
    public async Task Rows_outside_the_viewer_scope_show_summary_fields_only_and_cannot_be_opened()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var serviceId = await ServiceAsync(world, "ERP");
        var ticket = await TaggedIncidentAsync(world, serviceId, "Gizli konu");
        var change = await ChangeWorld.CreateAsync(world.Desk, world);
        await TagAsync(world.Desk, ChangeWorld.PathOf(change), serviceId, ChangeWorld.VersionOf(change));

        var asDesk = await ImpactAsync(world.Desk, serviceId);
        var asField = await ImpactAsync(world.Field, serviceId);

        Assert.True(asDesk.GetProperty("tickets").GetProperty(ItemsProperty)[0].GetProperty(CanOpenProperty).GetBoolean());
        var row = asField.GetProperty("tickets").GetProperty(ItemsProperty)[0];
        Assert.False(row.GetProperty(CanOpenProperty).GetBoolean());
        Assert.Equal(TicketWorld.NumberOf(ticket), row.GetProperty("number").GetString());
        Assert.Equal("Gizli konu", row.GetProperty("title").GetString());
        Assert.Equal("open", row.GetProperty("status").GetString());
        Assert.Equal(["canOpen", "number", "status", "title"], row.EnumerateObject().Select(p => p.Name).Order().ToList());
    }

    [Fact]
    public async Task Employee_gets_not_found()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var serviceId = await ServiceAsync(world, "VPN");

        using var response = await world.Employee.GetAsync(ImpactPath(serviceId));
        using var unknown = await world.Desk.GetAsync(ImpactPath(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }
}
