using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Helpers;
using Api.Middleware;
using Api.SharedKernel.Constants;
using MediatR;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Enhanced session broadcast service with Redis Pub/Sub for multi-instance support,
/// connection pool limits, event buffering, selective broadcasting, and Last-Event-ID reconnection.
/// Issue #4764 - SSE Streaming Infrastructure + Session State Broadcasting
/// Issue #2561 SP2 T7: Durable cross-instance replay via Redis Sorted Set.
/// </summary>
public sealed class SessionBroadcastService : ISessionBroadcastService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionSubscriptionPool> _pools = new();
    private readonly ISubscriber? _subscriber;
    private readonly IDatabase? _redisDb;
    private readonly ISessionSequenceProvider _seq;
    private readonly ILogger<SessionBroadcastService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Lua script for atomically writing to the replay ZSET, capping at N entries, and refreshing TTL.
    /// KEYS[1] = replay key, ARGV[1] = score (seq), ARGV[2] = member (json), ARGV[3] = cap, ARGV[4] = ttl (seconds).
    /// Uses ZREMRANGEBYRANK to keep only the newest cap entries after each ZADD.
    /// Issue #2561 SP2 T7.
    /// </summary>
    private static readonly string ReplayZaddCapScript = @"
        local key    = KEYS[1]
        local score  = tonumber(ARGV[1])
        local member = ARGV[2]
        local cap    = tonumber(ARGV[3])
        local ttl    = tonumber(ARGV[4])
        redis.call('ZADD', key, score, member)
        redis.call('ZREMRANGEBYRANK', key, 0, -(cap + 1))
        redis.call('EXPIRE', key, ttl)
        return 1
    ";

    // TTL for replay ZSET: same 48h as the sequence counter (session-aligned; refreshed on each write).
    private static readonly TimeSpan ReplayTtl = TimeSpan.FromHours(48);

    /// <summary>Maximum concurrent SSE connections per session.</summary>
    public const int MaxConnectionsPerSession = 20;

    /// <summary>Maximum events per second per session (burst protection).</summary>
    public const int MaxEventsPerSecond = 10;

    /// <summary>Number of recent events to buffer for Last-Event-ID reconnection.</summary>
    public const int EventBufferSize = 100;

    /// <summary>Redis channel prefix for session events.</summary>
    private const string RedisChannelPrefix = "meepleai:session:events:";

    public SessionBroadcastService(
        ILogger<SessionBroadcastService> logger,
        ISessionSequenceProvider seq,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _seq = seq ?? throw new ArgumentNullException(nameof(seq));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (redis is { IsConnected: true })
        {
            _subscriber = redis.GetSubscriber();
            _redisDb = redis.GetDatabase();
            SubscribeToRedisChannels();
            _logger.LogInformation(
                "SessionBroadcastService initialized with Redis Pub/Sub and durable replay buffer (T7)");
        }
        else
        {
            // WARNING: multi-instance deployments without Redis lose cross-instance SSE replay.
            // Each instance maintains its own in-memory CircularEventBuffer; a client reconnecting
            // to a different instance will fall back to full-buffer replay (or empty if the new
            // instance never saw the session). Redis is REQUIRED for correct cross-instance replay.
            _logger.LogWarning(
                "SessionBroadcastService initialized WITHOUT Redis. In-memory replay buffer active. " +
                "Multi-instance deployments will have siloed replay buffers — cross-instance " +
                "Last-Event-ID reconnection is NOT guaranteed. Provide IConnectionMultiplexer for " +
                "durable cross-instance SSE replay (Issue #2561 SP2 T7).");
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<SseEventEnvelope> SubscribeAsync(
        Guid sessionId,
        Guid userId,
        string? lastEventId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var pool = _pools.GetOrAdd(sessionId, _ => new SessionSubscriptionPool(EventBufferSize));

        // Create subscriber channel
        var channel = Channel.CreateBounded<SseEventEnvelope>(new BoundedChannelOptions(200)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

        var subscription = new SessionSubscription(userId, channel.Writer);

        // Atomic check-and-add to prevent TOCTOU race on connection limit
        if (!pool.TryAdd(subscription))
        {
            _logger.LogWarning(
                "Connection limit ({Max}) reached for session {SessionId}. Rejecting subscriber.",
                MaxConnectionsPerSession, sessionId);
            yield break;
        }

        _logger.LogInformation(
            "SSE subscriber added for session {SessionId}, user {UserId}. Active: {Count}/{Max}",
            sessionId, userId, pool.ConnectionCount, MaxConnectionsPerSession);

        // Replay missed events if reconnecting with Last-Event-ID.
        // T7: Redis present → durable cross-instance ZSET replay (ZRANGEBYSCORE exclusive lower bound).
        //     Redis absent → in-memory CircularEventBuffer fallback (single-instance only).
        if (!string.IsNullOrEmpty(lastEventId))
        {
            IReadOnlyList<SseEventEnvelope> missedEvents;

            if (_redisDb is not null && long.TryParse(lastEventId, NumberStyles.None, CultureInfo.InvariantCulture, out var sinceSeq))
            {
                // Redis path: fetch all events with seq > sinceSeq (exclusive lower bound via "(" prefix).
                missedEvents = await GetReplayFromRedisAsync(sessionId, sinceSeq, userId, lastEventId).ConfigureAwait(false);
            }
            else
            {
                // Fallback: in-memory CircularEventBuffer (also handles legacy/garbage lastEventId when
                // TryParse fails — returns empty list for unrecognised ids, matching existing behaviour).
                missedEvents = pool.GetEventsSince(lastEventId, userId);
            }

            foreach (var evt in missedEvents)
            {
                yield return evt;
            }

            _logger.LogInformation(
                "Replayed {Count} missed events for session {SessionId} since {LastEventId} (path: {Path})",
                missedEvents.Count, sessionId, LogSanitizer.Sanitize(lastEventId),
                _redisDb is not null ? "redis-zset" : "in-memory");
        }

        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return evt;
            }
        }
        finally
        {
            pool.Remove(subscription);
            channel.Writer.TryComplete();

            _logger.LogInformation(
                "SSE subscriber disconnected from session {SessionId}, user {UserId}. Active: {Count}",
                sessionId, userId, pool.ConnectionCount);

            // Clean up empty pools
            if (pool.ConnectionCount == 0)
            {
                _pools.TryRemove(sessionId, out _);
            }
        }
    }

    /// <inheritdoc />
    public Task PublishAsync(
        Guid sessionId,
        INotification evt,
        EventVisibility visibility = default,
        CancellationToken ct = default)
    {
        // Build the envelope, deriving EventType from the domain event via the mapper.
        // Id is intentionally left empty here: PublishEnvelopeAsync is the single
        // id-assignment chokepoint (Issue #2561 SP2 T6 — monotonic sequence).
        var envelope = new SseEventEnvelope
        {
            Id = string.Empty,
            EventType = SseEventTypeMapper.GetEventType(evt),
            Data = evt,
            Timestamp = DateTime.UtcNow
        };

        // Delegate to the envelope-based path (which assigns the real monotonic Id).
        return PublishEnvelopeAsync(sessionId, envelope, visibility, ct);
    }

    /// <inheritdoc />
    public async Task PublishEnvelopeAsync(
        Guid sessionId,
        SseEventEnvelope envelope,
        EventVisibility visibility = default,
        CancellationToken ct = default)
    {
        // Single id-assignment chokepoint: monotonic per-session D20 sequence (incoming Id is ignored).
        // Issue #2561 SP2 T6: replaces the Ticks-based "{sessionId:N}-{Ticks:x}" format with a
        // Redis-INCR-backed (in-process fallback) monotonic counter. D20 round-trips exactly for
        // CircularEventBuffer.GetSince's ordinal match; legacy ids reconnecting after deploy simply
        // miss → existing full-replay path → FE dedups via seenIds.
        var seq = await _seq.NextAsync(sessionId, ct).ConfigureAwait(false);
        envelope = envelope with { Id = seq.ToString("D20", CultureInfo.InvariantCulture) };

        // Publish to Redis for multi-instance (if available)
        if (_subscriber is not null)
        {
            // T7: Write to durable replay ZSET before Pub/Sub so reconnecting clients always
            // find the event even if they reconnect after the Pub/Sub message is gone.
            await AppendToRedisReplayAsync(sessionId, envelope, visibility, ct).ConfigureAwait(false);
            await PublishToRedisAsync(sessionId, envelope, visibility, ct).ConfigureAwait(false);
        }

        // Publish locally
        PublishLocally(sessionId, envelope, visibility);
    }

    /// <inheritdoc />
    public int GetConnectionCount(Guid sessionId)
    {
        return _pools.TryGetValue(sessionId, out var pool) ? pool.ConnectionCount : 0;
    }

    /// <inheritdoc />
    public async Task DisconnectAllAsync(Guid sessionId, CancellationToken ct = default)
    {
        if (_pools.TryRemove(sessionId, out var pool))
        {
            pool.CompleteAll();
            _logger.LogInformation("Disconnected all subscribers from session {SessionId}", sessionId);
        }

        // Notify other instances via Redis
        if (_subscriber is not null)
        {
            var channel = new RedisChannel($"{RedisChannelPrefix}disconnect", RedisChannel.PatternMode.Literal);
            await _subscriber.PublishAsync(channel, sessionId.ToString()).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Atomically appends an envelope to the Redis replay ZSET, evicts the oldest entries
    /// beyond the cap, and refreshes the TTL — all in a single Lua script round-trip.
    /// Visibility metadata is encoded in the ZSET member (as a <see cref="ReplayEntry"/> wrapper)
    /// so that <see cref="GetReplayFromRedisAsync"/> can filter private events at read time.
    /// Issue #2561 SP2 T7.
    /// </summary>
    private async Task AppendToRedisReplayAsync(Guid sessionId, SseEventEnvelope envelope, EventVisibility visibility, CancellationToken ct)
    {
        try
        {
            var seq = long.Parse(envelope.Id, CultureInfo.InvariantCulture);
            var key = (RedisKey)RedisKeyConstants.GetSessionReplayKey(sessionId);
            var entry = new ReplayEntry(envelope, visibility.IsPublic, visibility.TargetUserId);
            var member = JsonSerializer.Serialize(entry, _jsonOptions);

            await _redisDb!.ScriptEvaluateAsync(
                ReplayZaddCapScript,
                keys: [key],
                values: [(RedisValue)seq, (RedisValue)member, (RedisValue)EventBufferSize, (RedisValue)(long)ReplayTtl.TotalSeconds]
            ).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to append event {EventId} to Redis replay ZSET for session {SessionId}. " +
                "Continuing — in-memory buffer still available for same-instance replay.",
                envelope.Id, sessionId);
        }
    }

    /// <summary>
    /// Reads events from the Redis replay ZSET with sequence &gt; sinceSeq (exclusive lower bound)
    /// and applies the SAME per-subscriber visibility filter as <see cref="CircularEventBuffer.GetSince"/>:
    /// an event is skipped only when it is private (<c>!IsPublic</c>) AND addressed to a specific user
    /// (<c>TargetUserId.HasValue</c>) who is NOT the current subscriber. Events with a null
    /// <c>TargetUserId</c> are broadcast-to-all (the codebase convention) and are always delivered.
    /// Visibility is encoded in every ZSET member by <see cref="AppendToRedisReplayAsync"/>,
    /// so private events are never leaked to a different subscriber during cross-instance replay.
    /// Issue #2561 SP2 T7.
    /// </summary>
    private async Task<IReadOnlyList<SseEventEnvelope>> GetReplayFromRedisAsync(
        Guid sessionId, long sinceSeq, Guid userId, string originalLastEventId)
    {
        try
        {
            var key = (RedisKey)RedisKeyConstants.GetSessionReplayKey(sessionId);
            // Exclusive lower bound: "(" prefix means strictly greater than sinceSeq.
            var entries = await _redisDb!.SortedSetRangeByScoreAsync(
                key,
                start: sinceSeq,
                stop: double.PositiveInfinity,
                exclude: Exclude.Start,
                order: Order.Ascending,
                skip: 0,
                take: EventBufferSize
            ).ConfigureAwait(false);

            var result = new List<SseEventEnvelope>(entries.Length);
            foreach (var entry in entries)
            {
                if (entry.IsNullOrEmpty) continue;
                try
                {
                    var replayEntry = JsonSerializer.Deserialize<ReplayEntry>(entry!, _jsonOptions);
                    if (replayEntry is null) continue;

                    // Visibility filter — EXACT mirror of CircularEventBuffer.GetSince (:671):
                    // skip only a private event (!IsPublic) addressed to a specific user
                    // (TargetUserId.HasValue) other than the current subscriber. A null TargetUserId
                    // means broadcast-to-all (codebase convention), so those events are always delivered.
                    if (!replayEntry.IsPublic && replayEntry.TargetUserId.HasValue && replayEntry.TargetUserId.Value != userId)
                    {
                        continue;
                    }

                    result.Add(replayEntry.Envelope);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to deserialize replay entry from Redis ZSET for session {SessionId}. Skipping.",
                        sessionId);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Redis replay ZSET read failed for session {SessionId}. Falling back to in-memory buffer.",
                sessionId);

            // Graceful degradation: fall back to in-memory CircularEventBuffer using the
            // original lastEventId string (avoids re-formatting sinceSeq and breaking any
            // non-D20-canonical ids that were passed through from the client).
            if (_pools.TryGetValue(sessionId, out var pool))
            {
                return pool.GetEventsSince(originalLastEventId, userId);
            }

            return Array.Empty<SseEventEnvelope>();
        }
    }

    private void PublishLocally(Guid sessionId, SseEventEnvelope envelope, EventVisibility visibility)
    {
        if (!_pools.TryGetValue(sessionId, out var pool))
        {
            _logger.LogDebug("No local subscribers for session {SessionId}", sessionId);
            return;
        }

        // Apply burst protection
        if (!pool.TryAcquireEventSlot())
        {
            _logger.LogWarning(
                "Event rate limit ({Max}/s) exceeded for session {SessionId}. Dropping event {EventType}.",
                MaxEventsPerSecond, sessionId, envelope.EventType);
            return;
        }

        // Buffer for reconnection
        pool.BufferEvent(envelope, visibility);

        // Broadcast to local subscribers
        var deliveredCount = 0;
        foreach (var sub in pool.GetSubscriptions())
        {
            // Apply visibility filter
            if (!visibility.IsPublic && visibility.TargetUserId.HasValue && sub.UserId != visibility.TargetUserId.Value)
            {
                continue;
            }

            if (sub.Writer.TryWrite(envelope))
            {
                deliveredCount++;
            }
        }

        _logger.LogDebug(
            "Published {EventType} to {Count} local subscribers for session {SessionId}",
            envelope.EventType, deliveredCount, sessionId);
    }

    private async Task PublishToRedisAsync(
        Guid sessionId,
        SseEventEnvelope envelope,
        EventVisibility visibility,
        CancellationToken ct)
    {
        try
        {
            var message = new RedisEventMessage
            {
                SessionId = sessionId,
                Envelope = envelope,
                Visibility = visibility,
                OriginInstanceId = _instanceId
            };

            var json = JsonSerializer.Serialize(message, _jsonOptions);
            var channel = new RedisChannel($"{RedisChannelPrefix}{sessionId}", RedisChannel.PatternMode.Literal);
            await _subscriber!.PublishAsync(channel, json).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish event to Redis for session {SessionId}. Local delivery only.", sessionId);
        }
    }

    private void SubscribeToRedisChannels()
    {
        // Subscribe to all session channels using pattern
        var pattern = new RedisChannel($"{RedisChannelPrefix}*", RedisChannel.PatternMode.Pattern);
        _subscriber!.Subscribe(pattern, (channel, message) =>
        {
            try
            {
                if (message.IsNullOrEmpty) return;

                var channelStr = channel.ToString();

                // Handle disconnect commands
                if (channelStr.EndsWith("disconnect", StringComparison.Ordinal))
                {
                    if (Guid.TryParse(message!, out var disconnectSessionId))
                    {
                        if (_pools.TryRemove(disconnectSessionId, out var pool))
                        {
                            pool.CompleteAll();
                        }
                    }
                    return;
                }

                // Handle normal event messages from other instances only
                var redisMessage = JsonSerializer.Deserialize<RedisEventMessage>(message!, _jsonOptions);
                if (redisMessage is not null)
                {
                    // Skip events from this instance (already delivered locally in PublishAsync)
                    if (string.Equals(redisMessage.OriginInstanceId, _instanceId, StringComparison.Ordinal))
                    {
                        return;
                    }

                    PublishLocally(redisMessage.SessionId, redisMessage.Envelope, redisMessage.Visibility);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing Redis message on channel {Channel}", channel);
            }
        });

        _logger.LogInformation("Subscribed to Redis pattern: {Pattern}", $"{RedisChannelPrefix}*");
    }

    public void Dispose()
    {
        foreach (var pool in _pools.Values)
        {
            pool.CompleteAll();
        }
        _pools.Clear();
    }
}

/// <summary>
/// Manages a pool of subscriptions for a single session with connection limits,
/// event buffering, and rate limiting.
/// </summary>
internal sealed class SessionSubscriptionPool
{
    private readonly List<SessionSubscription> _subscriptions = [];
    private readonly Lock _subscriptionLock = new();
    private readonly Lock _rateLimitLock = new();
    private readonly CircularEventBuffer _eventBuffer;
    private int _eventCount;
    private long _lastSecondTicks;

    public SessionSubscriptionPool(int bufferSize)
    {
        _eventBuffer = new CircularEventBuffer(bufferSize);
    }

    public int ConnectionCount
    {
        get
        {
            lock (_subscriptionLock)
            {
                return _subscriptions.Count(s => !s.IsCompleted);
            }
        }
    }

    /// <summary>
    /// Atomically checks the connection limit and adds the subscription.
    /// Returns false if the pool is full.
    /// </summary>
    public bool TryAdd(SessionSubscription subscription)
    {
        lock (_subscriptionLock)
        {
            if (_subscriptions.Count(s => !s.IsCompleted) >= SessionBroadcastService.MaxConnectionsPerSession)
            {
                return false;
            }
            _subscriptions.Add(subscription);
            return true;
        }
    }

    public void Remove(SessionSubscription subscription)
    {
        subscription.Complete();
        lock (_subscriptionLock)
        {
            _subscriptions.Remove(subscription);
        }
    }

    public IReadOnlyList<SessionSubscription> GetSubscriptions()
    {
        lock (_subscriptionLock)
        {
            return _subscriptions.Where(s => !s.IsCompleted).ToList();
        }
    }

    /// <summary>
    /// Rate limiter: allows max N events per second.
    /// Uses lock for correctness (low contention per-session).
    /// </summary>
    public bool TryAcquireEventSlot()
    {
        lock (_rateLimitLock)
        {
            var nowSecond = DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond;
            if (nowSecond != _lastSecondTicks)
            {
                _lastSecondTicks = nowSecond;
                _eventCount = 1;
                return true;
            }
            return ++_eventCount <= SessionBroadcastService.MaxEventsPerSecond;
        }
    }

    public void BufferEvent(SseEventEnvelope envelope, EventVisibility visibility)
    {
        _eventBuffer.Add(new BufferedEvent(envelope, visibility));
    }

    public IReadOnlyList<SseEventEnvelope> GetEventsSince(string lastEventId, Guid userId)
    {
        return _eventBuffer.GetSince(lastEventId, userId);
    }

    public void CompleteAll()
    {
        lock (_subscriptionLock)
        {
            foreach (var sub in _subscriptions)
            {
                sub.Complete();
            }
        }
    }
}

/// <summary>
/// Represents a single SSE subscriber connection.
/// Thread-safe completion via Interlocked.
/// </summary>
internal sealed class SessionSubscription
{
    private int _completed;

    public Guid UserId { get; }
    public ChannelWriter<SseEventEnvelope> Writer { get; }
    public bool IsCompleted => Volatile.Read(ref _completed) == 1;

    public SessionSubscription(Guid userId, ChannelWriter<SseEventEnvelope> writer)
    {
        UserId = userId;
        Writer = writer;
    }

    public void Complete()
    {
        if (Interlocked.CompareExchange(ref _completed, 1, 0) == 0)
        {
            Writer.TryComplete();
        }
    }
}

/// <summary>
/// Circular buffer for recent events to support Last-Event-ID reconnection.
/// Thread-safe for concurrent reads and writes.
/// </summary>
internal sealed class CircularEventBuffer
{
    private readonly BufferedEvent[] _buffer;
    private readonly int _capacity;
    private long _writeIndex;
    private long _count;
    private readonly Lock _lock = new();

    public CircularEventBuffer(int capacity)
    {
        _capacity = capacity;
        _buffer = new BufferedEvent[capacity];
    }

    public void Add(BufferedEvent item)
    {
        lock (_lock)
        {
            _buffer[(int)(_writeIndex % _capacity)] = item;
            _writeIndex++;
            if (_count < _capacity) _count++;
        }
    }

    public IReadOnlyList<SseEventEnvelope> GetSince(string lastEventId, Guid userId)
    {
        lock (_lock)
        {
            var result = new List<SseEventEnvelope>();
            var found = false;
            var startIdx = _writeIndex >= _capacity ? _writeIndex - _capacity : 0L;

            for (var i = startIdx; i < _writeIndex; i++)
            {
                var item = _buffer[(int)(i % _capacity)];
                if (item.Envelope is null) continue;

                if (!found)
                {
                    if (string.Equals(item.Envelope.Id, lastEventId, StringComparison.Ordinal))
                    {
                        found = true;
                    }
                    continue;
                }

                // Apply visibility filter
                if (!item.Visibility.IsPublic && item.Visibility.TargetUserId.HasValue && item.Visibility.TargetUserId.Value != userId)
                {
                    continue;
                }

                result.Add(item.Envelope);
            }

            return result;
        }
    }
}

[StructLayout(LayoutKind.Auto)]
internal record struct BufferedEvent(SseEventEnvelope Envelope, EventVisibility Visibility);

/// <summary>
/// ZSET member format for the durable Redis replay buffer (Issue #2561 SP2 T7).
/// Wraps the event envelope together with its visibility metadata so that
/// <see cref="SessionBroadcastService.GetReplayFromRedisAsync"/> can enforce the same
/// per-subscriber filter as <see cref="CircularEventBuffer.GetSince"/> at read time.
/// </summary>
/// <param name="Envelope">The event envelope to replay.</param>
/// <param name="IsPublic"><c>true</c> when the event is visible to all subscribers.</param>
/// <param name="TargetUserId">
/// When <see cref="IsPublic"/> is <c>false</c>, the only subscriber that may receive this event.
/// </param>
internal record ReplayEntry(SseEventEnvelope Envelope, bool IsPublic, Guid? TargetUserId);

/// <summary>
/// Redis Pub/Sub message format for cross-instance event delivery.
/// </summary>
internal record RedisEventMessage
{
    public Guid SessionId { get; init; }
    public required SseEventEnvelope Envelope { get; init; }
    public EventVisibility Visibility { get; init; }
    public string? OriginInstanceId { get; init; }
}
