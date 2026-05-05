namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Phase 2 stub: always allows quota consumption.
/// PHASE 3 REPLACE with CreditBasedPricingEngine in Phase 3 Task 3.2.
/// </summary>
internal sealed class NullPricingEngine : IPricingEngine
{
    public Task<bool> ConsumeQuotaAsync(Guid? userId, string operationType, CancellationToken ct = default)
        => Task.FromResult(true);
}
