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
/// <remarks>
/// #4169: le soglie del circuit breaker e la catena di fallback sono opzionali per lo stesso motivo.
/// Il runtime non le legge (le soglie vengono da <c>AI:CircuitBreaker</c> di appsettings, l'ordine di
/// fallback dal catalogo modelli), e la pagina admin non le manda più.
/// </remarks>
internal record UpdateLlmSystemConfigCommand(
    decimal DailyBudgetUsd,
    decimal MonthlyBudgetUsd,
    Guid UpdatedByUserId,
    int? CircuitBreakerFailureThreshold = null,
    int? CircuitBreakerOpenDurationSeconds = null,
    int? CircuitBreakerSuccessThreshold = null,
    string? FallbackChainJson = null,
    int? DegradeThresholdPercent = null,
    int? BackgroundQuotaPercent = null,
    int? EvaluationQuotaPercent = null) : ICommand<LlmSystemConfigDto>;
