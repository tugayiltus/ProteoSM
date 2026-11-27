using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ProteoSM.Application.Abstractions;
using ProteoSM.Domain.Email;

namespace ProteoSM.Infrastructure.Email;

/// <summary>
/// The endpoint the Worker sends with: the panel settings while they are active, else <c>Email:From</c> +
/// <c>Email:Smtp:*</c> exactly as before. Only <see cref="EmailSettings.IsActive"/> decides: a failed test on active
/// settings never switches the sender. The row is cached for <see cref="CacheLifetime"/>; after that only its version is
/// read, and the row is loaded and the password unprotected again only when the version changed.
/// </summary>
internal sealed class ActiveSmtpEndpointProvider(
    IServiceScopeFactory scopes,
    IOptionsMonitor<EmailOptions> options,
    IEmailSecretProtector secrets,
    TimeProvider clock) : IDisposable
{
    public static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(10);

    public const string UnreadablePasswordMessage = "The stored SMTP password of the panel settings cannot be read.";

    private readonly SemaphoreSlim _refresh = new(1, 1);
    private volatile CachedRow? _cached;

    /// <summary>
    /// The row version seen at <paramref name="CheckedAt"/>, and the panel values when the row was active. Plain values,
    /// not an <see cref="SmtpEndpoint"/>: MimeKit subscribes each message to its sender <c>MailboxAddress</c> and never
    /// unsubscribes, so a shared address would keep every sent message in memory.
    /// </summary>
    private sealed record CachedRow(uint Version, SmtpTransport? Panel, DateTimeOffset CheckedAt);

    public async ValueTask<SmtpEndpoint> GetAsync(CancellationToken cancellationToken)
    {
        var cached = _cached;
        if (cached is null || IsStale(cached))
        {
            cached = await RefreshAsync(cancellationToken);
        }

        // The environment values are read on every call, so a changed Email:Smtp:* applies at once, as before.
        return cached.Panel is { } panel ? SmtpEndpoint.FromTransport(panel) : SmtpEndpoint.FromEnvironment(options.CurrentValue);
    }

    public void Dispose() => _refresh.Dispose();

    private bool IsStale(CachedRow cached) => clock.GetUtcNow() - cached.CheckedAt >= CacheLifetime;

    private async Task<CachedRow> RefreshAsync(CancellationToken cancellationToken)
    {
        await _refresh.WaitAsync(cancellationToken);
        try
        {
            // Another send may have refreshed while this one waited.
            if (_cached is { } current && !IsStale(current))
            {
                return current;
            }

            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var now = clock.GetUtcNow();
            var version = await db.EmailSettings.AsNoTracking().Select(s => s.Version).SingleAsync(cancellationToken);
            if (_cached is { } previous && previous.Version == version)
            {
                return _cached = previous with { CheckedAt = now };
            }

            var row = await db.EmailSettings.AsNoTracking().SingleAsync(cancellationToken);

            // A failure throws before the cache changes, so the next send tries again.
            return _cached = new CachedRow(row.Version, row.IsActive ? PanelTransport(row) : null, now);
        }
        finally
        {
            _refresh.Release();
        }
    }

    private SmtpTransport PanelTransport(EmailSettings row)
    {
        string? password;
        try
        {
            password = row.PasswordProtected is null ? null : secrets.Unprotect(row.PasswordProtected);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            // No inner exception and no protected text: the Worker logs and stores this message as the delivery's
            // last error, and the send is tried again later.
            throw new InvalidOperationException(UnreadablePasswordMessage);
        }

        return new SmtpTransport(row.SenderName, row.SenderAddress, row.Host, row.Port, row.Security, row.UserName, password);
    }
}
