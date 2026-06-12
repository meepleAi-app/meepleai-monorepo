using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Singleton in-process implementation of
/// <see cref="IWikidataEnrichmentEventBroadcaster"/>. Each subscriber gets a
/// dedicated bounded <see cref="Channel{T}"/>; publish writes to all channels
/// with <see cref="BoundedChannelFullMode.DropOldest"/> drop policy so a slow
/// SSE client never blocks the M9 scheduler tick.
/// </summary>
/// <remarks>
/// Capacity per-subscriber is intentionally small (128) — the FE only renders
/// the last N events on the page anyway, and a deep queue would just amplify
/// delivery latency without changing the steady-state UX.
/// </remarks>
internal sealed class InMemoryWikidataEnrichmentEventBroadcaster : IWikidataEnrichmentEventBroadcaster
{
    private const int PerSubscriberCapacity = 128;

    private readonly ConcurrentDictionary<Guid, Channel<WikidataEnrichmentEvent>> _subscribers = new();
    private readonly ILogger<InMemoryWikidataEnrichmentEventBroadcaster> _logger;

    public InMemoryWikidataEnrichmentEventBroadcaster(
        ILogger<InMemoryWikidataEnrichmentEventBroadcaster> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public int SubscriberCount => _subscribers.Count;

    public void Publish(WikidataEnrichmentEvent payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (_subscribers.IsEmpty)
        {
            return;
        }

        foreach (var (_, channel) in _subscribers)
        {
            // DropOldest channel — TryWrite never blocks. If the subscriber's
            // queue is full, the channel quietly evicts the oldest pending
            // event before accepting the new one. Either way TryWrite returns
            // true on a healthy channel; false only when the writer has
            // already completed, which we treat as a sign the subscriber is
            // being torn down and the next iteration will clean it up.
            if (!channel.Writer.TryWrite(payload))
            {
                _logger.LogDebug(
                    "InMemoryWikidataEnrichmentEventBroadcaster: dropped event {AttemptId} for a completed subscriber",
                    payload.AttemptId);
            }
        }
    }

    public async IAsyncEnumerable<WikidataEnrichmentEvent> SubscribeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateBounded<WikidataEnrichmentEvent>(
            new BoundedChannelOptions(PerSubscriberCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest,
            });

        _subscribers.TryAdd(subscriberId, channel);
        _logger.LogDebug(
            "InMemoryWikidataEnrichmentEventBroadcaster: subscriber {SubscriberId} added (now {Total})",
            subscriberId, _subscribers.Count);

        try
        {
            // NOTE: never use `yield break` from inside a nested catch — the
            // C# async-iterator state machine guarantees `finally` runs on
            // `yield break`, but the inversion via a `break` flag is the safer
            // pattern for a long-running subscriber loop (avoids any future
            // refactor accidentally moving `yield break` to a position that
            // skips the outer finally cleanup of the subscriber map entry).
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
            _logger.LogDebug(
                "InMemoryWikidataEnrichmentEventBroadcaster: subscriber {SubscriberId} removed (now {Total})",
                subscriberId, _subscribers.Count);
        }
    }
}
