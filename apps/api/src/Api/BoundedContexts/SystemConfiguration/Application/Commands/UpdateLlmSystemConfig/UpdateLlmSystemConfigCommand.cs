using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateLlmSystemConfig;

/// <summary>
/// Issue #5495: Command to update LLM system configuration (circuit breaker, budget, fallback chain).
/// Returns the updated config DTO.
/// </summary>
/// <remarks>
/// #4166: i campi della politica di budget sono opzionali, e uno assente lascia il valore salvato.
/// La pagina admin di oggi non li conosce: un comando a sostituzione completa la farebbe fallire
/// (è il difetto di #4157) o azzererebbe le quote.
/// </remarks>
internal record UpdateLlmSystemConfigCommand(
    int CircuitBreakerFailureThreshold,
    int CircuitBreakerOpenDurationSeconds,
    int CircuitBreakerSuccessThreshold,
    decimal DailyBudgetUsd,
    decimal MonthlyBudgetUsd,
    string FallbackChainJson,
    Guid UpdatedByUserId,
    int? DegradeThresholdPercent = null,
    int? BackgroundQuotaPercent = null,
    int? EvaluationQuotaPercent = null) : ICommand<LlmSystemConfigDto>;
