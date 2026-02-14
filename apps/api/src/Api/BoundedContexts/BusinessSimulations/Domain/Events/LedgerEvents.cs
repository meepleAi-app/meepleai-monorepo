using MediatR;

namespace Api.BoundedContexts.BusinessSimulations.Domain.Events;

/// <summary>
/// Issue #3721: Event raised when token usage should be tracked in the financial ledger.
/// Published by TokenTrackingService after successful usage recording.
/// </summary>
public record TokenUsageLedgerEvent(
    Guid UserId,
    string ModelId,
    int TokensConsumed,
    decimal CostUsd,
    string? Endpoint,
    DateTime Timestamp) : INotification;
