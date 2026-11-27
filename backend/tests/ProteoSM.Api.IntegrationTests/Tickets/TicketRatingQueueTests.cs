using System.Net;
using System.Text.Json;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Api.IntegrationTests.Tickets;

/// <summary>The to_rate queue lists the actor's own completed, unrated tickets inside the rating window.</summary>
public sealed class TicketRatingQueueTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const string EmployeeEmail = "ayse@example.com";
    private const string QueuePath = "/api/v1/tickets?queue=to_rate";
    private static readonly DateTimeOffset Start = new(2027, 6, 1, 9, 0, 0, TimeSpan.Zero);

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        var now = factory.Clock.GetUtcNow();
        factory.Clock.SetUtcNow(now < Start ? Start : now + (RatingLimits.Window * 2));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<List<string>> QueueNumbersAsync(ApiClient client)
    {
        var page = await TicketWorld.ReadAsync(client, QueuePath);
        return [.. page.GetProperty("items").EnumerateArray().Select(t => t.GetProperty("number").GetString()!)];
    }

    [Fact]
    public async Task To_rate_lists_only_own_rateable_unrated_tickets_in_window()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var resolved = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee, "Çözüldü"));
        var rated = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee, "Puanlandı"));
        using var rate = await TicketRatingSteps.RateAsync(world.Employee, rated, 5);
        Assert.Equal(HttpStatusCode.OK, rate.StatusCode);
        var toCancel = await TicketWorld.CreateIncidentAsync(world.Employee, "İptal");
        using var cancel = await world.Desk.PostAsync(TicketWorld.PathOf(toCancel, "cancel"), new { version = TicketWorld.VersionOf(toCancel), reason = "Mükerrer" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.OtherEmployee, "Başkasının"));

        var numbers = await QueueNumbersAsync(world.Employee);

        Assert.Equal([TicketWorld.NumberOf(resolved)], numbers);
        Assert.Empty(await QueueNumbersAsync(world.Desk));

        factory.Clock.Advance(RatingLimits.Window);
        // Sessions end after a day, so the employee signs in again.
        var later = ApiClient.Create(factory);
        await later.LoginAsync(EmployeeEmail);
        Assert.Empty(await QueueNumbersAsync(later));
    }
}
