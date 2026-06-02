namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to per-tenant monthly cost budget tracking (D-H, plan amendment A1).
/// Implemented by `EvaluationRepository` backed by `KbQualityBudgetCounter` entity.
/// </summary>
public interface IEvalCostBudgetChecker
{
    Task<decimal> GetRemainingAsync(Guid tenantId, CancellationToken ct);

    Task IncrementSpentAsync(Guid tenantId, decimal amountUsd, CancellationToken ct);

    /// <summary>
    /// Hard-deletes any <c>KbQualityBudgetCounter</c> row whose <c>YearMonth</c>
    /// (alphabetical string compare on the <c>yyyy-MM</c> format is equivalent to
    /// chronological compare) is strictly older than <paramref name="yearMonthExclusive"/>.
    /// Called by <c>KbQualityCostCapResetJob</c> on the 1st of each month to prune
    /// counters that are no longer in the current or prior accounting window.
    /// Returns the number of rows deleted.
    /// </summary>
    Task<int> DeleteBudgetCountersOlderThanAsync(string yearMonthExclusive, CancellationToken ct);
}
