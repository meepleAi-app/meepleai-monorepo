namespace Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Issue #2520: Model usage statistics for monitoring and cost tracking
/// Stored as JSONB in PostgreSQL
/// </summary>
public sealed record UsageStats
{
    public int TotalRequests { get; init; }
    public long TotalTokensUsed { get; init; }
    public decimal TotalCostUsd { get; init; }
    public DateTime? LastUsedAt { get; init; }

    public UsageStats(
        int totalRequests = 0,
        long totalTokensUsed = 0,
        decimal totalCostUsd = 0m,
        DateTime? lastUsedAt = null)
    {
        if (totalRequests < 0)
            throw new ArgumentException("TotalRequests cannot be negative", nameof(totalRequests));

        if (totalTokensUsed < 0)
            throw new ArgumentException("TotalTokensUsed cannot be negative", nameof(totalTokensUsed));

        if (totalCostUsd < 0)
            throw new ArgumentException("TotalCostUsd cannot be negative", nameof(totalCostUsd));

        TotalRequests = totalRequests;
        TotalTokensUsed = totalTokensUsed;
        TotalCostUsd = totalCostUsd;
        LastUsedAt = lastUsedAt;
    }

    public static UsageStats Empty => new();

    /// <summary>
    /// Update usage statistics after a successful request
    /// </summary>
    public UsageStats RecordUsage(int tokens, decimal costUsd)
    {
        return this with
        {
            TotalRequests = TotalRequests + 1,
            TotalTokensUsed = TotalTokensUsed + tokens,
            TotalCostUsd = TotalCostUsd + costUsd,
            LastUsedAt = DateTime.UtcNow
        };
    }
}
