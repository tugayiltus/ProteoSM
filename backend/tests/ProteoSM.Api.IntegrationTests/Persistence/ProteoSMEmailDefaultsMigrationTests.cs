using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Infrastructure.Persistence.Migrations;

namespace ProteoSM.Api.IntegrationTests.Persistence;

/// <summary>The product name and footer reach default email templates only; an edited template stays as the admin left it.</summary>
public sealed class ProteoSMEmailDefaultsMigrationTests(PostgresDatabase database) : IClassFixture<PostgresDatabase>
{
    private const string BeforeMigration = "ProblemSlaTimerNoConcurrency";
    private const string Activation = "account_activation";
    private const string PasswordReset = "password_reset";
    private const string Footer = "ProteoSM by ProteONE";
    private const string EditedBody = "Proteone hesabı, {{actionUrl}} (yönetici düzenledi)\n";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_unchanged_default_gets_the_new_name_and_footer_and_an_edited_template_stays_byte_identical()
    {
        await using var db = database.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeMigration, Ct);
        var before = await db.EmailTemplates.AsNoTracking().ToDictionaryAsync(t => t.EventType, Ct);
        Assert.Contains("Proteone", before[Activation].Body, StringComparison.Ordinal);
        await db.Database.ExecuteSqlRawAsync("UPDATE email_templates SET body = {0} WHERE event_type = {1}", [EditedBody, PasswordReset], Ct);

        await migrator.MigrateAsync(cancellationToken: Ct);

        var after = await db.EmailTemplates.AsNoTracking().ToDictionaryAsync(t => t.EventType, Ct);
        Assert.DoesNotContain("Proteone", after[Activation].Body, StringComparison.Ordinal);
        Assert.Contains("ProteoSM hesabınızı", after[Activation].Body, StringComparison.Ordinal);
        Assert.EndsWith("\n\n" + Footer, after[Activation].Body, StringComparison.Ordinal);
        Assert.Equal(EditedBody, after[PasswordReset].Body);
        Assert.Equal(before[PasswordReset].Subject, after[PasswordReset].Subject);
        foreach (var (eventType, template) in after.Where(pair => pair.Key != PasswordReset))
        {
            Assert.EndsWith("\n\n" + Footer, template.Body, StringComparison.Ordinal);
            Assert.Equal(before[eventType].Subject, template.Subject);
        }
    }

    [Fact]
    public async Task A_template_with_an_edited_subject_is_left_alone_even_when_its_body_is_the_default()
    {
        await using var db = database.CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforeMigration, Ct);
        await db.Database.ExecuteSqlRawAsync("UPDATE email_templates SET subject = 'Özel konu' WHERE event_type = {0}", [Activation], Ct);
        var body = await db.EmailTemplates.AsNoTracking().Where(t => t.EventType == Activation).Select(t => t.Body).SingleAsync(Ct);

        await migrator.MigrateAsync(cancellationToken: Ct);

        var after = await db.EmailTemplates.AsNoTracking().SingleAsync(t => t.EventType == Activation, Ct);
        Assert.Equal("Özel konu", after.Subject);
        Assert.Equal(body, after.Body);
    }

    [Fact]
    public void Every_default_names_its_own_event_type_once()
    {
        var types = ProteoSMEmailDefaults.Defaults.Select(d => d.EventType).ToList();
        Assert.Equal(types.Count, types.Distinct().Count());
    }
}
