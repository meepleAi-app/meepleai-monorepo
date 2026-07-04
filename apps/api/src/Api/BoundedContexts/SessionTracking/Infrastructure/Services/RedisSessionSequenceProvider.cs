using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Api.SharedKernel.Constants;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Monotonic sequence provider backed by Redis INCR with in-process fallback.
/// Issue #2561 SP2 T6.
/// </summary>
/// <remarks>
/// Redis path: INCR meepleai:session:seq:{sessionId:N} with session-aligned TTL (48h).
/// TTL rationale: no explicit live-game-session max exists in the codebase; 48h comfortably
/// exceeds the longest realistic board game session including breaks, ensuring the counter
/// cannot reset mid-session. TTL refreshes on each write so long-running sessions stay safe.
///
/// Redis-vs-fallback is decided ONCE at construction (mirrors <see cref="SessionBroadcastService"/>),
/// NOT per call. Issue #2563: a per-call IsConnected check let a mid-session Redis drop fall into the
/// in-process counter, which restarts at 0 and emits a seq below the Redis high-watermark —
/// permanently corrupting ZSET eviction and Last-Event-ID resume (ZRANGEBYSCORE never sees seq 1..N
/// again). With the ctor decision a session that started on Redis never silently downgrades to a
/// from-zero in-process counter: a mid-session Redis failure propagates so the caller fails the
/// publish and the durable Redis seq stays authoritative (the client re-reads it on reconnect).
///
/// Pure fallback path (Redis null/disconnected AT CONSTRUCTION): ConcurrentDictionary&lt;Guid, StrongBox&lt;long&gt;&gt;
/// + Interlocked.Increment. Counters are per-instance and not durable across restarts — acceptable
/// because this mode already implies single-instance (no cross-instance coordination). D20 format
/// is identical in both paths.
/// </remarks>
public sealed class RedisSessionSequenceProvider : ISessionSequenceProvider
{
    // TTL: 48h — see class XML doc for rationale.
    private static readonly TimeSpan SessionSeqTtl = TimeSpan.FromHours(48);

    // Redis-vs-fallback decided once at construction (Issue #2563). Non-null ⇒ Redis mode.
    private readonly IDatabase? _redisDb;

    // In-process fallback: StrongBox<long> lets Interlocked.Increment operate on the ref field.
    private readonly ConcurrentDictionary<Guid, StrongBox<long>> _fallbackCounters = new();

    public RedisSessionSequenceProvider(
        IConnectionMultiplexer? redis,
        ILogger<RedisSessionSequenceProvider> logger)
    {
        // NextAsync no longer logs (the per-call fallback try/catch was removed in #2563), so the
        // logger is used only here at construction — kept as a ctor-local, not a field (Sonar S1450).
        ArgumentNullException.ThrowIfNull(logger);

        // Decide the backing store ONCE, at construction. See class remarks (Issue #2563).
        _redisDb = redis is { IsConnected: true } ? redis.GetDatabase() : null;

        logger.LogInformation(
            _redisDb is not null
                ? "RedisSessionSequenceProvider initialized in Redis mode (durable monotonic seq)."
                : "RedisSessionSequenceProvider initialized in in-process fallback mode (single-instance).");
    }

    /// <inheritdoc />
    public async Task<long> NextAsync(Guid sessionId, CancellationToken ct)
    {
        if (_redisDb is not null)
        {
            // Redis mode (decided at ctor). On a mid-session Redis failure we deliberately do NOT
            // fall back to the in-process counter — restarting at 0 would break monotonicity
            // (Issue #2563). The exception propagates so the caller fails the publish; the durable
            // Redis seq stays authoritative and the client re-reads it on reconnect.
            var key = RedisKeyConstants.GetSessionSeqKey(sessionId);
            var seq = await _redisDb.StringIncrementAsync(key).ConfigureAwait(false);
            // Refresh TTL on every write so long-running sessions don't expire mid-game.
            await _redisDb.KeyExpireAsync(key, SessionSeqTtl).ConfigureAwait(false);
            return seq;
        }

        // In-process fallback (Redis absent/disconnected at construction). GetOrAdd is safe —
        // StrongBox is a reference type and Interlocked.Increment is atomic.
        var box = _fallbackCounters.GetOrAdd(sessionId, _ => new StrongBox<long>(0L));
        return Interlocked.Increment(ref box.Value);
    }
}
