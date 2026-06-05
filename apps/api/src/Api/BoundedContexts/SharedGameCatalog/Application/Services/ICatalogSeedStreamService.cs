namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #1903 M6.1: SSE event broadcaster for admin catalog seed pipeline.
/// Subscribers receive a replay of the last N events + future live events.
/// Pattern: mirrors <c>IPdfProgressStreamService</c> (DocumentProcessing BC) —
/// in-memory pub/sub via <see cref="System.Threading.Channels.Channel"/>.
/// </summary>
/// <remarks>
/// <para>Singleton registration: a single instance brokers all events across
/// the process so the Quartz <c>CatalogSeedFetchJob</c> publisher and admin
/// HTTP SSE subscribers share state. Per-subscriber bounded channels with
/// <see cref="System.Threading.Channels.BoundedChannelFullMode.DropOldest"/>
/// guarantee slow subscribers can never back-pressure the publisher.</para>
/// <para>Replay buffer is bounded (200 events) — newly connecting admins
/// catch up on the most recent batch activity without paging through DB
/// records.</para>
/// </remarks>
internal interface ICatalogSeedStreamService
{
    /// <summary>
    /// Publish an event to all active subscribers and append it to the replay
    /// buffer. Never throws on subscriber faults — failed subscribers are
    /// removed silently and logged at <c>Warning</c> level.
    /// </summary>
    /// <exception cref="ArgumentNullException">When <paramref name="evt"/> is null.</exception>
    Task PublishAsync(CatalogSeedStreamEvent evt, CancellationToken ct = default);

    /// <summary>
    /// Subscribe to live events. The returned enumerable first replays the
    /// existing buffered events (snapshot taken at subscribe time), then
    /// yields live events as they are published. Auto-cleans the subscription
    /// when the <paramref name="ct"/> cancels or the enumerator is disposed.
    /// </summary>
    IAsyncEnumerable<CatalogSeedStreamEvent> SubscribeAsync(CancellationToken ct);
}

/// <summary>
/// Discriminated event base for the SSE stream. Concrete subtypes carry the
/// payload; <see cref="EventType"/> drives the SSE <c>event:</c> field, and
/// <see cref="OccurredAt"/> is included verbatim in the JSON payload.
/// </summary>
internal abstract record CatalogSeedStreamEvent(string EventType, DateTime OccurredAt);

/// <summary>Emitted at the start of a CatalogSeedFetchJob batch.</summary>
internal sealed record BatchStartedEvent(
    Guid BatchId,
    IReadOnlyList<Guid> DraftIds,
    DateTime StartedAt) : CatalogSeedStreamEvent("batch.started", StartedAt);

/// <summary>Emitted after a single draft is fetched successfully.</summary>
internal sealed record SeedEntryFetchedEvent(
    Guid DraftId,
    int? BggId,
    string ProviderUsed,
    int FetchedFields,
    double DurationMs,
    DateTime OccurredAt) : CatalogSeedStreamEvent("seed.fetched", OccurredAt);

/// <summary>Emitted after a single draft fetch fails (terminal FetchFailed status).</summary>
internal sealed record SeedEntryFetchFailedEvent(
    Guid DraftId,
    int? BggId,
    string ErrorType,
    DateTime OccurredAt) : CatalogSeedStreamEvent("seed.failed", OccurredAt);

/// <summary>Emitted at the end of a CatalogSeedFetchJob batch.</summary>
internal sealed record BatchCompletedEvent(
    Guid BatchId,
    int Succeeded,
    int Failed,
    double TotalDurationMs,
    DateTime CompletedAt) : CatalogSeedStreamEvent("batch.completed", CompletedAt);
