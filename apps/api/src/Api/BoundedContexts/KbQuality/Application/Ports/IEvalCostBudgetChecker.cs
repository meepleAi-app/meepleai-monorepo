namespace Api.BoundedContexts.KbQuality.Application.Ports;

/// <summary>
/// Port to the per-tenant monthly cost counters (D-H, plan amendment A1).
/// Implemented by `EvaluationRepository` backed by `KbQualityBudgetCounter` entity.
/// </summary>
/// <remarks>
/// #4166 (ADR-095 D6): the KbQuality cap is now the evaluation quota of the single LLM budget,
/// read through <c>ILlmBudgetStatus</c>. The counters are no longer read or written; only the
/// monthly cleanup is left, until the table is dropped in a later contract delivery.
/// </remarks>
public interface IEvalCostBudgetChecker
{
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
