using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Proteone.Api.IntegrationTests.Infrastructure;

namespace Proteone.Api.IntegrationTests.Persistence;

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
}
