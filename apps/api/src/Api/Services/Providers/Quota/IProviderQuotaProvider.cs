namespace Api.Services.Providers.Quota;

/// <summary>
/// Strategy for fetching credit/balance from a provider that exposes a quota API.
/// Issue #936 (G2). Implementations enforce 5s timeout and never throw on network failures.
/// </summary>
internal interface IProviderQuotaProvider
{
    /// <summary>Provider name this strategy serves (e.g., "openrouter", "deepseek").</summary>
    string ProviderName { get; }

    /// <summary>
    /// Environment variable from which to read the API key.
    /// Quota fetch always requires authentication.
    /// </summary>
    string ApiKeyEnvVar { get; }

    Task<QuotaFetchResult> FetchAsync(string apiKey, CancellationToken cancellationToken);
}

/// <summary>
/// Result of a quota fetch. Currency normalized to USD.
/// All numeric fields are decimals to preserve cent precision.
/// </summary>
internal sealed record QuotaFetchResult(
    bool Success,
    decimal? UsedUsd,
    decimal? LimitUsd,        // null = unlimited / not enforced by provider
    decimal? RemainingUsd,
    DateTime? ResetAt,         // null = no scheduled reset (rolling balance)
    string? ErrorCode,
    string? ErrorMessage);
