using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Proteone.Api.IntegrationTests.Infrastructure;
using Proteone.Api.IntegrationTests.Tickets;
using Proteone.Application.Notifications;
using Proteone.Domain.Notifications;
using Proteone.Infrastructure.Persistence.Migrations;

namespace Proteone.Api.IntegrationTests.Notifications;

/// <summary>The completion email carries the rating link only for tickets the requester can rate.</summary>
[Collection(ApiCollection.Name)]
public sealed class RatingLinkEmailTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);
    private const string ServiceRequestRequired = "service_request_required";
    private const string RatingLinkLabel = "Kaydı puanlamak için: ";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<string> CompletedEmailBodyAsync(string outcome)
    {
        var world = await TicketWorld.CreateAsync(factory);
        var ticket = await TicketWorld.CreateIncidentAsync(world.Employee);
        ticket = await ActionAsync(world.Desk, ticket, "claim");
        ticket = await ActionAsync(world.Desk, ticket, "start");
        if (outcome == ServiceRequestRequired)
        {
            // This outcome needs a related request; opening it changes the ticket, so read the version again.
            using var related = await world.Desk.PostAsync(TicketWorld.PathOf(ticket, "related-request"), new { requestTypeId = world.NewLaptop.Id });
            Assert.Equal(HttpStatusCode.Created, related.StatusCode);
            ticket = await TicketWorld.ReadAsync(world.Desk, TicketWorld.PathOf(ticket));
        }

        return await CompleteAndReadBodyAsync(world, ticket, outcome);
    }

    private async Task<string> CompleteAndReadBodyAsync(TicketWorld world, JsonElement ticket, string outcome)
    {
        var number = TicketWorld.NumberOf(ticket);
        await ActionAsync(world.Desk, ticket, "complete", new { version = TicketWorld.VersionOf(ticket), resolution = "Toner değişti", outcome });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DispatchOutboxHandler>().HandleAsync(50, Lease, Ct);
        }

        await using var db = factory.Database.CreateDbContext();
        var delivery = await db.EmailDeliveries.SingleAsync(d => d.TemplateType == NotificationEventTypes.TicketCompleted && d.RecipientUserId == world.EmployeeUser.Id, Ct);
        Assert.Contains(number, delivery.Subject, StringComparison.Ordinal);
        return delivery.Body!;
    }

    private static async Task<JsonElement> ActionAsync(ApiClient client, JsonElement ticket, string action, object? body = null)
    {
        using var response = await client.PostAsync(TicketWorld.PathOf(ticket, action), body ?? new { version = TicketWorld.VersionOf(ticket) });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await client.ReadJsonAsync<JsonElement>(response);
    }

    [Fact]
    public async Task Completed_email_of_resolved_ticket_contains_rating_link()
    {
        var body = await CompletedEmailBodyAsync("resolved");

        Assert.Matches($@"{RatingLinkLabel}{System.Text.RegularExpressions.Regex.Escape(ApiFactory.PublicBaseUrl)}/tickets/[^\s?]+\?rate=1", body);
    }

    [Fact]
    public async Task Completed_email_of_rejected_request_has_no_rating_link()
    {
        var body = await CompletedEmailBodyAsync(ServiceRequestRequired);

        Assert.DoesNotContain("rate=1", body, StringComparison.Ordinal);
        Assert.EndsWith(RatingLinkLabel, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Migration_adds_the_link_to_the_default_body()
    {
        await SetBodyAsync(RatingLinkInCompletedTemplate.PreviousBody);

        await RunMigrationAsync();

        Assert.Equal(RatingLinkInCompletedTemplate.NewBody, await BodyAsync());
    }

    [Fact]
    public async Task Edited_template_is_not_changed_by_the_migration()
    {
        const string edited = "Merhaba {{recipientName}}, kayıt tamam: {{ticketUrl}}";
        await SetBodyAsync(edited);

        await RunMigrationAsync();

        Assert.Equal(edited, await BodyAsync());
    }

    private async Task SetBodyAsync(string body)
    {
        await using var db = factory.Database.CreateDbContext();
        await db.Database.ExecuteSqlAsync($"UPDATE email_templates SET body = {body} WHERE event_type = 'ticket_completed'", Ct);
    }

    private async Task<string> BodyAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        return await db.Database.SqlQuery<string>($"SELECT body AS \"Value\" FROM email_templates WHERE event_type = 'ticket_completed'").SingleAsync(Ct);
    }

    private async Task RunMigrationAsync()
    {
        await using var db = factory.Database.CreateDbContext();
        // Raw SQL goes through the connection: ExecuteSqlRaw would read the braces of {{variables}} as format items.
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = RatingLinkInCompletedTemplate.UpSql;
        await db.Database.OpenConnectionAsync(Ct);
        await command.ExecuteNonQueryAsync(Ct);
    }
}
