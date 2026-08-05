using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Api.Observability;

namespace Api.SharedKernel.Infrastructure.Http;

/// <summary>
/// SSRF-safe connect pin (issue #3495, findings C1/C2/H4). Used as a
/// <see cref="SocketsHttpHandler.ConnectCallback"/> so that EVERY connection an egress
/// <c>HttpClient</c> makes — the initial request AND every auto-redirect hop — resolves the
/// host once, fails closed if any resolved address is private/reserved (per
/// <see cref="SsrfPolicy"/>), and dials the validated IP directly. Because the socket connects
/// to a fixed <see cref="IPEndPoint"/> (never the hostname), there is no second resolution:
/// DNS-rebinding and redirect-to-internal are closed by construction. The original host is left
/// on the request so TLS SNI / the Host header stay correct.
/// </summary>
internal static class SsrfPinnedConnect
{
    /// <summary>
    /// Resolves <paramref name="host"/> exactly once and returns the pinned validated address.
    /// Fails closed (throws) if resolution is empty or if ANY resolved address is blocked.
    /// </summary>
    internal static async Task<IPAddress> ResolveAndValidateAsync(
        IDnsResolver dns,
        string host,
        string sink,
        CancellationToken ct)
    {
        IReadOnlyList<IPAddress> addresses = await dns.ResolveAsync(host, ct).ConfigureAwait(false);

        if (addresses.Count == 0)
        {
            // Fail-closed (no address) — count as an SSRF block BEFORE throwing (bounded tags only,
            // the host is never a metric tag; it stays in the exception message).
            MeepleAiMetrics.RecordEgressBlocked(sink, MeepleAiMetrics.EgressBlockReasons.PrivateIp);
            throw new InvalidOperationException($"SSRF: host '{host}' did not resolve to any address");
        }

        IPAddress? pinned = null;
        foreach (IPAddress ip in addresses)
        {
            if (SsrfPolicy.IsBlocked(ip))
            {
                MeepleAiMetrics.RecordEgressBlocked(sink, MeepleAiMetrics.EgressBlockReasons.PrivateIp);
                throw new InvalidOperationException($"SSRF: host '{host}' resolves to blocked address {ip}");
            }

            pinned ??= ip;
        }

        // Validated a public address → count for the block-ratio denominator.
        MeepleAiMetrics.RecordEgressAllowed(sink);
        return pinned!;
    }

    /// <summary>
    /// Builds a <see cref="SocketsHttpHandler.ConnectCallback"/> that pins to the validated IP.
    /// </summary>
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> Create(
        IDnsResolver dns, string sink)
    {
        ArgumentNullException.ThrowIfNull(dns);

        return async (context, ct) =>
        {
            IPAddress pinned = await ResolveAndValidateAsync(dns, context.DnsEndPoint.Host, sink, ct)
                .ConfigureAwait(false);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(pinned, context.DnsEndPoint.Port), ct)
                    .ConfigureAwait(false);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        };
    }
}
