using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Channels;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.Middleware;
using MediatR;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Services;

/// <summary>
/// Enhanced session broadcast service with Redis Pub/Sub for multi-instance support,
/// connection pool limits, event buffering, selective broadcasting, and Last-Event-ID reconnection.
/// Issue #4764 - SSE Streaming Infrastructure + Session State Broadcasting
/// </summary>
public sealed class SessionBroadcastService : ISessionBroadcastService, IDisposable
{
    private readonly ConcurrentDictionary<Guid, SessionSubscriptionPool> _pools = new();
    private readonly ISubscriber? _subscriber;
    private readonly ILogger<SessionBroadcastService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly string _instanceId = Guid.NewGuid().ToString("N");

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
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        if (redis is { IsConnected: true })
        {
            _subscriber = redis.GetSubscriber();
            SubscribeToRedisChannels();
            _logger.LogInformation("SessionBroadcastService initialized with Redis Pub/Sub support");
        }
        else
        {
            _logger.LogInformation("SessionBroadcastService initialized in single-instance mode (no Redis)");
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

        // Replay buffered events if reconnecting with Last-Event-ID
        if (!string.IsNullOrEmpty(lastEventId))
        {
            var missedEvents = pool.GetEventsSince(lastEventId, userId);
            foreach (var evt in missedEvents)
            {
                yield return evt;
            }
            _logger.LogInformation(
                "Replayed {Count} missed events for session {SessionId} since {LastEventId}",
                missedEvents.Count, sessionId, LogValueSanitizer.Sanitize(lastEventId));
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
    public async Task PublishAsync(
        Guid sessionId,
        INotification evt,
        EventVisibility visibility = default,
        CancellationToken ct = default)
    {
        // Create envelope
        var envelope = new SseEventEnvelope
        {
            Id = $"{sessionId:N}-{DateTime.UtcNow.Ticks:x}",
            EventType = SseEventTypeMapper.GetEventType(evt),
            Data = evt,
            Timestamp = DateTime.UtcNow
        };

        // Publish to Redis for multi-instance (if available)
        if (_subscriber is not null)
        {
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
/// Redis Pub/Sub message format for cross-instance event delivery.
/// </summary>
internal record RedisEventMessage
{
    public Guid SessionId { get; init; }
    public required SseEventEnvelope Envelope { get; init; }
    public EventVisibility Visibility { get; init; }
    public string? OriginInstanceId { get; init; }
}
