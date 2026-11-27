using System.Net;
using System.Text.Json;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Tickets;

namespace ProteoSM.Api.IntegrationTests.Tickets;

/// <summary>The rating window ends exactly <see cref="RatingLimits.Window"/> after completion (exclusive).</summary>
public sealed class TicketRatingWindowTests(FakeClockApiFactory factory) : IClassFixture<FakeClockApiFactory>, IAsyncLifetime
{
    private const string EmployeeEmail = "ayse@example.com";
    private static readonly DateTimeOffset Start = new(2027, 3, 1, 9, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    public async ValueTask InitializeAsync()
    {
        await factory.ResetAsync();
        // The fixture's clock is shared by the tests of this class and cannot go back, so each test starts later.
        var now = factory.Clock.GetUtcNow();
        factory.Clock.SetUtcNow(now < Start ? Start : now + (RatingLimits.Window * 2));
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Sessions end after a day, so a client that waits for days signs in again.</summary>
    private async Task<ApiClient> EmployeeAsync()
    {
        var client = ApiClient.Create(factory);
        await client.LoginAsync(EmployeeEmail);
        return client;
    }

    private async Task<(JsonElement Completed, DateTimeOffset CompletedAt)> CompletedTicketAsync()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee));
        return (completed, completed.GetProperty("completedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Rating_one_second_before_window_end_is_accepted()
    {
        var (completed, completedAt) = await CompletedTicketAsync();
        factory.Clock.SetUtcNow(completedAt + RatingLimits.Window - OneSecond);

        using var rate = await TicketRatingSteps.RateAsync(await EmployeeAsync(), completed, 4);

        Assert.Equal(HttpStatusCode.OK, rate.StatusCode);
    }

    [Fact]
    public async Task Rating_at_window_end_is_409_window_closed()
    {
        var (completed, completedAt) = await CompletedTicketAsync();
        factory.Clock.SetUtcNow(completedAt + RatingLimits.Window);
        var employee = await EmployeeAsync();

        using var rate = await TicketRatingSteps.RateAsync(employee, completed, 4);
        var detail = await TicketWorld.ReadAsync(employee, TicketWorld.PathOf(completed));

        Assert.Equal(HttpStatusCode.Conflict, rate.StatusCode);
        Assert.Equal(ErrorCodes.RatingWindowClosed, await ApiClient.ReadCodeAsync(rate));
        Assert.False(detail.GetProperty("canRate").GetBoolean());
    }

    [Fact]
    public async Task Update_after_window_end_is_409_window_closed()
    {
        var (completed, completedAt) = await CompletedTicketAsync();
        factory.Clock.Advance(TimeSpan.FromHours(1));
        using var first = await TicketRatingSteps.RateAsync(await EmployeeAsync(), completed, 2);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        factory.Clock.SetUtcNow(completedAt + RatingLimits.Window);

        using var update = await TicketRatingSteps.RateAsync(await EmployeeAsync(), completed, 5);

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
        Assert.Equal(ErrorCodes.RatingWindowClosed, await ApiClient.ReadCodeAsync(update));
    }
}
