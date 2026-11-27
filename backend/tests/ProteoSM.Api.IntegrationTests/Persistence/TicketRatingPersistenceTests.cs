using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Domain.Common;
using ProteoSM.Domain.Organization;
using ProteoSM.Domain.Tickets;
using ProteoSM.Domain.Users;
using ProteoSM.Infrastructure.Persistence;

namespace ProteoSM.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class TicketRatingPersistenceTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string UniqueViolation = "23505";
    private const string CheckViolation = "23514";
    private const string ScoreCheckName = "ck_ticket_ratings_score";
    private const int ScoreAboveMaximum = RatingLimits.MaxScore + 1;
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private User _requester = null!;
    private Team _team = null!;
    private Ticket _ticket = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await database.ResetAsync();
        _requester = User.Create("Ayşe", "Kaya", "ayse@example.com", UserRole.Employee, null, null, "hash", Now);
        _team = Team.Create("Servis Masası");
        _ticket = Ticket.CreateIncident("INC-2026-000001", "Yazıcı", "Çalışmıyor", null, null, _requester.Id, _team.Id, Now);
        _ticket.Start(Now);
        _ticket.Complete("Giderildi", TicketOutcome.Resolved, false, false, Now);
        await using var db = database.CreateDbContext();
        db.Users.Add(_requester);
        db.Teams.Add(_team);
        db.Tickets.Add(_ticket);
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private TicketRating NewRating(int score = 4, string? comment = "İyi") =>
        TicketRating.Create(_ticket, _requester.Id, score, comment, Now);

    [Fact]
    public async Task Rating_round_trips()
    {
        var rating = NewRating(5, "Hızlıydı");
        await using (var db = database.CreateDbContext())
        {
            db.TicketRatings.Add(rating);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var loaded = await read.TicketRatings.SingleAsync(r => r.TicketId == _ticket.Id, Ct);
        Assert.Equal((5, "Hızlıydı", _team.Id, _requester.Id), (loaded.Score, loaded.Comment, loaded.TeamId, loaded.RatedBy));
        Assert.Null(loaded.AssigneeId);
        Assert.NotEqual(0u, loaded.Version);
    }

    [Fact]
    public async Task Second_rating_row_for_one_ticket_violates_the_unique_index()
    {
        await using (var db = database.CreateDbContext())
        {
            db.TicketRatings.Add(NewRating());
            await db.SaveChangesAsync(Ct);
        }

        await using var again = database.CreateDbContext();
        again.TicketRatings.Add(NewRating(2, null));
        // The context turns a unique violation into a domain conflict (409).
        var error = await Assert.ThrowsAsync<DomainException>(() => again.SaveChangesAsync(Ct));
        var cause = Assert.IsType<DbUpdateException>(error.InnerException);
        Assert.Equal(UniqueViolation, Assert.IsType<PostgresException>(cause.InnerException).SqlState);
    }

    [Fact]
    public async Task Score_check_rejects_six()
    {
        await using var db = database.CreateDbContext();
        var sql = $"""
            INSERT INTO ticket_ratings (id, ticket_id, score, team_id, rated_by, rated_at, updated_at)
            VALUES (gen_random_uuid(), '{_ticket.Id}', {ScoreAboveMaximum}, '{_team.Id}', '{_requester.Id}', now(), now())
            """;

        var error = await Assert.ThrowsAnyAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql, Ct));

        Assert.Equal((CheckViolation, ScoreCheckName), (error.SqlState, error.ConstraintName));
    }
}
