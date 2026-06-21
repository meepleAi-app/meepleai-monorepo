using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #2256 — Redis pub/sub backplane for multi-pod fan-out of the F4 SSE
/// stream. Mirrors <see cref="InMemoryWikidataEnrichmentEventBroadcaster"/>'s
/// public surface so a single env-var flip (<c>WIKIDATA_SSE_BACKPLANE=redis</c>)
/// switches the runtime topology without touching the publisher (M9 scheduler /
/// M12 admin trigger) or the subscriber (the F4 SSE endpoint).
/// </summary>
/// <remarks>
/// <para><b>Topology</b>: publish writes a single Redis <c>PUBLISH</c> on the
/// channel <see cref="ChannelName"/>; every pod that has at least one local
/// subscriber holds an open <c>SUBSCRIBE</c> against the same channel via
/// <see cref="StackExchange.Redis"/> and pumps each incoming message into the
/// bounded per-subscriber <see cref="Channel{T}"/> registry — exactly the same
/// channel infrastructure the in-memory variant uses. The SSE endpoint sees no
/// difference between the two backplanes.</para>
///
/// <para><b>Delivery semantics</b>: at-most-once, best-effort — identical to the
/// in-memory variant. Redis pub/sub itself is fire-and-forget with no
/// persistence; the dead-letter table remains the source of truth.</para>
///
/// <para><b>Reconnect</b>: handled transparently by
/// <see cref="IConnectionMultiplexer"/>. We open a SUBSCRIBE on first subscriber
/// arrival and close it on last subscriber departure. If the connection drops
/// mid-flight, StackExchange.Redis auto-reconnects and re-arms the SUBSCRIBE
/// (per its own reconnection contract).</para>
///
/// <para><b>Bounded channel</b>: each local SSE subscriber gets its own
/// <see cref="Channel{T}"/> with <see cref="BoundedChannelFullMode.DropOldest"/>
/// — slow SSE clients can never back-pressure the Redis pump (which would
/// otherwise stall the channel queue and starve other subscribers on the same
/// pod).</para>
/// </remarks>
internal sealed class RedisWikidataEnrichmentEventBroadcaster
    : IWikidataEnrichmentEventBroadcaster, IDisposable
{
    /// <summary>
    /// Redis pub/sub channel name. Public const so a future ops dashboard can
    /// subscribe directly with <c>redis-cli SUBSCRIBE</c> for live debugging.
    /// </summary>
    public const string ChannelName = "wikidata-enrichment:attempt-recorded";

    private const int PerSubscriberCapacity = 128;

    // System.Text.Json with web defaults — matches the SSE endpoint's wire
    // serialisation (AdminWikidataCoverEnrichmentEndpoints uses the same).
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly RedisChannel RedisChannelLiteral =
        RedisChannel.Literal(ChannelName);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisWikidataEnrichmentEventBroadcaster> _logger;
    private readonly ConcurrentDictionary<Guid, Channel<WikidataEnrichmentEvent>> _subscribers = new();

    // Single SUBSCRIBE shared across all local subscribers — opened on first
    // arrival, released on last departure. The lock guards the open/close
    // transition only; the steady-state pump uses the lock-free
    // ConcurrentDictionary above for fan-out.
    private readonly Lock _subscriptionGate = new();
    private ChannelMessageQueue? _redisQueue;
    private bool _disposed;

    public RedisWikidataEnrichmentEventBroadcaster(
        IConnectionMultiplexer redis,
        ILogger<RedisWikidataEnrichmentEventBroadcaster> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public int SubscriberCount => _subscribers.Count;

    public void Publish(WikidataEnrichmentEvent payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var json = JsonSerializer.Serialize(payload, SerializerOptions);

        try
        {
            // Fire-and-forget — same delivery contract as the in-memory
            // variant. We intentionally do NOT await PublishAsync: the M9
            // scheduler tick budget is 1 minute and a stuck publisher would
            // collapse it. StackExchange.Redis's IConnectionMultiplexer
            // queues the PUBLISH on its own internal pipeline.
            _redis.GetSubscriber().Publish(RedisChannelLiteral, json, CommandFlags.FireAndForget);
        }
#pragma warning disable CA1031 // Fire-and-forget pub/sub must never throw into the caller.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Log + swallow — identical to the in-memory variant's "publisher
            // never throws" contract. The dead-letter table still has the row.
            _logger.LogWarning(
                ex,
                "RedisWikidataEnrichmentEventBroadcaster: PUBLISH failed for attempt {AttemptId} on channel {Channel}",
                payload.AttemptId,
                ChannelName);
        }
    }

    public async IAsyncEnumerable<WikidataEnrichmentEvent> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateBounded<WikidataEnrichmentEvent>(
            new BoundedChannelOptions(PerSubscriberCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            });

        _subscribers.TryAdd(subscriberId, channel);
        await EnsureRedisSubscriptionAsync().ConfigureAwait(false);

        _logger.LogDebug(
            "RedisWikidataEnrichmentEventBroadcaster: subscriber {SubscriberId} added (now {Total})",
            subscriberId, _subscribers.Count);

        try
        {
            // Mirror of InMemoryWikidataEnrichmentEventBroadcaster's loop —
            // see comments there re: `yield break` from a nested catch.
            while (!cancellationToken.IsCancellationRequested)
            {
                WikidataEnrichmentEvent? payload = null;
                var shouldBreak = false;
                try
                {
                    payload = await channel.Reader
                        .ReadAsync(cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    shouldBreak = true;
                }
                catch (ChannelClosedException)
                {
                    shouldBreak = true;
                }

                if (shouldBreak) break;
                yield return payload!;
            }
        }
        finally
        {
            _subscribers.TryRemove(subscriberId, out _);
            channel.Writer.TryComplete();
            await MaybeReleaseRedisSubscriptionAsync().ConfigureAwait(false);

            _logger.LogDebug(
                "RedisWikidataEnrichmentEventBroadcaster: subscriber {SubscriberId} removed (now {Total})",
                subscriberId, _subscribers.Count);
        }
    }

    /// <summary>
    /// Lazily opens the Redis SUBSCRIBE on first subscriber arrival.
    /// Idempotent: subsequent subscribers reuse the existing queue.
    /// </summary>
    private async Task EnsureRedisSubscriptionAsync()
    {
        // Fast path: the SUBSCRIBE is already open — no lock needed.
        if (_redisQueue is not null) return;

        ChannelMessageQueue? newQueue = null;
        var shouldRegister = false;

        lock (_subscriptionGate)
        {
            if (_redisQueue is null)
            {
                shouldRegister = true;
            }
        }

        if (!shouldRegister) return;

        try
        {
            newQueue = await _redis.GetSubscriber()
                .SubscribeAsync(RedisChannelLiteral)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "RedisWikidataEnrichmentEventBroadcaster: failed to open SUBSCRIBE on channel {Channel}",
                ChannelName);
            throw;
        }

        var commit = false;
        lock (_subscriptionGate)
        {
            if (_redisQueue is null)
            {
                _redisQueue = newQueue;
                commit = true;
            }
        }

        if (commit)
        {
            newQueue!.OnMessage(message =>
            {
                FanOutToLocalChannels(message.Message);
            });
        }
        else
        {
            // Lost the race — somebody else opened the SUBSCRIBE while we were
            // awaiting our own. Release the duplicate.
            if (newQueue is not null)
            {
                await newQueue.UnsubscribeAsync().ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Closes the Redis SUBSCRIBE when the last local subscriber leaves so we
    /// don't keep a connection open in the steady state of "admin page closed".
    /// </summary>
    private async Task MaybeReleaseRedisSubscriptionAsync()
    {
        if (!_subscribers.IsEmpty) return;

        ChannelMessageQueue? toRelease = null;
        lock (_subscriptionGate)
        {
            if (_subscribers.IsEmpty && _redisQueue is not null)
            {
                toRelease = _redisQueue;
                _redisQueue = null;
            }
        }

        if (toRelease is not null)
        {
            try
            {
                await toRelease.UnsubscribeAsync().ConfigureAwait(false);
            }
#pragma warning disable CA1031 // Unsubscribe failure is informational only.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(
                    ex,
                    "RedisWikidataEnrichmentEventBroadcaster: UNSUBSCRIBE failed on channel {Channel}; relying on connection teardown",
                    ChannelName);
            }
        }
    }

    /// <summary>
    /// Deserialises the incoming Redis message and writes it into every local
    /// subscriber's bounded channel. Drop-oldest policy guarantees TryWrite
    /// never blocks — a slow SSE client can never starve the Redis pump.
    /// </summary>
    private void FanOutToLocalChannels(RedisValue message)
    {
        if (message.IsNullOrEmpty || _subscribers.IsEmpty) return;

        WikidataEnrichmentEvent? payload;
        try
        {
            payload = JsonSerializer.Deserialize<WikidataEnrichmentEvent>(
                message.ToString(), SerializerOptions);
        }
        catch (JsonException ex)
        {
            // Malformed message — log + drop. A future schema change would land
            // here; the dead-letter table is still the source of truth.
            _logger.LogWarning(
                ex,
                "RedisWikidataEnrichmentEventBroadcaster: dropped malformed message on channel {Channel}",
                ChannelName);
            return;
        }

        if (payload is null) return;

        foreach (var (_, channel) in _subscribers)
        {
            if (!channel.Writer.TryWrite(payload))
            {
                _logger.LogDebug(
                    "RedisWikidataEnrichmentEventBroadcaster: dropped event {AttemptId} for a completed subscriber",
                    payload.AttemptId);
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ChannelMessageQueue? toRelease;
        lock (_subscriptionGate)
        {
            toRelease = _redisQueue;
            _redisQueue = null;
        }

        if (toRelease is not null)
        {
            try
            {
                toRelease.Unsubscribe();
            }
#pragma warning disable CA1031 // Best-effort teardown.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(
                    ex,
                    "RedisWikidataEnrichmentEventBroadcaster: UNSUBSCRIBE during dispose failed on channel {Channel}",
                    ChannelName);
            }
        }

        // Complete every outstanding local channel so any active subscriber
        // exits its enumerator promptly instead of hanging on ReadAsync.
        foreach (var (_, channel) in _subscribers)
        {
            channel.Writer.TryComplete();
        }
        _subscribers.Clear();
    }
}
