namespace Api.BoundedContexts.Administration.Domain.Exceptions;

/// <summary>
/// Thrown by the admin retry path when an operator attempts to re-arm a
/// <c>domain_event_outbox</c> row that is not in <c>Failed</c> status.
///
/// <para>Issue #1535 T6 code review (F5): the original implementation caught
/// <see cref="InvalidOperationException"/> at the endpoint and mapped it to 409
/// Conflict. That swept up unrelated <c>InvalidOperationException</c>s thrown by EF
/// (concurrency conflicts, second-operation-on-context), MediatR behaviors, and
/// AuditingSaveChangesInterceptor — all masquerading as 'cannot re-arm'. This dedicated
/// domain exception narrows the catch to the actual entity-guard failure.</para>
/// </summary>
public sealed class OutboxRowNotFailedException : Exception
{
    public OutboxRowNotFailedException(Guid id, string currentStatus)
        : base($"Cannot re-arm domain_event_outbox row {id}: current status is '{currentStatus}', " +
               "only rows in 'Failed' status may be re-armed.")
    {
        Id = id;
        CurrentStatus = currentStatus;
    }

    /// <summary>The outbox row id the operator attempted to re-arm.</summary>
    public Guid Id { get; }

    /// <summary>The row's status at the moment of the re-arm attempt.</summary>
    public string CurrentStatus { get; }
}
