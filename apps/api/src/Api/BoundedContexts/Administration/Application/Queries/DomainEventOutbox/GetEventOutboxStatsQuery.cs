using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.DomainEventOutbox;

/// <summary>
/// Returns an aggregate snapshot of the <c>domain_event_outbox</c> queue:
/// Pending / Failed counts, the oldest Pending row's age (seconds), and a
/// rolling 24h Sent counter. Powers the future <c>/admin/monitor?tab=events</c>
/// status panel.
///
/// Issue #1535 T6.
/// </summary>
internal sealed record GetEventOutboxStatsQuery() : IQuery<DomainEventOutboxStatsDto>;
