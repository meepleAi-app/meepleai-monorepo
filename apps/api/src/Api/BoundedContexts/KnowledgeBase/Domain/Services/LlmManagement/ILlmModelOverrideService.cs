namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.LlmManagement;

/// <summary>
/// ISSUE-1725: Service for managing LLM model overrides during budget constraints.
/// Provides in-memory override mechanism to downgrade to cheaper models when budget critical.
/// </summary>
/// <remarks>
/// Use Case: When LlmBudgetMonitoringService detects critical budget threshold,
/// it calls EnableBudgetMode() to switch all requests to cheaper models.
/// Routing strategies check IsInBudgetMode() and GetOverrideModel() before selecting default models.
///
/// Lifecycle: In-memory state, reset on app restart or when budget drops below critical.
/// </remarks>
internal interface ILlmModelOverrideService
{
    /// <summary>
    /// Check if budget mode is currently active (cheaper models enforced)
    /// </summary>
    bool IsInBudgetMode();

    /// <summary>
    /// Enable budget mode (switch to cheaper models for all requests)
    /// </summary>
    /// <param name="reason">Reason for enabling budget mode (e.g., "Daily budget 95% exceeded")</param>
    void EnableBudgetMode(string reason);

    /// <summary>
    /// Disable budget mode (return to normal model selection)
    /// </summary>
    void DisableBudgetMode();

    /// <summary>
    /// Get cheaper alternative model for a given model (if in budget mode)
    /// </summary>
    /// <param name="originalModel">Original model requested</param>
    /// <returns>Cheaper alternative model, or original if no downgrade mapping exists</returns>
    string GetOverrideModel(string originalModel);

    /// <summary>
    /// Get budget mode status information (for monitoring/debugging)
    /// </summary>
    /// <returns>Status string with reason and timestamp</returns>
    string GetBudgetModeStatus();

    /// <summary>
    /// Get appropriate model when user budget is exhausted (per-user budget constraint)
    /// </summary>
    /// <param name="requestedModel">Originally requested model</param>
    /// <param name="budgetExhausted">True if user budget is exhausted</param>
    /// <returns>Free model alternative if budget exhausted, otherwise original model</returns>
    string GetModelForBudgetConstraint(string requestedModel, bool budgetExhausted);
}
