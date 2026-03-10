namespace Api.Infrastructure.Entities.SystemConfiguration;

/// <summary>
/// Issue #5498: Persistence entity for LLM system configuration.
/// Maps to SystemConfiguration.LlmSystemConfigs table.
/// </summary>
public sealed class LlmSystemConfigEntity
{
    public Guid Id { get; set; }

    // Circuit breaker
    public int CircuitBreakerFailureThreshold { get; set; }
    public int CircuitBreakerOpenDurationSeconds { get; set; }
    public int CircuitBreakerSuccessThreshold { get; set; }

    // Budget
    public decimal DailyBudgetUsd { get; set; }
    public decimal MonthlyBudgetUsd { get; set; }

    // Fallback chain (JSON)
    public string FallbackChainJson { get; set; } = "[]";

    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
