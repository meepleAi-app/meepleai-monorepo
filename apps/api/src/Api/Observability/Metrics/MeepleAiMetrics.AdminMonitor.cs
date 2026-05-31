// F4.1 #1718 — Admin Monitor SSE broadcast metrics
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter incremented each time a <c>DomainEventDto</c> is silently dropped
    /// because a subscriber's bounded channel is at capacity (DropOldest).
    ///
    /// Implementation note: with <c>BoundedChannelFullMode.DropOldest</c>,
    /// <c>TryWrite</c> always returns <see langword="true"/> (it ejects the oldest
    /// item to make room and then writes the new one). Drops are therefore detected
    /// by snapshot-comparing the reader count against the channel capacity before
    /// each write. The check is approximate (slight race under concurrent writers)
    /// but sufficient for operational alerting.
    /// See <c>ChannelEventBroadcaster.Publish</c> for the increment site.
    ///
    /// Tags: none (bounded cardinality — single counter per process).
    ///
    /// Suggested alert: rate > 0 sustained for &gt; 1 min → SSE consumers falling behind.
    /// </summary>
    public static readonly Counter<long> AdminSseEventsDropped = Meter.CreateCounter<long>(
        name: "meepleai.admin.sse.events_dropped.total",
        unit: "events",
        description: "Total domain events dropped for slow SSE subscribers (DropOldest, F4.1 #1718)");
}
