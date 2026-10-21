using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #1903 M6.1 — singleton, in-memory SSE event broadcaster for the admin
/// catalog seed pipeline. Mirrors the pub/sub pattern of
/// <c>PdfProgressStreamService</c> (DocumentProcessing BC).
/// </summary>
/// <remarks>
/// <para>Each subscriber owns a bounded channel
/// (<see cref="BoundedChannelFullMode.DropOldest"/>) so slow consumers cannot
/// back-pressure the publisher. The replay buffer is capped at
/// <see cref="ReplayBufferSize"/> entries (FIFO).</para>
/// <para>Faulty subscribers are removed silently with a warning log — a
/// single bad client must never stall job execution.</para>
/// </remarks>
internal sealed class CatalogSeedStreamService : ICatalogSeedStreamService
{
    private const int ReplayBufferSize = 200;
    private const int SubscriberChannelCapacity = 100;

    private readonly ConcurrentDictionary<Guid, Channel<CatalogSeedStreamEvent>> _subscribers = new();
    private readonly LinkedList<CatalogSeedStreamEvent> _replayBuffer = new();
    private readonly Lock _replayLock = new();
    private readonly ILogger<CatalogSeedStreamService> _logger;

    public CatalogSeedStreamService(ILogger<CatalogSeedStreamService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task PublishAsync(CatalogSeedStreamEvent evt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        AddToReplayBuffer(evt);

        foreach (var (id, channel) in _subscribers)
        {
            try
            {
                // Fast path: non-blocking write succeeds when channel has slack.
                if (channel.Writer.TryWrite(evt))
                {
                    continue;
                }

                // Slow path: bounded channel is full → DropOldest semantics will
                // make room asynchronously. Await briefly to honour publisher CT.
                await channel.Writer.WriteAsync(evt, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // INFRASTRUCTURE BOUNDARY: a single faulty subscriber must not crash
            // the publisher. Mirror of PdfProgressStreamService behaviour.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                _logger.LogWarning(ex,
                    "CatalogSeedStreamService: failed to write event {EventType} to subscriber {SubscriberId}; removing",
                    evt.EventType, id);
                if (_subscribers.TryRemove(id, out var removed))
                {
                    removed.Writer.TryComplete();
                }
            }
        }
    }

    /// <summary>
    /// Subscribers currently registered. A subscriber registers on the first
    /// <c>MoveNextAsync</c> of its enumerator (the iterator body is lazy), not when
    /// <see cref="SubscribeAsync"/> is called, and unregisters when the enumeration ends.
    /// Exposed so tests can observe registration instead of guessing it with a
    /// delay (#4604, same shape as #3711 and #4193).
    /// </summary>
    internal int SubscriberCount => _subscribers.Count;

    /// <summary>
    /// Subscribe to stream. New subscriber receives a replay of recent events
    /// (up to <see cref="ReplayBufferSize"/>) followed by live events. The
    /// subscriber registers lazily, on the first <c>MoveNextAsync</c>; an event
    /// whose <c>PublishAsync</c> completed between the call and that first move
    /// is not lost — it sits in the replay buffer and is delivered by the
    /// replay, exactly once.
    /// Replay uses an <c>OccurredAt</c> cutoff, taken right after registration,
    /// to reduce duplicates at the "subscribe/publish seam": only events strictly
    /// before the cutoff are replayed, and events stamped at or after it are
    /// guaranteed to reach the live channel. That is <b>exactly-once only when
    /// <c>PublishAsync</c> does not overlap the registration</b>. If it does, an
    /// event E stamped before the cutoff can be added to the buffer (so the
    /// snapshot replays it) and then written to the channel that has just been
    /// registered (so it arrives live too): E is delivered twice. Nothing is lost;
    /// the duplicate is acceptable for a diagnostic SSE stream.
    /// </summary>
    /// <inheritdoc />
    public async IAsyncEnumerable<CatalogSeedStreamEvent> SubscribeAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var subscriberId = Guid.NewGuid();
        var channel = Channel.CreateBounded<CatalogSeedStreamEvent>(
            new BoundedChannelOptions(SubscriberChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });

        _subscribers[subscriberId] = channel;

        // Mark cutoff: events with OccurredAt >= cutoff are guaranteed to reach
        // the channel (since registration above happens-before any subsequent
        // PublishAsync write), so they are not replayed. This narrows, but does
        // not close, the double-delivery window at the subscribe/publish seam:
        // a PublishAsync already in flight can add an event E (OccurredAt <
        // cutoff) to the replay buffer and then write E to this freshly
        // registered channel, so E arrives from the snapshot AND live. Closing
        // it needs registration, cutoff and snapshot under _replayLock together
        // with the buffer add and the subscriber enumeration in PublishAsync, or
        // a sequence number instead of the timestamp.
        var registrationCutoff = DateTime.UtcNow;

        _logger.LogDebug(
            "CatalogSeedStreamService: subscriber {SubscriberId} connected (total={Count})",
            subscriberId, _subscribers.Count);

        try
        {
            // Snapshot the replay buffer under lock so we don't observe a
            // half-rotated linked list during enumeration.
            List<CatalogSeedStreamEvent> snapshot;
            lock (_replayLock)
            {
                snapshot = _replayBuffer
                    .Where(e => e.OccurredAt < registrationCutoff)
                    .ToList();
            }

            foreach (var replay in snapshot)
            {
                yield return replay;
            }

            await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return evt;
            }
        }
        finally
        {
            _subscribers.TryRemove(subscriberId, out _);
            channel.Writer.TryComplete();
            _logger.LogDebug(
                "CatalogSeedStreamService: subscriber {SubscriberId} disconnected (remaining={Count})",
                subscriberId, _subscribers.Count);
        }
    }

    private void AddToReplayBuffer(CatalogSeedStreamEvent evt)
    {
        lock (_replayLock)
        {
            _replayBuffer.AddLast(evt);
            while (_replayBuffer.Count > ReplayBufferSize)
            {
                _replayBuffer.RemoveFirst();
            }
        }
    }
}
