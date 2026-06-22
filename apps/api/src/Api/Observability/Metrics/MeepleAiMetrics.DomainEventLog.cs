// BE-3 #1590 AC7: Domain event log persistence counters
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// G1: Incremented once per domain_event_logs row successfully inserted.
    /// Tag: event_type — the registered alias (e.g. "agent.created") from EventTypeRegistry.
    /// </summary>
    public static readonly Counter<long> DomainEventsInserted = Meter.CreateCounter<long>(
        name: "meepleai.domain_event_log.inserted.total",
        unit: "events",
        description: "Total domain events persisted to domain_event_logs by event type (#1590 G1)");

    /// <summary>
    /// G2: Incremented when a MediatR notification handler throws after the
    /// domain_event_log row is already durably committed (row is NOT rolled back).
    /// Tags:
    ///   event_type   — the registered alias (e.g. "agent.created"), with CLR-type fallback
    ///                  for unregistered events. Matches G1's event_type so the two counters
    ///                  JOIN on the same label value in dashboards/alerts.
    ///   handler_name — MediatR dispatches to all handlers internally, so the individual
    ///                  handler name is not available here; set to the event CLR type name
    ///                  to keep the tag cardinality bounded (only SessionFinalizedEvent has &gt;1
    ///                  handler — acceptable for v1 without a custom INotificationPublisher).
    /// </summary>
    public static readonly Counter<long> DomainEventDispatchFailures = Meter.CreateCounter<long>(
        name: "meepleai.domain_event_log.dispatch_failures.total",
        unit: "failures",
        description: "Total handler dispatch failures after domain event persisted (#1590 G2)");
}
