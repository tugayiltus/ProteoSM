using System.Collections.Concurrent;
using ProteoSM.Application.Abstractions;

namespace ProteoSM.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test double for <see cref="IEmailTestSender"/>: no network in tests. It records every transport and message it gets
/// and answers with <see cref="Result"/>, which passes by default.
/// </summary>
public sealed class FakeEmailTestSender : IEmailTestSender
{
    private static readonly EmailTestResult Passed = new(true, null, null);

    private readonly ConcurrentQueue<(SmtpTransport Transport, EmailMessage Message)> _sent = new();

    public EmailTestResult Result { get; set; } = Passed;

    /// <summary>Runs before each send; lets a test change the database while the test send is in flight.</summary>
    public Func<Task>? BeforeSend { get; set; }

    public IReadOnlyList<(SmtpTransport Transport, EmailMessage Message)> Sent => [.. _sent];

    public async Task<EmailTestResult> SendTestAsync(SmtpTransport transport, EmailMessage message, CancellationToken cancellationToken)
    {
        if (BeforeSend is { } beforeSend)
        {
            await beforeSend();
        }

        _sent.Enqueue((transport, message));
        return Result;
    }

    public void Reset()
    {
        Result = Passed;
        BeforeSend = null;
        _sent.Clear();
    }
}
