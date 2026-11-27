using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ProteoSM.Api.IntegrationTests.Infrastructure;

/// <summary>
/// How <see cref="ScriptedSmtpServer"/> answers. <see cref="Silent"/> accepts the connection and never answers.
/// <see cref="AuthReplyText"/> replaces the default text of the AUTH reply (to test what a server echoes back).
/// </summary>
public sealed record SmtpScript(
    bool Silent = false,
    bool OfferAuth = false,
    int AuthReply = 235,
    int MailFromReply = 250,
    int RcptReply = 250,
    string? AuthReplyText = null);

/// <summary>
/// A plain-text SMTP server on the loopback address, port chosen by the system. It follows a fixed script and
/// records each command line it gets, so tests never touch the network and never need a real mail server.
/// </summary>
public sealed class ScriptedSmtpServer : IAsyncDisposable
{
    private const int OkReply = 250;
    private const int ReadyReply = 220;
    private const int StartDataReply = 354;
    private const int ClosingReply = 221;
    private const int AuthContinueReply = 334;
    private const int UnknownCommandReply = 500;
    private const string DataEnd = ".";

    private readonly TcpListener _listener;
    private readonly SmtpScript _script;
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentQueue<string> _commands = new();
    private readonly ConcurrentBag<TcpClient> _clients = [];
    private readonly Task _acceptLoop;

    private ScriptedSmtpServer(SmtpScript script)
    {
        _script = script;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = AcceptAsync();
    }

    public int Port { get; }

    /// <summary>Each command line in the order the server got it (message body lines excluded).</summary>
    public IReadOnlyList<string> Commands => [.. _commands];

    public int ConnectionCount => _clients.Count;

    public static ScriptedSmtpServer Start(SmtpScript script) => new(script);

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        foreach (var client in _clients)
        {
            client.Dispose();
        }

        try
        {
            await _acceptLoop;
        }
        catch (Exception exception) when (exception is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // The listener was stopped on purpose.
        }

        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            var client = await _listener.AcceptTcpClientAsync(_stop.Token);
            _clients.Add(client);
            _ = ServeAsync(client);
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        if (_script.Silent)
        {
            return;
        }

        try
        {
            var stream = client.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII);
            await using var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true };
            await writer.WriteLineAsync($"{ReadyReply} scripted ESMTP");
            while (await reader.ReadLineAsync(_stop.Token) is { } line)
            {
                _commands.Enqueue(line);
                if (!await AnswerAsync(line, reader, writer))
                {
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The client closed the connection or the server stopped.
        }
    }

    private async Task<bool> AnswerAsync(string line, StreamReader reader, StreamWriter writer)
    {
        var verb = line.Split(' ', 2)[0].ToUpperInvariant();
        switch (verb)
        {
            case "EHLO":
                if (_script.OfferAuth)
                {
                    await writer.WriteLineAsync($"{OkReply}-scripted");
                    await writer.WriteLineAsync($"{OkReply} AUTH PLAIN");
                }
                else
                {
                    await writer.WriteLineAsync($"{OkReply} scripted");
                }

                return true;
            case "HELO":
            case "RSET":
            case "NOOP":
                await writer.WriteLineAsync($"{OkReply} OK");
                return true;
            case "AUTH":
                if (line.Split(' ').Length < 3)
                {
                    // AUTH PLAIN without an initial response: ask for it, then answer.
                    await writer.WriteLineAsync($"{AuthContinueReply} ");
                    if (await reader.ReadLineAsync(_stop.Token) is null)
                    {
                        return false;
                    }
                }

                await writer.WriteLineAsync($"{_script.AuthReply} {_script.AuthReplyText ?? "authentication result"}");
                return true;
            case "MAIL":
                await writer.WriteLineAsync($"{_script.MailFromReply} sender result");
                return true;
            case "RCPT":
                await writer.WriteLineAsync($"{_script.RcptReply} recipient result");
                return true;
            case "DATA":
                await writer.WriteLineAsync($"{StartDataReply} end with <CRLF>.<CRLF>");
                while (await reader.ReadLineAsync(_stop.Token) is { } body && body != DataEnd)
                {
                }

                await writer.WriteLineAsync($"{OkReply} queued");
                return true;
            case "QUIT":
                await writer.WriteLineAsync($"{ClosingReply} bye");
                return false;
            default:
                await writer.WriteLineAsync($"{UnknownCommandReply} unknown command");
                return true;
        }
    }
}
