using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands.DomainEventOutbox;

/// <summary>
/// Operator-triggered re-arm of a terminal Failed outbox row. The row's
/// <c>Status</c> is reset to <c>Pending</c>, <c>Attempts</c> back to 0, and
/// <c>EnqueuedAt</c> moved to <c>now</c> so the processor's next poll picks it
/// up at the tail of the queue. Issued from the admin
/// <c>/admin/monitor?tab=events</c> dashboard (future work).
///
/// <para>Returns <c>true</c> when the row was successfully re-armed,
/// <c>false</c> when no Failed row with the given id exists (caller maps to a
/// 404). A row that exists in Pending/Sent status causes the underlying
/// <see cref="Api.Infrastructure.Entities.DomainEventOutbox.DomainEventOutboxEntity.RearmFromFailed"/>
/// to throw <see cref="InvalidOperationException"/> — the endpoint maps to 409.</para>
///
/// Issue #1535 T6.
/// </summary>
internal sealed record RetryEventOutboxRowCommand(Guid Id) : ICommand<bool>;
