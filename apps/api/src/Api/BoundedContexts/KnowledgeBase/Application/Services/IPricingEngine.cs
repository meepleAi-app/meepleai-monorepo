namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Enforces pricing quotas for KB operations.
/// Phase 2: NullPricingEngine stub (always allows).
/// Phase 3 Task 3.2: CreditBasedPricingEngine implementation.
/// </summary>
internal interface IPricingEngine
{
    /// <summary>
    /// Consumes quota for an operation.
    /// Returns true if the operation is allowed, false if quota is exhausted.
    /// </summary>
    Task<bool> ConsumeQuotaAsync(
        Guid? userId,
        string operationType,
        CancellationToken ct = default);
}
