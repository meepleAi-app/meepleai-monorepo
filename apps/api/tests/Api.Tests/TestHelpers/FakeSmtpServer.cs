using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace Api.Tests.TestHelpers;

/// <summary>
/// A loopback SMTP server for tests that need to see what <see cref="System.Net.Mail.SmtpClient"/> really puts
/// on the wire (#4812). Most of <c>EmailService</c> builds its <c>SmtpClient</c> by hand, with no
/// <c>IEmailSender</c> to substitute, so a test that wants the subject and body the recipient would read has
/// nothing to capture them from — except the socket.
/// </summary>
/// <remarks>
/// It speaks just enough SMTP (no TLS, no AUTH) and keeps each message decoded the way a mail client would
/// show it: the subject through RFC 2047 encoded words, the body through its transfer encoding and charset.
/// A UTF-8 body or subject does not arrive as the characters that were written, so asserting on the raw
/// bytes would pin the encoding instead of the text.
/// </remarks>
internal sealed class FakeSmtpServer : IAsyncDisposable
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _stop = new();
    private readonly object _gate = new();
    private readonly List<ReceivedEmail> _received = [];
    private readonly List<Task> _connections = [];
    private readonly Task _accepting;

    public FakeSmtpServer()
    {
        _listener.Start();
        _accepting = AcceptAsync();
    }

    public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

    /// <summary>The messages accepted so far, oldest first.</summary>
    public IReadOnlyList<ReceivedEmail> Received
    {
        get
        {
            lock (_gate)
            {
                return [.. _received];
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();

        Task[] connections;
        lock (_gate)
        {
            connections = [.. _connections];
        }

        await Task.WhenAny(
            Task.WhenAll(connections.Append(_accepting)),
            Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
        _stop.Dispose();
    }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stop.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
                lock (_gate)
                {
                    _connections.Add(ServeAsync(client));
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
        {
            // The server is being disposed.
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                var stream = client.GetStream();

                // Latin-1 maps every byte to one char, so nothing is altered on the way in: the charset is applied
                // later, once the message has been taken apart.
                using var reader = new StreamReader(stream, Encoding.Latin1);
                await using var writer = new StreamWriter(stream, Encoding.Latin1) { NewLine = "\r\n", AutoFlush = true };

                await writer.WriteLineAsync("220 fake-smtp ready").ConfigureAwait(false);

                string? from = null;
                string? to = null;
                while (await reader.ReadLineAsync(_stop.Token).ConfigureAwait(false) is { } line)
                {
                    switch (line.Split(' ', 2)[0].ToUpperInvariant())
                    {
                        case "EHLO":
                        case "HELO":
                            await writer.WriteLineAsync("250 fake-smtp").ConfigureAwait(false);
                            break;
                        case "MAIL":
                            from = AddressOf(line);
                            await writer.WriteLineAsync("250 OK").ConfigureAwait(false);
                            break;
                        case "RCPT":
                            to = AddressOf(line);
                            await writer.WriteLineAsync("250 OK").ConfigureAwait(false);
                            break;
                        case "DATA":
                            await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>").ConfigureAwait(false);
                            var data = await ReadDataAsync(reader).ConfigureAwait(false);
                            lock (_gate)
                            {
                                _received.Add(ReceivedEmail.Parse(from, to, data));
                            }

                            await writer.WriteLineAsync("250 OK queued").ConfigureAwait(false);
                            break;
                        case "RSET":
                        case "NOOP":
                            await writer.WriteLineAsync("250 OK").ConfigureAwait(false);
                            break;
                        case "QUIT":
                            await writer.WriteLineAsync("221 bye").ConfigureAwait(false);
                            return;
                        default:
                            await writer.WriteLineAsync("502 command not implemented").ConfigureAwait(false);
                            break;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
        {
            // The client went away, or the server is being disposed.
        }
    }

    private static string AddressOf(string command)
    {
        var open = command.IndexOf('<', StringComparison.Ordinal);
        var close = command.IndexOf('>', StringComparison.Ordinal);
        return open >= 0 && close > open ? command[(open + 1)..close] : command;
    }

    /// <summary>Reads up to the lone "." line, undoing the dot-stuffing SMTP applies to lines that start with one.</summary>
    private async Task<string> ReadDataAsync(StreamReader reader)
    {
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(_stop.Token).ConfigureAwait(false) is { } line)
        {
            if (line == ".")
                break;

            data.Append(line.StartsWith("..", StringComparison.Ordinal) ? line[1..] : line).Append("\r\n");
        }

        return data.ToString();
    }
}

/// <summary>A message as <see cref="FakeSmtpServer"/> received it; <see cref="Subject"/> and <see cref="Body"/> are decoded.</summary>
internal sealed record ReceivedEmail(
    string? From,
    string? To,
    string Subject,
    string Body,
    IReadOnlyDictionary<string, string> Headers)
{
    private static readonly Regex EncodedWord = new(
        @"=\?(?<charset>[^?]+)\?(?<enc>[bBqQ])\?(?<text>[^?]*)\?=",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(2));

    // A long subject arrives as several encoded words separated by folding whitespace, which a reader drops.
    private static readonly Regex GapBetweenEncodedWords = new(
        @"(?<=\?=)\s+(?==\?)",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static readonly Regex Charset = new(
        @"charset\s*=\s*""?(?<name>[^"";\s]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture,
        TimeSpan.FromSeconds(2));

    internal static ReceivedEmail Parse(string? from, string? to, string data)
    {
        var separator = data.IndexOf("\r\n\r\n", StringComparison.Ordinal);
        var headerBlock = separator < 0 ? data : data[..separator];
        var rawBody = separator < 0 ? string.Empty : data[(separator + 4)..];

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? current = null;
        foreach (var line in headerBlock.Split("\r\n"))
        {
            if (line.Length > 0 && (line[0] == ' ' || line[0] == '\t') && current is not null)
            {
                headers[current] += " " + line.Trim();
                continue;
            }

            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0)
                continue;

            current = line[..colon].Trim();
            headers[current] = line[(colon + 1)..].Trim();
        }

        var subject = DecodeHeader(headers.GetValueOrDefault("Subject", string.Empty));
        return new ReceivedEmail(from, to, subject, DecodeBody(headers, rawBody), headers);
    }

    private static string DecodeHeader(string value)
    {
        value = GapBetweenEncodedWords.Replace(value, string.Empty);
        return EncodedWord.Replace(value, match =>
        {
            var encoding = Encoding.GetEncoding(match.Groups["charset"].Value);
            var text = match.Groups["text"].Value;
            var bytes = char.ToUpperInvariant(match.Groups["enc"].Value[0]) == 'B'
                ? Convert.FromBase64String(text)
                : DecodeQuotedPrintable(text.Replace('_', ' '));
            return encoding.GetString(bytes);
        });
    }

    private static string DecodeBody(IReadOnlyDictionary<string, string> headers, string rawBody)
    {
        var charsetMatch = Charset.Match(headers.GetValueOrDefault("Content-Type", string.Empty));
        var encoding = Encoding.GetEncoding(charsetMatch.Success ? charsetMatch.Groups["name"].Value : "utf-8");

        // The server read the lines as Latin-1, so this recovers the bytes that were sent.
        var sent = Encoding.Latin1.GetBytes(rawBody);
        var transfer = headers.GetValueOrDefault("Content-Transfer-Encoding", "7bit").Trim().ToLowerInvariant();
        var bytes = transfer switch
        {
            "base64" => Convert.FromBase64String(string.Concat(rawBody.Where(c => !char.IsWhiteSpace(c)))),
            "quoted-printable" => DecodeQuotedPrintable(rawBody),
            _ => sent,
        };

        return encoding.GetString(bytes);
    }

    private static byte[] DecodeQuotedPrintable(string text)
    {
        var bytes = new List<byte>(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '=')
            {
                bytes.Add((byte)text[i]);
                continue;
            }

            // "=\r\n" is a soft line break; "=XX" is a byte.
            if (i + 1 < text.Length && text[i + 1] == '\r')
            {
                i += 2;
                continue;
            }

            if (i + 2 < text.Length && byte.TryParse(text.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            {
                bytes.Add(value);
                i += 2;
            }
            else
            {
                bytes.Add((byte)'=');
            }
        }

        return [.. bytes];
    }
}
