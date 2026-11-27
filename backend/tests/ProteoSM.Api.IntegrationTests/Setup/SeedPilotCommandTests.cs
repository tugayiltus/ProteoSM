using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ProteoSM.Api.Cli;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application;
using ProteoSM.Application.Abstractions;
using ProteoSM.Application.Features.Organization;
using ProteoSM.Application.Features.Pilot;
using ProteoSM.Domain.Audit;
using ProteoSM.Domain.Catalog;
using ProteoSM.Domain.Changes;
using ProteoSM.Domain.Problems;
using ProteoSM.Domain.Tasks;
using ProteoSM.Domain.Notifications;
using ProteoSM.Domain.Tickets;
using ProteoSM.Domain.Users;
using ProteoSM.Infrastructure;
using ProteoSM.Infrastructure.Persistence;

namespace ProteoSM.Api.IntegrationTests.Setup;

/// <summary>The <c>seed-pilot</c> command: refusal rules and a small-scale run through the application handlers.</summary>
[Collection(ApiCollection.Name)]
public sealed class SeedPilotCommandTests(ApiFactory factory) : IAsyncLifetime
{
    private const int CliSuccess = 0;
    private const int CliFailure = 1;
    private const string ProductionEnvironment = "Production";
    private const string SmallScale = "0.02";
    private const double SmallScaleFactor = 0.02;
    private const string ChangeScale = "0.1";
    private const double ChangeScaleFactor = 0.1;
    private const string TransferScale = "0.4";
    private const string ScaleOption = "--scale";

    // At 0.4 the seed writes 24 changes: two passes over its plan block, which close changes in every end state.
    private const int ClosedSuccessfulAtTransferScale = 6;
    private const int ClosedRolledBackAtTransferScale = 2;
    private const int ClosedFailedAtTransferScale = 1;
    private const int CancelledAtTransferScale = 1;
    private const int TeamCount = 2;
    private const string PasswordMarker = "(shown once): ";
    private const int TicketsBeforeFailure = 1;

    // The service catalogue is the same at any scale: eight services, each with an owner team and one to four assets.
    private const int SeededServices = 8;
    private const int MinAssetsPerService = 1;
    private const int MaxAssetsPerService = 4;

    // About 30% of the tickets get a service; the bounds leave room for the deterministic draw at a small scale.
    private const int MinServicedTicketPercent = 15;
    private const int MaxServicedTicketPercent = 45;
    private const int Percent = 100;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await factory.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private IServiceProvider ServicesIn(string environment) =>
        factory.WithWebHostBuilder(b => b.UseEnvironment(environment)).Services;

    [Fact]
    public async Task Refuses_in_production_and_writes_nothing()
    {
        var error = new StringWriter();

        var exit = await SeedPilotCommand.RunAsync(ServicesIn(ProductionEnvironment), [ScaleOption, SmallScale], TextWriter.Null, error);

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(CliFailure, exit);
        Assert.Contains(ProductionEnvironment, error.ToString(), StringComparison.Ordinal);
        Assert.False(await db.Users.AnyAsync(Ct));
        Assert.False(await db.AuditEvents.AnyAsync(Ct));
    }

    [Fact]
    public async Task Refuses_when_any_ticket_exists_and_writes_nothing()
    {
        var requester = await TestUsers.CreateAsync(factory, UserRole.Employee, "existing@example.com");
        var team = await TestData.TeamAsync(factory, "Masa", true);
        await TestData.SaveAsync(factory, db => db.Tickets.Add(
            Ticket.CreateIncident("INC-2026-000001", "Konu", "Aciklama", null, null, requester.Id, team.Id, DateTimeOffset.UtcNow)));
        var error = new StringWriter();

        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, SmallScale], TextWriter.Null, error);

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(CliFailure, exit);
        Assert.Contains("tickets", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, await db.Users.CountAsync(Ct));
        Assert.False(await db.AuditEvents.AnyAsync(Ct));
    }

    [Fact]
    public async Task Rejects_an_invalid_scale()
    {
        var error = new StringWriter();

        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, "0"], TextWriter.Null, error);

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(CliFailure, exit);
        Assert.Contains("Usage:", error.ToString(), StringComparison.Ordinal);
        Assert.False(await db.Users.AnyAsync(Ct));
    }

    [Fact]
    public async Task Creates_the_scaled_pilot_data_through_the_handlers()
    {
        var output = new StringWriter();
        var services = ServicesIn(SeedPilotCommand.PilotEnvironment);
        var startedAt = DateTimeOffset.UtcNow;

        var exit = await SeedPilotCommand.RunAsync(services, [ScaleOption, SmallScale], output, TextWriter.Null);

        Assert.Equal(CliSuccess, exit);
        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(Scaled(PilotSeedVolumes.Employees), await db.Users.CountAsync(u => u.Role == UserRole.Employee, Ct));
        Assert.Equal(Scaled(PilotSeedVolumes.ItStaff), await db.Users.CountAsync(u => u.Role == UserRole.ItStaff, Ct));
        Assert.True(await db.Users.AnyAsync(u => u.Role == UserRole.Admin, Ct));

        var assets = await db.Assets.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(Scaled(PilotSeedVolumes.Assets), assets.Count);
        Assert.All(assets, a =>
        {
            Assert.NotEqual(Guid.Empty, a.ModelId);
            Assert.False(string.IsNullOrWhiteSpace(a.Brand));
            Assert.False(string.IsNullOrWhiteSpace(a.Model));
        });
        var modelIds = await db.AssetModels.Select(m => m.Id).ToListAsync(Ct);
        Assert.All(assets, a => Assert.Contains(a.ModelId, modelIds));

        var tickets = await db.Tickets.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(Scaled(PilotSeedVolumes.Tickets), tickets.Count);
        var ticketsWithSla = await db.SlaInstances.Select(s => s.TicketId).Distinct().CountAsync(Ct);
        Assert.Equal(tickets.Count, ticketsWithSla);
        Assert.Equal(Enum.GetValues<TicketStatus>().ToHashSet(), tickets.Select(t => t.Status).ToHashSet());
        Assert.Contains(tickets, t => t.Kind == TicketKind.Request);
        Assert.Contains(tickets, t => t.Kind == TicketKind.Incident);
        var oldestAllowed = startedAt.AddDays(-PilotSeedVolumes.DaysBack - 1);
        Assert.All(tickets, t => Assert.InRange(t.CreatedAt, oldestAllowed, DateTimeOffset.UtcNow));
        Assert.True(tickets.Max(t => t.CreatedAt) - tickets.Min(t => t.CreatedAt) > TimeSpan.FromDays(PilotSeedVolumes.DaysBack / 2));

        Assert.Equal(tickets.Count, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.TicketCreated, Ct));
        Assert.True(await db.AuditEvents.AnyAsync(a => a.Action == AuditActions.AssetCreated, Ct));
        Assert.True(await db.TicketComments.AnyAsync(Ct));
        var ratings = await db.TicketRatings.AsNoTracking().ToListAsync(Ct);
        Assert.NotEmpty(ratings);
        Assert.True(ratings.Select(r => r.Score).Distinct().Count() > 1);
        Assert.All(ratings, r =>
        {
            var rated = tickets.Single(t => t.Id == r.TicketId);
            Assert.Equal(TicketStatus.Completed, rated.Status);
            Assert.Contains(rated.Outcome!.Value, TicketRating.RateableOutcomes);
            Assert.Equal(rated.RequesterId, r.RatedBy);
        });
        Assert.Equal(Scaled(PilotSeedVolumes.Articles), await db.KnowledgeArticles.CountAsync(Ct));
        Assert.True(await db.Categories.AnyAsync(c => c.Name == PilotSeed.PilotCategoryName && c.Scope == CategoryScope.Incident, Ct));

        Assert.Equal(4, await db.Licences.CountAsync(Ct));
        Assert.Equal(2, await db.Releases.CountAsync(Ct));
        Assert.True(await db.LicenceAssignments.CountAsync(a => a.ReleasedAt == null, Ct) > 4);
        var catalogue = await db.Services.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(SeededServices, catalogue.Count);
        var teamIds = await db.Teams.Select(t => t.Id).ToListAsync(Ct);
        Assert.All(catalogue, s => Assert.Contains(s.OwnerTeamId, teamIds));
        var links = await db.ServiceAssets.AsNoTracking().ToListAsync(Ct);
        Assert.All(catalogue, s => Assert.InRange(links.Count(l => l.ServiceId == s.Id), MinAssetsPerService, MaxAssetsPerService));
        var serviced = tickets.Where(t => t.ServiceId != null).ToList();
        Assert.InRange(serviced.Count * Percent, tickets.Count * MinServicedTicketPercent, tickets.Count * MaxServicedTicketPercent);
        Assert.Contains(serviced, t => t.Kind == TicketKind.Incident);
        Assert.Contains(serviced, t => t.Kind == TicketKind.Request);
        Assert.All(serviced, t => Assert.Contains(t.ServiceId!.Value, catalogue.Select(s => s.Id)));

        var printed = output.ToString();
        var password = PrintedPassword(printed);
        Assert.Equal(1, CountOccurrences(printed, password));
        var passwords = services.GetRequiredService<IPasswordService>();
        var employee = await db.Users.AsNoTracking().FirstAsync(u => u.Role == UserRole.Employee, Ct);
        Assert.True(passwords.Verify(employee.PasswordHash, password));
        Assert.False(employee.MustChangePassword);
    }

    [Fact]
    public async Task Seeds_changes_with_overlapping_windows_on_shared_assets_and_work_tasks()
    {
        // 0.1 gives six changes and fifteen tasks: every open change status the seed plans, a closed one and an overlap by index.
        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, ChangeScale], TextWriter.Null, TextWriter.Null);

        Assert.Equal(CliSuccess, exit);
        await using var db = factory.Database.CreateDbContext();
        var changes = await db.ChangeRequests.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(PilotSeedVolumes.Scaled(PilotSeedVolumes.Changes, ChangeScaleFactor), changes.Count);
        Assert.Equal(
            new[] { ChangeStatus.Draft, ChangeStatus.PendingApproval, ChangeStatus.Scheduled, ChangeStatus.Closed }.ToHashSet(), changes.Select(c => c.Status).ToHashSet());
        Assert.All(changes, c => Assert.True(c.WindowStart < c.WindowEnd));
        var links = await db.ChangeAssets.AsNoTracking().ToListAsync(Ct);
        var overlapping = from first in changes
                          from second in changes
                          where first.Id.CompareTo(second.Id) < 0 && first.WindowStart < second.WindowEnd && second.WindowStart < first.WindowEnd
                          where links.Any(a => a.ChangeId == first.Id && links.Any(b => b.ChangeId == second.Id && b.AssetId == a.AssetId))
                          select first.Id;
        Assert.NotEmpty(overlapping);

        var tasks = await db.WorkTasks.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(
            PilotSeedVolumes.Scaled(PilotSeedVolumes.StandaloneTasks, ChangeScaleFactor) + await db.WorkTasks.CountAsync(t => t.ChangeId != null, Ct), tasks.Count);
        Assert.Equal(
            new[] { WorkTaskStatus.Open, WorkTaskStatus.InProgress, WorkTaskStatus.Completed, WorkTaskStatus.Cancelled }.ToHashSet(),
            tasks.Where(t => t.ChangeId == null).Select(t => t.Status).ToHashSet());
        Assert.Contains(tasks, t => t.ChangeId != null);
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.Status == OutboxStatus.Pending, Ct));
    }

    [Fact]
    public async Task Seeds_problems_in_every_state_with_linked_incidents_a_fixing_change_and_an_article()
    {
        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, ChangeScale], TextWriter.Null, TextWriter.Null);

        Assert.Equal(CliSuccess, exit);
        await using var db = factory.Database.CreateDbContext();
        var problems = await db.Problems.AsNoTracking().ToListAsync(Ct);
        Assert.Equal(PilotSeedVolumes.Scaled(PilotSeedVolumes.Problems, ChangeScaleFactor), problems.Count);
        Assert.Equal(Enum.GetValues<ProblemStatus>().ToHashSet(), problems.Select(p => p.Status).ToHashSet());
        Assert.All(problems, p => Assert.Matches(@"^PRB-\d{4}-\d{6}$", p.Number));
        Assert.True(await db.ProblemTicketLinks.AnyAsync(Ct));
        Assert.True(await db.ProblemChangeLinks.AnyAsync(Ct));
        var withArticle = problems.Where(p => p.ArticleId != null).ToList();
        Assert.NotEmpty(withArticle);
        Assert.All(withArticle, p => Assert.Contains(p.Status, new[] { ProblemStatus.KnownError, ProblemStatus.Resolved }));

        // Some problems and some changes name the service they concern, not all of them.
        Assert.Contains(problems, p => p.ServiceId != null);
        Assert.Contains(problems, p => p.ServiceId == null);
        var changes = await db.ChangeRequests.AsNoTracking().ToListAsync(Ct);
        Assert.Contains(changes, c => c.ServiceId != null);
        Assert.Contains(changes, c => c.ServiceId == null);
    }

    // Below about 0.19 the seed has one IT staff member, who is in both teams and sees every incident and change; this
    // scale adds intake-only problem owners next to transferred incidents and field-team changes, which they cannot see.
    [Fact]
    public async Task Links_problems_only_to_incidents_and_changes_their_intake_owner_can_see()
    {
        var error = new StringWriter();
        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, TransferScale], TextWriter.Null, error);

        Assert.True(exit == CliSuccess, error.ToString());
        await using var db = factory.Database.CreateDbContext();
        var intake = await db.Teams.SingleAsync(t => t.IsDefaultIntake, Ct);
        Assert.True(await db.Tickets.AnyAsync(t => t.Kind == TicketKind.Incident && t.TeamId != intake.Id, Ct));
        var dualMembers = await db.TeamMemberships.GroupBy(m => m.UserId).Where(g => g.Count() > 1).Select(g => g.Key).ToListAsync(Ct);
        Assert.True(await db.Problems.AnyAsync(p => p.OwnerId != null && !dualMembers.Contains(p.OwnerId.Value), Ct));
        var linkedTeams = await db.ProblemTicketLinks.Join(db.Tickets, l => l.TicketId, t => t.Id, (_, t) => t.TeamId).Distinct().ToListAsync(Ct);
        Assert.Equal([intake.Id], linkedTeams);
        Assert.True(await db.ChangeRequests.AnyAsync(c => c.OwnerTeamId != intake.Id, Ct));
        var changeTeams = await db.ProblemChangeLinks.Join(db.ChangeRequests, l => l.ChangeId, c => c.Id, (_, c) => c.OwnerTeamId).Distinct().ToListAsync(Ct);
        Assert.Equal([intake.Id], changeTeams);
    }

    // Closed changes walk the whole flow (tasks, implementation, backout) as their owner, in the intake and the field
    // team, so the change report has an outcome mix, a success rate and changes that caused incidents.
    [Fact]
    public async Task Closes_changes_in_every_end_state_and_ties_backed_out_ones_to_the_incidents_they_caused()
    {
        var error = new StringWriter();
        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, TransferScale], TextWriter.Null, error);

        Assert.True(exit == CliSuccess, error.ToString());
        await using var db = factory.Database.CreateDbContext();
        var changes = await db.ChangeRequests.AsNoTracking().ToListAsync(Ct);
        var closed = changes.Where(c => c.Status == ChangeStatus.Closed).ToList();
        Assert.Equal(ClosedSuccessfulAtTransferScale, closed.Count(c => c.Outcome == ChangeOutcome.Successful));
        Assert.Equal(ClosedRolledBackAtTransferScale, closed.Count(c => c.Outcome == ChangeOutcome.RolledBack));
        Assert.Equal(ClosedFailedAtTransferScale, closed.Count(c => c.Outcome == ChangeOutcome.Failed));
        Assert.Equal(CancelledAtTransferScale, changes.Count(c => c.Status == ChangeStatus.Cancelled));
        Assert.Equal(TeamCount, closed.Select(c => c.OwnerTeamId).Distinct().Count());
        Assert.All(closed, c => Assert.True(c.ActualStart >= c.WindowStart && c.ClosedAt >= c.ActualStart && c.ActualEnd >= c.ActualStart));

        var causing = await db.ChangeTicketLinks
            .Where(l => l.Kind == ChangeTicketLinkKind.Caused)
            .Join(db.Tickets, l => l.TicketId, t => t.Id, (l, t) => new { l.ChangeId, t.Kind })
            .ToListAsync(Ct);
        Assert.All(causing, l => Assert.Equal(TicketKind.Incident, l.Kind));
        Assert.All(closed.Where(c => c.Outcome != ChangeOutcome.Successful), c => Assert.Contains(causing, l => l.ChangeId == c.Id));
        var closedIds = closed.Select(c => c.Id).ToList();
        var closedTasks = await db.WorkTasks.AsNoTracking().Where(t => t.ChangeId != null && closedIds.Contains(t.ChangeId.Value)).ToListAsync(Ct);
        Assert.NotEmpty(closedTasks);
        Assert.All(closedTasks, t => Assert.False(t.IsOpen));
    }

    [Fact]
    public async Task Seeds_location_names_the_csv_importer_can_address()
    {
        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, SmallScale], TextWriter.Null, TextWriter.Null);

        Assert.Equal(CliSuccess, exit);
        await using var db = factory.Database.CreateDbContext();
        var names = await db.Locations.AsNoTracking().Select(l => l.Name).ToListAsync(Ct);
        Assert.NotEmpty(names);
        Assert.All(names, name => Assert.DoesNotContain(LocationPaths.SegmentSeparator, name));
        Assert.Contains(await db.Locations.AsNoTracking().ToListAsync(Ct), l => l.ParentId is not null);
    }

    [Fact]
    public async Task Leaves_no_seeded_notification_for_the_worker_to_send()
    {
        // The seed writes outbox rows through the handlers; a pilot Worker on a real relay must never mail them.
        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, SmallScale], TextWriter.Null, TextWriter.Null);

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(CliSuccess, exit);
        Assert.True(await db.OutboxEvents.AnyAsync(Ct));
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.Status == OutboxStatus.Pending, Ct));
        Assert.All(await db.OutboxEvents.AsNoTracking().ToListAsync(Ct), e => Assert.NotNull(e.DispatchedAt));
    }

    [Fact]
    public async Task A_failed_seed_still_closes_the_notifications_it_queued()
    {
        // The second ticket fails after the first one committed its notifications; those must not reach the Worker.
        await using var services = SeedServicesWith(new FailingTicketAuditLog(TicketsBeforeFailure));

        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<PilotSeed>().RunAsync(SmallScaleFactor, Ct));

        await using var db = factory.Database.CreateDbContext();
        Assert.Equal(TicketsBeforeFailure, await db.Tickets.CountAsync(Ct));
        Assert.True(await db.OutboxEvents.AnyAsync(Ct));
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.Status == OutboxStatus.Pending, Ct));
    }

    [Fact]
    public async Task A_seed_failure_is_not_masked_when_the_run_is_also_cancelled()
    {
        // The run is cancelled as the seed fails (Ctrl+C): the seed's own exception reaches the caller, and the
        // notifications it queued are still closed.
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        await using var services = SeedServicesWith(new FailingTicketAuditLog(TicketsBeforeFailure, cancellation));

        await Assert.ThrowsAsync<InvalidOperationException>(() => services.GetRequiredService<PilotSeed>().RunAsync(SmallScaleFactor, cancellation.Token));

        await using var db = factory.Database.CreateDbContext();
        Assert.True(await db.OutboxEvents.AnyAsync(Ct));
        Assert.False(await db.OutboxEvents.AnyAsync(e => e.Status == OutboxStatus.Pending, Ct));
    }

    [Fact]
    public async Task Prints_the_breach_mail_warning()
    {
        var output = new StringWriter();

        var exit = await SeedPilotCommand.RunAsync(ServicesIn(SeedPilotCommand.PilotEnvironment), [ScaleOption, SmallScale], output, TextWriter.Null);

        Assert.Equal(CliSuccess, exit);
        Assert.Contains(PilotSeed.EmailDomain, output.ToString(), StringComparison.Ordinal);
        Assert.Contains("EMAIL_MODE=Pickup", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_same_seed_produces_the_same_data()
    {
        var services = ServicesIn(SeedPilotCommand.PilotEnvironment);

        Assert.Equal(CliSuccess, await SeedPilotCommand.RunAsync(services, [ScaleOption, SmallScale], TextWriter.Null, TextWriter.Null));
        var first = await SnapshotAsync();
        await factory.ResetAsync();
        Assert.Equal(CliSuccess, await SeedPilotCommand.RunAsync(services, [ScaleOption, SmallScale], TextWriter.Null, TextWriter.Null));
        var second = await SnapshotAsync();

        Assert.NotEmpty(first);
        Assert.Equal(first, second);
    }

    private static int Scaled(int volume) => PilotSeedVolumes.Scaled(volume, SmallScaleFactor);

    /// <summary>The seed command's composition (Application + Infrastructure, seed actor and clock) with another audit log.</summary>
    private ServiceProvider SeedServicesWith(IAuditLog audit)
    {
        var services = new ServiceCollection();
        services.AddSingleton(factory.Services.GetRequiredService<IConfiguration>());
        services.AddSingleton(factory.Services.GetRequiredService<IHostEnvironment>());
        services.AddLogging();
        services.AddSingleton<PilotSeedClock>();
        services.AddSingleton<TimeProvider>(provider => provider.GetRequiredService<PilotSeedClock>());
        services.AddSingleton<PilotSeedActor>();
        services.AddSingleton<ICurrentActor>(provider => provider.GetRequiredService<PilotSeedActor>());
        services.AddApplication();
        services.AddInfrastructure();
        services.AddSingleton(audit);
        services.AddSingleton<PilotSeed>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }

    private static string PrintedPassword(string output)
    {
        var start = output.IndexOf(PasswordMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "The pilot password is printed.");
        var rest = output[(start + PasswordMarker.Length)..];
        return rest[..rest.IndexOfAny(['\r', '\n'])];
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>Everything that does not depend on the moment of the run (ids, timestamps).</summary>
    private async Task<List<string>> SnapshotAsync()
    {
        await using AppDbContext db = factory.Database.CreateDbContext();
        var tickets = await (
            from ticket in db.Tickets
            join requester in db.Users on ticket.RequesterId equals requester.Id
            join assignee in db.Users on ticket.AssigneeId equals assignee.Id into assignees
            from assignee in assignees.DefaultIfEmpty()
            orderby ticket.Number
            select $"{ticket.Number}|{ticket.Kind}|{ticket.Subject}|{ticket.Status}|{ticket.Priority}|{requester.Email}|{(assignee == null ? "-" : assignee.Email)}")
            .ToListAsync(Ct);
        var assets = await db.Assets.OrderBy(a => a.SerialNumber)
            .Select(a => $"{a.SerialNumber}|{a.Brand}|{a.Model}|{a.Status}")
            .ToListAsync(Ct);
        var comments = await db.TicketComments.CountAsync(Ct);
        return [.. tickets, .. assets, $"comments={comments}"];
    }

    /// <summary>
    /// Records nothing and throws on the ticket creation after <paramref name="allowedTickets"/>, first cancelling
    /// <paramref name="cancelOnFailure"/> when given.
    /// </summary>
    private sealed class FailingTicketAuditLog(int allowedTickets, CancellationTokenSource? cancelOnFailure = null) : IAuditLog
    {
        private int _ticketsCreated;

        public void Record(string action, string entityType, Guid? entityId, object? oldValues = null, object? newValues = null, string? reason = null, Guid? actorUserId = null)
        {
            if (action == AuditActions.TicketCreated && Interlocked.Increment(ref _ticketsCreated) > allowedTickets)
            {
                cancelOnFailure?.Cancel();
                throw new InvalidOperationException("Simulated failure while seeding tickets.");
            }
        }
    }
}
