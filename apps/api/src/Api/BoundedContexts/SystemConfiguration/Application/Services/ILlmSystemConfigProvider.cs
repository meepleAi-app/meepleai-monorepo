namespace Api.BoundedContexts.SystemConfiguration.Application.Services;

/// <summary>
/// Issue #5498: Provides LLM system configuration with DB-first, appsettings-fallback strategy.
/// Cached for performance — changes take effect within cache TTL (60s).
/// </summary>
public interface ILlmSystemConfigProvider
{
    /// <summary>
    /// Get circuit breaker failure threshold (DB override or appsettings default).
    /// </summary>
    Task<int> GetCircuitBreakerFailureThresholdAsync(CancellationToken ct = default);

    /// <summary>
    /// Get circuit breaker open duration in seconds (DB override or appsettings default).
    /// </summary>
    Task<int> GetCircuitBreakerOpenDurationSecondsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get circuit breaker success threshold for half-open recovery (DB override or appsettings default).
    /// </summary>
    Task<int> GetCircuitBreakerSuccessThresholdAsync(CancellationToken ct = default);

    /// <summary>
    /// Get daily budget limit in USD.
    /// </summary>
    Task<decimal> GetDailyBudgetUsdAsync(CancellationToken ct = default);

    /// <summary>
    /// Get monthly budget limit in USD.
    /// </summary>
    Task<decimal> GetMonthlyBudgetUsdAsync(CancellationToken ct = default);

    /// <summary>
    /// Get a synchronous snapshot of the current thresholds (from cache or defaults).
    /// Safe to call from non-async contexts (e.g., Singleton initialization).
    /// </summary>
    (int FailureThreshold, int OpenDurationSeconds, int SuccessThreshold) GetCircuitBreakerThresholdsSnapshot();

    /// <summary>
    /// Invalidate the cached configuration (e.g., after admin update).
    /// </summary>
    void InvalidateCache();
}
