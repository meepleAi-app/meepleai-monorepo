// Issue #2561 SP2 T12 — Native SSE stream observability (AC-OBS-1).
//
// Two metrics:
//   1. meepleai.live_session.sse.active_connections (ObservableGauge<int>)
//      — current number of open SSE connections to GET /api/v1/live-sessions/{id}/stream.
//        Incremented (+1) when a client's connection passes auth + headers-set,
//        decremented (−1) in the finally block when the connection tears down.
//        Uses Interlocked.Increment/Decrement for thread-safety across concurrent
//        connections (same pattern as WikidataDeadLetterCount / SetWikidataQueueDepth).
//
//   2. meepleai.live_session.sse.reconnect.total (Counter<long>)
//      — incremented once when the /stream request carries a non-empty lastEventId
//        query param, indicating the client is resuming after a drop (not a fresh
//        connect). Distinguishes intentional reconnects from new connections.
//
// Naming follows the dot-separated convention adopted by the LiveSession partial
// (meepleai.live_session.*) introduced in MeepleAiMetrics.LiveSession.cs (#2097).
// Cardinality: 1 gauge series + 1 counter series (no per-session label, per #614
// cardinality policy — high-cardinality session-level breakdown available in logs).
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    // ── Active-connections gauge ──────────────────────────────────────────────

    /// <summary>
    /// Backing store for <see cref="LiveSseActiveConnections"/>. Modified only
    /// via <see cref="Interlocked.Increment(ref int)"/> /
    /// <see cref="Interlocked.Decrement(ref int)"/> so reads on the scrape
    /// thread are ordered correctly on ARM64 (same pattern as
    /// <c>_wikidataDeadLetterCount</c>).
    /// </summary>
    private static int _liveSseActiveConnections;

    /// <summary>
    /// Current number of open SSE connections to
    /// <c>GET /api/v1/live-sessions/{id}/stream</c>.
    ///
    /// Increment via <see cref="IncrementLiveSseActiveConnections"/> after
    /// auth succeeds + SSE headers are set; decrement via
    /// <see cref="DecrementLiveSseActiveConnections"/> in the stream's
    /// <c>finally</c> block so every connect is matched by a disconnect.
    ///
    /// SLO guidance:
    ///   - Sustained value &gt; 10 × expected peak concurrent sessions →
    ///     connection leak or crash-loop reconnect storm; investigate client.
    ///   - Sustained 0 during an active game night window → upstream proxy
    ///     or auth middleware may be rejecting SSE connections.
    ///
    /// Cardinality: 1 gauge series (no per-session label — use structured logs
    /// for per-session breakdown per cardinality policy #614).
    /// </summary>
    public static readonly ObservableGauge<int> LiveSseActiveConnections = Meter.CreateObservableGauge(
        name: "meepleai.live_session.sse.active_connections",
        observeValue: () => System.Threading.Interlocked.CompareExchange(ref _liveSseActiveConnections, 0, 0),
        unit: "connections",
        description: "Current open SSE connections to GET /api/v1/live-sessions/{id}/stream (#2561 SP2 T12)");

    /// <summary>Atomically increments the active-connections gauge by 1. Call after SSE headers are flushed.</summary>
    public static void IncrementLiveSseActiveConnections() =>
        System.Threading.Interlocked.Increment(ref _liveSseActiveConnections);

    /// <summary>
    /// Atomically decrements the active-connections gauge by 1. Call in the stream's
    /// <c>finally</c> block so every connect is matched by a disconnect even if the
    /// streaming loop throws.
    /// </summary>
    public static void DecrementLiveSseActiveConnections() =>
        System.Threading.Interlocked.Decrement(ref _liveSseActiveConnections);

    /// <summary>
    /// Test-only: resets the active-connections gauge to a known value.
    /// Production code MUST NOT call this.
    /// </summary>
    internal static void ResetLiveSseActiveConnections(int value = 0) =>
        System.Threading.Interlocked.Exchange(ref _liveSseActiveConnections, value);

    // ── Reconnect counter ─────────────────────────────────────────────────────

    /// <summary>
    /// Total number of SSE reconnect requests received by
    /// <c>GET /api/v1/live-sessions/{id}/stream</c>.
    ///
    /// A reconnect is identified by the presence of a non-empty
    /// <c>lastEventId</c> query param (the client replays the browser
    /// <c>EventSource</c> <c>Last-Event-ID</c> header as a QS param in our
    /// implementation). Fresh connections with no <c>lastEventId</c> are
    /// NOT counted here; they appear only in the active-connections gauge
    /// increment.
    ///
    /// SLO guidance:
    ///   - Reconnect rate consistently &gt; 30% of connection rate →
    ///     abnormal drop / proxy timeout; investigate keep-alive config
    ///     or 30s heartbeat interval.
    ///   - Sudden spike in reconnects without a matching spike in new
    ///     connections → mass disconnect event (deploy, network hiccup);
    ///     correlate with <c>meepleai.live_session.sse.active_connections</c>.
    ///
    /// Cardinality: 1 counter series (no per-session label per #614 policy).
    /// </summary>
    public static readonly Counter<long> LiveSseReconnectTotal = Meter.CreateCounter<long>(
        name: "meepleai.live_session.sse.reconnect.total",
        unit: "reconnects",
        description: "Total SSE reconnect requests (lastEventId present) to /live-sessions/{id}/stream (#2561 SP2 T12)");

    /// <summary>
    /// Records a reconnect. Call once when the /stream request carries a
    /// non-empty <c>lastEventId</c> query param.
    /// </summary>
    public static void RecordLiveSseReconnect() =>
        LiveSseReconnectTotal.Add(1);
}
