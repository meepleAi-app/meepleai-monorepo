namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

/// <summary>
/// Issue #5074: Cached snapshot of OpenRouter account status from /auth/key endpoint.
/// </summary>
public sealed record OpenRouterAccountStatus
{
    /// <summary>Current account balance in USD.</summary>
    public decimal BalanceUsd { get; init; }

    /// <summary>Account credit limit in USD (0 = pay-as-you-go).</summary>
    public decimal LimitUsd { get; init; }

    /// <summary>Total usage this period in USD.</summary>
    public decimal UsageUsd { get; init; }

    /// <summary>True if the account is on a free tier with limited models.</summary>
    public bool IsFreeTier { get; init; }

    /// <summary>Rate limit: max requests per interval.</summary>
    public int RateLimitRequests { get; init; }

    /// <summary>Rate limit interval (e.g. "10s", "1m").</summary>
    public string RateLimitInterval { get; init; } = string.Empty;

    /// <summary>Timestamp when this data was retrieved from the API.</summary>
    public DateTime LastUpdated { get; init; }
}
