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
/// Fallback path (Redis null/disconnected): ConcurrentDictionary&lt;Guid, StrongBox&lt;long&gt;&gt;
/// + Interlocked.Increment. In fallback mode, counters are per-instance and not durable
/// across restarts — acceptable because fallback mode already implies single-instance
/// (no cross-instance coordination). D20 format is identical in both paths.
/// </remarks>
public sealed class RedisSessionSequenceProvider : ISessionSequenceProvider
{
    // TTL: 48h — see class XML doc for rationale.
    private static readonly TimeSpan SessionSeqTtl = TimeSpan.FromHours(48);

    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisSessionSequenceProvider> _logger;

    // In-process fallback: StrongBox<long> lets Interlocked.Increment operate on the ref field.
    private readonly ConcurrentDictionary<Guid, StrongBox<long>> _fallbackCounters = new();

    public RedisSessionSequenceProvider(
        IConnectionMultiplexer? redis,
        ILogger<RedisSessionSequenceProvider> logger)
    {
        _redis = redis;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<long> NextAsync(Guid sessionId, CancellationToken ct)
    {
        if (_redis is { IsConnected: true })
        {
            try
            {
                var db = _redis.GetDatabase();
                var key = RedisKeyConstants.GetSessionSeqKey(sessionId);
                var seq = await db.StringIncrementAsync(key).ConfigureAwait(false);
                // Refresh TTL on every write so long-running sessions don't expire mid-game.
                await db.KeyExpireAsync(key, SessionSeqTtl).ConfigureAwait(false);
                return seq;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Redis INCR failed for session {SessionId}; falling back to in-process counter.", sessionId);
            }
        }

        // In-process fallback: GetOrAdd is safe — StrongBox is reference type, Interlocked.Increment is atomic.
        var box = _fallbackCounters.GetOrAdd(sessionId, _ => new StrongBox<long>(0L));
        return Interlocked.Increment(ref box.Value);
    }
}
