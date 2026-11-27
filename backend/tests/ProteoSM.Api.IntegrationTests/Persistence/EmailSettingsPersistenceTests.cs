using Microsoft.EntityFrameworkCore;
using Npgsql;
using ProteoSM.Api.IntegrationTests.Infrastructure;
using ProteoSM.Application.Common;
using ProteoSM.Domain.Email;

namespace ProteoSM.Api.IntegrationTests.Persistence;

[Collection(DatabaseCollection.Name)]
public sealed class EmailSettingsPersistenceTests(PostgresDatabase database) : IAsyncLifetime
{
    private const string CheckViolation = "23514";
    private const string PortCheckName = "ck_email_settings_port";
    private static readonly DateTimeOffset Now = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => await database.ResetAsync();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Migration_seeds_one_inactive_google_row()
    {
        await using var db = database.CreateDbContext();

        var settings = await db.EmailSettings.SingleAsync(Ct);

        Assert.Equal(EmailSettings.SingletonId, settings.Id);
        Assert.Equal((EmailProvider.Google, "smtp.gmail.com", 587, EmailSecurity.StartTls), (settings.Provider, settings.Host, settings.Port, settings.Security));
        Assert.Equal(string.Empty, settings.SenderAddress);
        Assert.False(settings.IsActive);
        Assert.Null(settings.TestedFingerprint);
        Assert.NotEqual(0u, settings.Version);
    }

    [Fact]
    public async Task Settings_round_trip()
    {
        await using (var db = database.CreateDbContext())
        {
            var settings = await db.EmailSettings.SingleAsync(Ct);
            settings.Update(EmailProvider.Smtp, "Servis Masası", "servis@sirket.com", "mail.sirket.local", 465, EmailSecurity.SslTls, "servis", "protected-1", Now);
            settings.RecordTest(true, null, Now);
            settings.Activate(Now);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var loaded = await read.EmailSettings.SingleAsync(Ct);
        Assert.Equal((EmailProvider.Smtp, EmailSecurity.SslTls, 465), (loaded.Provider, loaded.Security, loaded.Port));
        Assert.Equal(("Servis Masası", "servis", "protected-1"), (loaded.SenderName, loaded.UserName, loaded.PasswordProtected));
        Assert.True(loaded.IsActive);
        Assert.True(loaded.IsTested);
        Assert.Equal((Now, (bool?)true), (loaded.LastTestedAt, loaded.LastTestSucceeded));
    }

    [Fact]
    public async Task Enums_are_stored_as_snake_case_text()
    {
        await using (var db = database.CreateDbContext())
        {
            var settings = await db.EmailSettings.SingleAsync(Ct);
            settings.Update(EmailProvider.Smtp, null, "servis@sirket.com", "mail.sirket.local", 465, EmailSecurity.SslTls, null, null, Now);
            await db.SaveChangesAsync(Ct);
        }

        await using var read = database.CreateDbContext();
        var stored = await read.Database.SqlQueryRaw<string>("SELECT provider || '/' || security AS \"Value\" FROM email_settings").SingleAsync(Ct);
        Assert.Equal("smtp/ssl_tls", stored);
    }

    [Fact]
    public async Task Port_check_rejects_zero()
    {
        await using var db = database.CreateDbContext();

        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE email_settings SET port = 0", Ct));

        Assert.Equal((CheckViolation, PortCheckName), (error.SqlState, error.ConstraintName));
    }

    [Fact]
    public async Task Stale_version_save_throws_concurrency()
    {
        await using var first = database.CreateDbContext();
        await using var second = database.CreateDbContext();
        var firstCopy = await first.EmailSettings.SingleAsync(Ct);
        var secondCopy = await second.EmailSettings.SingleAsync(Ct);
        first.SetExpectedVersion(firstCopy, firstCopy.Version);
        second.SetExpectedVersion(secondCopy, secondCopy.Version);

        firstCopy.Update(EmailProvider.Smtp, null, "bir@sirket.com", "mail.sirket.local", 587, EmailSecurity.StartTls, null, null, Now);
        await first.SaveChangesAsync(Ct);
        secondCopy.Update(EmailProvider.Smtp, null, "iki@sirket.com", "mail.sirket.local", 587, EmailSecurity.StartTls, null, null, Now);

        await Assert.ThrowsAsync<ConcurrencyException>(() => second.SaveChangesAsync(Ct));
    }
}
