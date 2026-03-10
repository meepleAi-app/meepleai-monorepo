namespace Api.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Issue #5498: Runtime LLM system configuration stored in database.
/// appsettings.json provides defaults; DB values override when present.
/// Supports circuit breaker thresholds, budget limits, and fallback chain.
/// </summary>
public sealed class LlmSystemConfig
{
    public Guid Id { get; private set; }

    // Circuit breaker settings
    public int CircuitBreakerFailureThreshold { get; private set; }
    public int CircuitBreakerOpenDurationSeconds { get; private set; }
    public int CircuitBreakerSuccessThreshold { get; private set; }

    // Budget limits
    public decimal DailyBudgetUsd { get; private set; }
    public decimal MonthlyBudgetUsd { get; private set; }

    // Fallback chain (JSON array of provider names)
    public string FallbackChainJson { get; private set; } = "[]";

    // Audit
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private LlmSystemConfig() { } // EF Core

    private LlmSystemConfig(
        Guid id,
        int failureThreshold,
        int openDurationSeconds,
        int successThreshold,
        decimal dailyBudgetUsd,
        decimal monthlyBudgetUsd,
        string fallbackChainJson)
    {
        Id = id;
        CircuitBreakerFailureThreshold = failureThreshold;
        CircuitBreakerOpenDurationSeconds = openDurationSeconds;
        CircuitBreakerSuccessThreshold = successThreshold;
        DailyBudgetUsd = dailyBudgetUsd;
        MonthlyBudgetUsd = monthlyBudgetUsd;
        FallbackChainJson = fallbackChainJson;
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Create a new LLM system config with default values.
    /// </summary>
    public static LlmSystemConfig CreateDefault()
    {
        return new LlmSystemConfig(
            Guid.NewGuid(),
            failureThreshold: 5,
            openDurationSeconds: 30,
            successThreshold: 3,
            dailyBudgetUsd: 10.00m,
            monthlyBudgetUsd: 100.00m,
            fallbackChainJson: "[\"Ollama\",\"OpenRouter\"]");
    }

    public void UpdateCircuitBreakerSettings(int failureThreshold, int openDurationSeconds, int successThreshold, Guid? updatedBy = null)
    {
        if (failureThreshold < 1) throw new ArgumentException("FailureThreshold must be >= 1", nameof(failureThreshold));
        if (openDurationSeconds < 1) throw new ArgumentException("OpenDurationSeconds must be >= 1", nameof(openDurationSeconds));
        if (successThreshold < 1) throw new ArgumentException("SuccessThreshold must be >= 1", nameof(successThreshold));

        CircuitBreakerFailureThreshold = failureThreshold;
        CircuitBreakerOpenDurationSeconds = openDurationSeconds;
        CircuitBreakerSuccessThreshold = successThreshold;
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = updatedBy;
    }

    public void UpdateBudgetLimits(decimal dailyBudgetUsd, decimal monthlyBudgetUsd, Guid? updatedBy = null)
    {
        if (dailyBudgetUsd < 0) throw new ArgumentException("DailyBudgetUsd cannot be negative", nameof(dailyBudgetUsd));
        if (monthlyBudgetUsd < 0) throw new ArgumentException("MonthlyBudgetUsd cannot be negative", nameof(monthlyBudgetUsd));
        if (dailyBudgetUsd > monthlyBudgetUsd) throw new ArgumentException("DailyBudgetUsd cannot exceed MonthlyBudgetUsd", nameof(dailyBudgetUsd));

        DailyBudgetUsd = dailyBudgetUsd;
        MonthlyBudgetUsd = monthlyBudgetUsd;
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = updatedBy;
    }

    public void UpdateFallbackChain(string fallbackChainJson, Guid? updatedBy = null)
    {
        FallbackChainJson = fallbackChainJson ?? throw new ArgumentNullException(nameof(fallbackChainJson));
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = updatedBy;
    }
}
