using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #2470 (DEC-C) — TTL'd counter of admin sessions that have recently
/// posted a heartbeat to the wikidata-dead-letters page. The alert
/// <c>WikidataSseSubscriberStarvation</c> consults this gauge to fire only
/// when an admin is actively waiting for live SSE updates — see
/// <c>infra/prometheus/alerts/wikidata-sse.yml</c>.
/// </summary>
/// <remarks>
/// <para>Heartbeat contract: the admin page posts every 30s while open.
/// Entries older than the TTL window (default 90s — 3 missed pings, see
/// <c>WikidataAdminClientHeartbeatTracker.TtlSeconds</c>) are evicted on the
/// next read. The eviction is lazy so the tracker has no background timer;
/// <see cref="GetConnectedCount(DateTime)"/> sweeps the dictionary
/// opportunistically.</para>
///
/// <para>Thread-safety: writes go through
/// <see cref="ConcurrentDictionary{TKey,TValue}.AddOrUpdate(TKey, TValue, Func{TKey, TValue, TValue})"/>;
/// reads iterate a snapshot of the keys (see comments in
/// <see cref="GetConnectedCount(DateTime)"/>) so a concurrent writer can never
/// crash the read.</para>
/// </remarks>
public interface IWikidataAdminClientHeartbeatTracker
{
    /// <summary>
    /// Records (or refreshes) the heartbeat for <paramref name="userId"/>.
    /// </summary>
    void RecordHeartbeat(Guid userId, DateTime utcNow);

    /// <summary>
    /// Returns the count of distinct user ids that have heartbeated within
    /// the TTL window relative to <paramref name="utcNow"/>. Performs lazy
    /// GC of expired entries.
    /// </summary>
    int GetConnectedCount(DateTime utcNow);
}

/// <inheritdoc cref="IWikidataAdminClientHeartbeatTracker"/>
internal sealed class WikidataAdminClientHeartbeatTracker : IWikidataAdminClientHeartbeatTracker
{
    /// <summary>
    /// TTL window in seconds. Three FE 30-second pings — anything older
    /// than this counts as "admin tab closed".
    /// </summary>
    public const int TtlSeconds = 90;

    private readonly ConcurrentDictionary<Guid, DateTime> _heartbeats = new();
    private readonly ILogger<WikidataAdminClientHeartbeatTracker> _logger;

    public WikidataAdminClientHeartbeatTracker(
        ILogger<WikidataAdminClientHeartbeatTracker> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void RecordHeartbeat(Guid userId, DateTime utcNow)
    {
        if (userId == Guid.Empty)
        {
            // Defensive: the admin-only filter should have rejected anonymous
            // requests upstream, but a malformed test or fuzzer should not
            // pollute the tracker.
            _logger.LogDebug("WikidataAdminClientHeartbeatTracker: ignored heartbeat with empty user id");
            return;
        }

        _heartbeats.AddOrUpdate(userId, utcNow, (_, _) => utcNow);
    }

    public int GetConnectedCount(DateTime utcNow)
    {
        if (_heartbeats.IsEmpty) return 0;

        var threshold = utcNow.AddSeconds(-TtlSeconds);

        // Snapshot the keys so a concurrent writer cannot mutate the
        // collection while we iterate. We tolerate the small amount of
        // staleness this introduces — a write that lands during iteration
        // will simply be counted on the next call.
        foreach (var (userId, lastBeat) in _heartbeats.ToArray())
        {
            if (lastBeat < threshold)
            {
                _heartbeats.TryRemove(new KeyValuePair<Guid, DateTime>(userId, lastBeat));
            }
        }

        return _heartbeats.Count;
    }
}
