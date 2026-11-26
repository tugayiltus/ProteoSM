using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Domain.Audit;
using Proteone.Domain.Common;
using Proteone.Domain.Tickets;

namespace Proteone.Api.IntegrationTests.Tickets;

/// <summary>Steps shared by the rating tests.</summary>
internal static class TicketRatingSteps
{
    public const string RatingAction = "rating";

    public static async Task<JsonElement> CompleteAsync(ApiClient desk, JsonElement ticket)
    {
        using var claim = await desk.PostAsync(TicketWorld.PathOf(ticket, "claim"), new { version = TicketWorld.VersionOf(ticket) });
        Assert.Equal(HttpStatusCode.OK, claim.StatusCode);
        var claimed = await desk.ReadJsonAsync<JsonElement>(claim);
        using var complete = await desk.PostAsync(TicketWorld.PathOf(ticket, "complete"), new { version = TicketWorld.VersionOf(claimed), resolution = "Toner değişti", outcome = "resolved" });
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        return await desk.ReadJsonAsync<JsonElement>(complete);
    }

    public static Task<HttpResponseMessage> RateAsync(ApiClient client, JsonElement ticket, int score, string? comment = null) =>
        client.PutAsync(TicketWorld.PathOf(ticket, RatingAction), new { score, comment });
}

[Collection(ApiCollection.Name)]
public sealed class TicketRatingTests(ApiFactory factory) : IAsyncLifetime
{
    private const int ParallelRounds = 10;
    private const string ServiceAction = "service";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Requester_rates_a_resolved_incident_and_desk_sees_score_and_comment()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee));
        var before = await TicketWorld.ReadAsync(world.Employee, TicketWorld.PathOf(completed));
        Assert.True(before.GetProperty("canRate").GetBoolean());
        Assert.Equal(JsonValueKind.Null, before.GetProperty("rating").ValueKind);

        using var rate = await TicketRatingSteps.RateAsync(world.Employee, completed, 4, "Hızlıydı");

        Assert.Equal(HttpStatusCode.OK, rate.StatusCode);
        var detail = await world.Employee.ReadJsonAsync<JsonElement>(rate);
        Assert.Equal(4, detail.GetProperty("rating").GetProperty("score").GetInt32());
        Assert.Equal("Hızlıydı", detail.GetProperty("rating").GetProperty("comment").GetString());
        Assert.True(detail.GetProperty("canRate").GetBoolean());
        var completedAt = completed.GetProperty("completedAt").GetDateTimeOffset();
        Assert.Equal(completedAt + RatingLimits.Window, detail.GetProperty("rateUntil").GetDateTimeOffset());
        Assert.True(detail.GetProperty("updatedAt").GetDateTimeOffset() > completed.GetProperty("updatedAt").GetDateTimeOffset());

        var desk = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(completed));
        Assert.Equal(4, desk.GetProperty("rating").GetProperty("score").GetInt32());
        Assert.Equal("Hızlıydı", desk.GetProperty("rating").GetProperty("comment").GetString());
        Assert.False(desk.GetProperty("canRate").GetBoolean());

        await using var db = factory.Database.CreateDbContext();
        var audit = await db.AuditEvents.SingleAsync(a => a.Action == AuditActions.RatingSubmitted, Ct);
        Assert.Contains("4", audit.NewValues!, StringComparison.Ordinal);
        Assert.DoesNotContain("Hızlıydı", audit.NewValues!, StringComparison.Ordinal);
        Assert.True(await db.TicketEvents.AnyAsync(e => e.Kind == TicketEventKind.RatingGiven && e.Visibility == Visibility.Internal, Ct));
    }

    [Fact]
    public async Task Rating_needs_no_version_and_a_later_action_with_the_fresh_version_works()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee));

        using var rate = await TicketRatingSteps.RateAsync(world.Employee, completed, 5);
        var fresh = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(completed));
        // Setting the service is allowed on a completed ticket and checks the version; a null service changes nothing.
        using var setService = await world.Desk.PutAsync(TicketWorld.PathOf(completed, ServiceAction), new { serviceId = (Guid?)null, version = TicketWorld.VersionOf(fresh) });

        Assert.Equal(HttpStatusCode.OK, rate.StatusCode);
        Assert.Equal(5, fresh.GetProperty("rating").GetProperty("score").GetInt32());
        Assert.Equal(HttpStatusCode.OK, setService.StatusCode);
    }

    [Fact]
    public async Task Comment_of_500_chars_with_surrounding_spaces_is_stored_trimmed()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee));
        var comment = new string('a', RatingLimits.CommentMaxLength);

        using var rate = await TicketRatingSteps.RateAsync(world.Employee, completed, 4, "  " + comment + "  ");

        Assert.Equal(HttpStatusCode.OK, rate.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        var stored = await db.TicketRatings.SingleAsync(Ct);
        Assert.Equal(comment, stored.Comment);
    }

    [Fact]
    public async Task Second_put_updates_and_writes_ratings_updated()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee));

        using var first = await TicketRatingSteps.RateAsync(world.Employee, completed, 2, "Yavaştı");
        using var second = await TicketRatingSteps.RateAsync(world.Employee, completed, 5, "  ");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var detail = await world.Employee.ReadJsonAsync<JsonElement>(second);
        Assert.Equal(5, detail.GetProperty("rating").GetProperty("score").GetInt32());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("rating").GetProperty("comment").ValueKind);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(1, await db.TicketRatings.CountAsync(Ct));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.RatingUpdated, Ct));
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.RatingSubmitted, Ct));
    }

    [Fact]
    public async Task Desk_member_who_is_not_requester_gets_403_not_requester()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee));
        Assert.True(completed.GetProperty("canWork").GetBoolean());

        using var rate = await TicketRatingSteps.RateAsync(world.Desk, completed, 5);

        Assert.Equal(HttpStatusCode.Forbidden, rate.StatusCode);
        Assert.Equal(ErrorCodes.RatingNotRequester, await ApiClient.ReadCodeAsync(rate));
    }

    [Fact]
    public async Task It_staff_rates_own_ticket()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Field));

        using var rate = await TicketRatingSteps.RateAsync(world.Field, completed, 3);

        Assert.Equal(HttpStatusCode.OK, rate.StatusCode);
    }

    [Fact]
    public async Task Cancelled_ticket_is_409_not_rateable()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        using var cancel = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "cancel"), new { version = TicketWorld.VersionOf(ticket), reason = "Mükerrer" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        using var rate = await TicketRatingSteps.RateAsync(world.Employee, ticket, 4);

        Assert.Equal(HttpStatusCode.Conflict, rate.StatusCode);
        Assert.Equal(ErrorCodes.RatingNotRateable, await ApiClient.ReadCodeAsync(rate));
    }

    [Fact]
    public async Task Open_ticket_is_409_not_rateable()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        Assert.False(ticket.GetProperty("canRate").GetBoolean());
        Assert.Equal(JsonValueKind.Null, ticket.GetProperty("rateUntil").ValueKind);

        using var rate = await TicketRatingSteps.RateAsync(world.Employee, ticket, 4);

        Assert.Equal(HttpStatusCode.Conflict, rate.StatusCode);
        Assert.Equal(ErrorCodes.RatingNotRateable, await ApiClient.ReadCodeAsync(rate));
    }

    [Fact]
    public async Task Other_employee_gets_404()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee));

        using var rate = await TicketRatingSteps.RateAsync(world.OtherEmployee, completed, 4);

        Assert.Equal(HttpStatusCode.NotFound, rate.StatusCode);
    }

    [Fact]
    public async Task Score_zero_and_six_and_comment_501_chars_are_400()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee));

        using var zero = await TicketRatingSteps.RateAsync(world.Employee, completed, RatingLimits.MinScore - 1);
        using var six = await TicketRatingSteps.RateAsync(world.Employee, completed, RatingLimits.MaxScore + 1);
        using var longComment = await TicketRatingSteps.RateAsync(world.Employee, completed, 4, new string('a', RatingLimits.CommentMaxLength + 1));

        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, six.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, longComment.StatusCode);
        await using var db = factory.Database.CreateDbContext();
        Assert.False(await db.TicketRatings.AnyAsync(Ct));
    }

    /// <summary>
    /// Two first ratings at once: the unique index keeps one row and the loser gets 409, never 500. The race is not
    /// forced, so the test repeats on fresh tickets until both requests insert at the same time at least once.
    /// </summary>
    [Fact]
    public async Task Two_parallel_first_ratings_leave_one_row_and_one_409()
    {
        var world = await TicketWorld.CreateAsync(factory);
        var conflicts = 0;

        for (var round = 0; round < ParallelRounds && conflicts == 0; round++)
        {
            var completed = await TicketRatingSteps.CompleteAsync(world.Desk, await TicketWorld.CreateIncidentAsync(world.Employee));
            var responses = await Task.WhenAll(
                TicketRatingSteps.RateAsync(world.Employee, completed, 4),
                TicketRatingSteps.RateAsync(world.Employee, completed, 2));
            var codes = responses.Select(r => r.StatusCode).Order().ToArray();
            foreach (var response in responses.Where(r => r.StatusCode == HttpStatusCode.Conflict))
            {
                Assert.Equal(ErrorCodes.ConcurrencyConflict, await ApiClient.ReadCodeAsync(response));
            }

            Assert.True(codes is [HttpStatusCode.OK, HttpStatusCode.OK] or [HttpStatusCode.OK, HttpStatusCode.Conflict], string.Join(",", codes));
            conflicts += codes.Count(c => c == HttpStatusCode.Conflict);
            responses.ToList().ForEach(r => r.Dispose());

            await using var db = factory.Database.CreateDbContext();
            var ticketId = completed.GetProperty("id").GetGuid();
            Assert.Equal(1, await db.TicketRatings.CountAsync(r => r.TicketId == ticketId, Ct));
        }

        Assert.Equal(1, conflicts);
    }
}
