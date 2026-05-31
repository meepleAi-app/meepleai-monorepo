using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Api.BoundedContexts.Administration.Application.Queries.AdminEvents;
using Api.Observability;

namespace Api.Infrastructure.EventBroadcasting;

/// <summary>
/// Singleton, in-process fan-out broadcaster for <see cref="DomainEventDto"/> events.
/// Used to feed the Admin Monitor SSE stream (F4.1, issue #1718).
///
/// <para>
/// <b>Channel semantics — DropOldest on backpressure:</b><br/>
/// Each subscriber gets its own bounded <see cref="Channel{T}"/> (default capacity 1000).
/// When a slow SSE consumer falls behind and the channel is full, the <i>oldest</i> event in
/// that subscriber's buffer is silently discarded to make room for the incoming event
/// (<c>BoundedChannelFullMode.DropOldest</c>).
/// This is the correct semantic for a realtime monitor: showing recent events is more
/// valuable than preserving stale ones for a lagged client.
/// </para>
///
/// <para>
/// <b>Eager channel registration:</b><br/>
/// Unlike a naïve async-iterator implementation, the channel is created and registered
/// in the <see cref="Subscribe"/> call (before the caller starts iterating).
/// This ensures that events published immediately after <c>Subscribe</c> are never lost.
/// </para>
///
/// <para>
/// <b>Drop metric:</b><br/>
/// Because <c>TryWrite</c> with <c>DropOldest</c> always returns <see langword="true"/>
/// (it drops the old item to make room and then writes), we cannot rely on the return
/// value to count drops. Instead we snapshot <c>ChannelReader.Count</c> before
/// writing: if the count equals the capacity, a drop is imminent. We increment
/// <see cref="MeepleAiMetrics.AdminSseEventsDropped"/> at that point.
/// The check is inherently a slight race (another writer could consume a slot between
/// the check and the write), but for a monitoring counter approximate accuracy is
/// sufficient and avoids locking overhead.
/// </para>
///
/// <para>
/// <b>Thread safety:</b><br/>
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> handles concurrent subscribe/unsubscribe.
/// Each <see cref="Channel{T}"/> is thread-safe by design (multi-writer, single-reader).
/// No additional locks are required.
/// </para>
/// </summary>
internal sealed class ChannelEventBroadcaster : IEventBroadcaster, IDisposable
{
    private const int DefaultCapacity = 1000;

    // Key: subscriber Guid (one per SSE connection)
    // Value: (channel, filter) pair
    private readonly ConcurrentDictionary<Guid, (Channel<DomainEventDto> Channel, EventBroadcastFilter Filter)> _subscribers
        = new();

    private readonly int _capacity;
    private bool _disposed;

    // Exposed as field (not property) for Interlocked operations in unit tests.
    // Production callers observe via MeepleAiMetrics.AdminSseEventsDropped counter.
    internal long DroppedEvents;

    public ChannelEventBroadcaster() : this(DefaultCapacity)
    {
    }

    /// <summary>
    /// Test-only constructor allowing a custom channel capacity so backpressure tests
    /// do not need to fill a 1 000-item buffer.
    /// </summary>
    internal ChannelEventBroadcaster(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _capacity = capacity;
    }

    // -------------------------------------------------------------------------
    // IEventBroadcaster
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    /// <remarks>
    /// Iterates all registered subscriber channels. For each channel whose
    /// <see cref="EventBroadcastFilter"/> matches <paramref name="evt"/>:
    /// <list type="bullet">
    ///   <item>If the channel is at capacity, increments the drop counter (see class-level doc).</item>
    ///   <item>Calls <c>TryWrite</c>, which with <c>DropOldest</c> always succeeds.</item>
    /// </list>
    /// Never throws. Silently skips closed channels (completed subscribers).
    /// </remarks>
    public void Publish(DomainEventDto evt)
    {
        ArgumentNullException.ThrowIfNull(evt);
        if (_disposed) return;

        foreach (var (_, (channel, filter)) in _subscribers)
        {
            if (!filter.Matches(evt)) continue;

            // Approximate drop detection: if the reader is already at capacity,
            // DropOldest will silently eject the oldest item before writing.
            if (channel.Reader.Count >= _capacity)
            {
                Interlocked.Increment(ref DroppedEvents);
                MeepleAiMetrics.AdminSseEventsDropped.Add(1);
            }

            // TryWrite with DropOldest always returns true (drops oldest to make room).
            channel.Writer.TryWrite(evt);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <b>Important — eager registration:</b>
    /// The subscriber's channel is created and added to the internal dictionary
    /// synchronously inside this call, before the caller starts iterating.
    /// This prevents a race where events published immediately after <c>Subscribe</c>
    /// would be lost because iteration hadn't started yet.
    /// </remarks>
    public IAsyncEnumerable<DomainEventDto> Subscribe(EventBroadcastFilter filter, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // Eagerly create and register the channel. Events published after this line
        // (but before the caller starts iterating) will be buffered in the channel.
        var channel = Channel.CreateBounded<DomainEventDto>(new BoundedChannelOptions(_capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false
        });

        var id = Guid.NewGuid();
        _subscribers[id] = (channel, filter);

        // Return an async enumerable that reads from the already-registered channel
        // and removes the subscription on cancellation or loop exit.
        return IterateChannel(id, channel, ct);
    }

    // S4456: iterator separated from parameter-validating wrapper above.
    private async IAsyncEnumerable<DomainEventDto> IterateChannel(
        Guid id,
        Channel<DomainEventDto> channel,
        [EnumeratorCancellation] CancellationToken ct)
    {
        try
        {
            await foreach (var evt in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return evt;
            }
        }
        finally
        {
            // Unregister so Publish stops writing to this channel.
            _subscribers.TryRemove(id, out _);
            // Complete the writer so ReadAllAsync terminates if not already done.
            channel.Writer.TryComplete();
        }
    }

    // -------------------------------------------------------------------------
    // IDisposable
    // -------------------------------------------------------------------------

    /// <summary>
    /// Closes all subscriber channels, causing their <c>ReadAllAsync</c> loops to
    /// exit normally (no exception thrown to consumers). Idempotent.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var (_, (channel, _)) in _subscribers)
        {
            channel.Writer.TryComplete();
        }
        _subscribers.Clear();
    }
}
