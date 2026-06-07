using Api.SharedKernel.Domain.Interfaces;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Events;

/// <summary>
/// Issue #3721: Event raised when token usage should be tracked in the financial ledger.
/// Published by TokenTrackingService after successful usage recording.
///
/// Issue #1938 / CF-2: Implements <see cref="IDomainEvent"/> so the ledger handler
/// can propagate <c>EventId</c> as <c>SourceEventId</c> on the resulting
/// <c>LedgerEntry</c> for DB-level idempotency when the publishing transaction
/// rolls back after publish (or MediatR retries transiently).
/// </summary>
public record TokenUsageLedgerEvent(
    Guid UserId,
    string ModelId,
    int TokensConsumed,
    decimal CostUsd,
    string? Endpoint,
    DateTime Timestamp) : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}
