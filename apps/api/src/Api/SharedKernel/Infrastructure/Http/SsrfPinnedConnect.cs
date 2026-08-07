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
        IReadOnlyList<IPAddress> addresses;
        try
        {
            addresses = await dns.ResolveAsync(host, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // #3583 — l'esito era già fail-closed (la connessione non avviene), ma senza questo
            // counter un sink che degrada per problemi DNS è indistinguibile da un sink inattivo su
            // meepleai_egress_blocked_total. Registra e RILANCIA: nessun esito cambia.
            //
            // Il filtro esclude la cancellazione iniziata dal chiamante — che non è un guasto di
            // egress — pur continuando a contare un timeout interno del resolver (che si presenta
            // come OperationCanceledException con un CTS proprio, quindi con `ct` non cancellato).
            //
            // Due limiti noti e accettati:
            //   - se il chiamante cancella e il resolver risponde con SocketException invece che
            //     OperationCanceledException, il caso viene contato come dns_failure;
            //   - questo guard gira dentro il ConnectCallback, quindi UNA VOLTA PER CONNESSIONE:
            //     ogni hop di redirect e ogni nuova connessione del pool incrementa. Il counter va
            //     letto come rate di fallimenti di dial, MAI come "richieste utente fallite".
            MeepleAiMetrics.RecordEgressBlocked(sink, MeepleAiMetrics.EgressBlockReasons.DnsFailure);
            throw;
        }

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
    /// #3495 M3 (Slice F) — defence in depth for FIXED-HOST sinks: the connection host must match one
    /// of <paramref name="allowedHostSuffixes"/> (exact or sub-domain). The IP deny-list answers
    /// "is this address internal?"; the allow-list answers "is this even a host we talk to?", which
    /// is what stops a compromised upstream from redirecting us to an arbitrary PUBLIC host.
    /// <para>
    /// Checked BEFORE resolution, and on every connection — so each auto-redirect hop is covered too.
    /// The arbitrary-URL manual sink deliberately passes no allow-list: fetching an admin-supplied URL
    /// is its purpose, and there the deny-list plus the pin are the boundary.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">The host is outside the sink's allow-list.</exception>
    internal static void ValidateHostAllowed(string host, string sink, IReadOnlyCollection<string>? allowedHostSuffixes)
    {
        if (allowedHostSuffixes is null || allowedHostSuffixes.Count == 0)
        {
            return;
        }

        if (!HostSuffixMatcher.Matches(host, allowedHostSuffixes))
        {
            MeepleAiMetrics.RecordEgressBlocked(sink, MeepleAiMetrics.EgressBlockReasons.HostNotAllowed);
            throw new InvalidOperationException($"SSRF: host '{host}' is not allowed for sink '{sink}'");
        }
    }

    /// <summary>
    /// Builds a <see cref="SocketsHttpConnectionContext"/> callback that enforces the optional host
    /// allow-list and then pins to the validated IP.
    /// </summary>
    public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> Create(
        IDnsResolver dns, string sink, IReadOnlyCollection<string>? allowedHostSuffixes = null)
    {
        ArgumentNullException.ThrowIfNull(dns);

        return async (context, ct) =>
        {
            // Cheapest gate first: an off-list host is refused without even resolving it.
            ValidateHostAllowed(context.DnsEndPoint.Host, sink, allowedHostSuffixes);

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
