using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.DomainEventOutbox;

/// <summary>
/// Returns the oldest Pending rows (FIFO by <c>EnqueuedAt</c>) — the backlog the
/// processor is currently working through. Powers the future
/// <c>/admin/monitor?tab=events</c> "in-flight" panel where operators verify
/// backlog dynamics during incidents.
///
/// <para><see cref="Limit"/> is clamped to <c>[1, 200]</c> by the handler.</para>
///
/// Issue #1535 T6.
/// </summary>
internal sealed record GetPendingEventOutboxRowsQuery(int Limit)
    : IQuery<IReadOnlyList<DomainEventOutboxRowDto>>;
