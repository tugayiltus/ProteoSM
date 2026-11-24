using System.Text.Json;
using Proteone.Api.IntegrationTests.Infrastructure;

namespace Proteone.Api.IntegrationTests.Tickets;

/// <summary>Istanbul enters the new year three hours before UTC; the number follows the company's calendar.</summary>
public sealed class TicketNumberYearTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task An_incident_after_local_midnight_belongs_to_the_new_year()
    {
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 12, 31, 22, 0, 0, TimeSpan.Zero));
        var world = await TicketWorld.CreateAsync(factory);

        var incident = await TicketWorld.CreateIncidentAsync(world.Employee);

        Assert.StartsWith("INC-2027-", TicketWorld.NumberOf(incident), StringComparison.Ordinal);
    }
}
