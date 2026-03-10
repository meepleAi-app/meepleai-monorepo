using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateLlmSystemConfig;

/// <summary>
/// Issue #5495: Command to update LLM system configuration (circuit breaker, budget, fallback chain).
/// Returns the updated config DTO.
/// </summary>
internal record UpdateLlmSystemConfigCommand(
    int CircuitBreakerFailureThreshold,
    int CircuitBreakerOpenDurationSeconds,
    int CircuitBreakerSuccessThreshold,
    decimal DailyBudgetUsd,
    decimal MonthlyBudgetUsd,
    string FallbackChainJson,
    Guid UpdatedByUserId) : ICommand<LlmSystemConfigDto>;
