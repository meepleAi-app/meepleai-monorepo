namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to per-tenant monthly cost budget tracking (D-H, plan amendment A1).
/// Implemented by `EvalCostBudgetCheckerAdapter` backed by `KbQualityBudgetCounter` entity.
/// </summary>
public interface IEvalCostBudgetChecker
{
    Task<decimal> GetRemainingAsync(Guid tenantId, CancellationToken ct);

    Task IncrementSpentAsync(Guid tenantId, decimal amountUsd, CancellationToken ct);
}
