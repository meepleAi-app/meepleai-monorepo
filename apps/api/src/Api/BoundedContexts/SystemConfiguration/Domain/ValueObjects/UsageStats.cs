namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Issue #2520: Model usage statistics for monitoring and cost tracking
/// Issue #2589: Restored input/output token granularity for accurate analytics
/// Stored as JSONB in PostgreSQL
/// </summary>
public sealed record UsageStats
{
    public int TotalRequests { get; init; }
    public long TotalInputTokens { get; init; }
    public long TotalOutputTokens { get; init; }
    public long TotalTokensUsed { get; init; }
    public decimal TotalCostUsd { get; init; }
    public DateTime? LastUsedAt { get; init; }

    public UsageStats(
        int totalRequests = 0,
        long totalInputTokens = 0,
        long totalOutputTokens = 0,
        long totalTokensUsed = 0,
        decimal totalCostUsd = 0m,
        DateTime? lastUsedAt = null)
    {
        if (totalRequests < 0)
            throw new ArgumentException("TotalRequests cannot be negative", nameof(totalRequests));

        if (totalInputTokens < 0)
            throw new ArgumentException("TotalInputTokens cannot be negative", nameof(totalInputTokens));

        if (totalOutputTokens < 0)
            throw new ArgumentException("TotalOutputTokens cannot be negative", nameof(totalOutputTokens));

        if (totalTokensUsed < 0)
            throw new ArgumentException("TotalTokensUsed cannot be negative", nameof(totalTokensUsed));

        if (totalCostUsd < 0)
            throw new ArgumentException("TotalCostUsd cannot be negative", nameof(totalCostUsd));

        TotalRequests = totalRequests;
        TotalInputTokens = totalInputTokens;
        TotalOutputTokens = totalOutputTokens;
        TotalTokensUsed = totalTokensUsed;
        TotalCostUsd = totalCostUsd;
        LastUsedAt = lastUsedAt;
    }

    public static UsageStats Empty => new();

    /// <summary>
    /// Update usage statistics after a successful request
    /// Issue #2589: Tracks input/output tokens separately for accurate cost attribution and analytics
    /// </summary>
    public UsageStats RecordUsage(int inputTokens, int outputTokens, decimal costUsd)
    {
        return this with
        {
            TotalRequests = TotalRequests + 1,
            TotalInputTokens = TotalInputTokens + inputTokens,
            TotalOutputTokens = TotalOutputTokens + outputTokens,
            TotalTokensUsed = TotalTokensUsed + inputTokens + outputTokens,
            TotalCostUsd = TotalCostUsd + costUsd,
            LastUsedAt = DateTime.UtcNow
        };
    }
}
