namespace Api.BoundedContexts.BusinessSimulations.Application.Interfaces;

/// <summary>
/// Service for automatically creating ledger entries from system events.
/// Issue #3721: Automatic Ledger Tracking (Epic #3688)
/// </summary>
internal interface ILedgerTrackingService
{
    /// <summary>
    /// Tracks a token usage expense in the financial ledger.
    /// Creates an auto-generated expense entry with TokenUsage category.
    /// </summary>
    /// <param name="userId">The user who consumed the tokens</param>
    /// <param name="modelId">The LLM model used (e.g., "openai/gpt-4o-mini")</param>
    /// <param name="tokensConsumed">Number of tokens consumed</param>
    /// <param name="costUsd">Cost in USD from OpenRouter pricing</param>
    /// <param name="endpoint">The API endpoint that triggered usage (e.g., "chat", "qa")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TrackTokenUsageAsync(
        Guid userId,
        string modelId,
        int tokensConsumed,
        decimal costUsd,
        string? endpoint = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks a subscription payment in the financial ledger.
    /// Creates an auto-generated income entry with Subscription category.
    /// Ready for Stripe webhook integration.
    /// </summary>
    /// <param name="userId">The subscribing user</param>
    /// <param name="amount">Payment amount</param>
    /// <param name="currency">Payment currency (ISO 4217)</param>
    /// <param name="subscriptionType">Type of subscription (e.g., "Basic", "Pro")</param>
    /// <param name="metadata">Optional JSON metadata (e.g., Stripe payment intent ID)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TrackSubscriptionPaymentAsync(
        Guid userId,
        decimal amount,
        string currency,
        string subscriptionType,
        string? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracks an infrastructure cost in the financial ledger.
    /// Creates an auto-generated expense entry with Infrastructure category.
    /// Typically called by the daily aggregation job.
    /// </summary>
    /// <param name="amount">Cost amount in USD</param>
    /// <param name="description">Description of the cost (e.g., "Daily LLM API costs")</param>
    /// <param name="metadata">Optional JSON metadata with cost breakdown</param>
    /// <param name="date">The date for the cost entry (defaults to UTC now)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task TrackInfrastructureCostAsync(
        decimal amount,
        string description,
        string? metadata = null,
        DateTime? date = null,
        CancellationToken cancellationToken = default);
}
