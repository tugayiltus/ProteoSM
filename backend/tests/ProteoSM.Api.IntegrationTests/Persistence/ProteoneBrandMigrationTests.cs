using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Domain.Audit;

namespace ProteoSM.Api.IntegrationTests.Persistence;

/// <summary>An existing installation holds the former product name in its email templates; a new one starts without it.</summary>
public sealed class ProteoneBrandMigrationTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    private const string BeforeBrand = "AddServices";
    private const string FormerName = "TGYSM";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_former_name_leaves_seeded_and_edited_templates_and_the_rest_of_the_text_stays()
    {
        await using var db = database.CreateDbContext();
        Assert.False(await db.EmailTemplates.AnyAsync(t => t.Subject.Contains(FormerName) || t.Body.Contains(FormerName), Ct));

        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeBrand, Ct);
        await db.Database.ExecuteSqlRawAsync(
            "UPDATE email_templates SET subject = 'TGYSM: {{{{subject}}}}', body = 'TGYSM hesabı, {{{{actionUrl}}}}' WHERE event_type = 'account_activation'", Ct);
        await migrator.MigrateAsync(cancellationToken: Ct);

        var edited = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.EventType == "account_activation", Ct);
        Assert.Equal("Proteone: {{subject}}", edited.Subject);
        Assert.Equal("Proteone hesabı, {{actionUrl}}", edited.Body);
    }

    [Fact]
    public async Task The_former_name_leaves_history_but_directory_paths_stay_and_the_audit_trail_is_closed_again()
    {
        await using var db = database.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("ProteoneBrand", Ct);
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO audit_events (id, occurred_at, action, entity_type, reason, new_values, correlation_id) VALUES
            (gen_random_uuid(), now(), 'categories.created', 'category', 'TGYSM için', '{{"name": "TGYSM Pilot", "filter": "(memberOf=CN=TGYSM,DC=x)"}}', 'c');
            """, Ct);

        await migrator.MigrateAsync(cancellationToken: Ct);

        var changed = await db.AuditEvents.AsNoTracking().SingleAsync(a => a.Action == "categories.created", Ct);
        Assert.Equal("Proteone için", changed.Reason);
        Assert.Contains("Proteone Pilot", changed.NewValues!, StringComparison.Ordinal);
        Assert.Contains("CN=TGYSM", changed.NewValues!, StringComparison.Ordinal);
        Assert.Equal(1, await db.AuditEvents.CountAsync(a => a.Action == AuditActions.ProductNameReplaced, Ct));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE audit_events SET reason = 'x'", Ct));
    }
}
