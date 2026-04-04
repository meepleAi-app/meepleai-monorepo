using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Issue #5489: Encapsulates LLM cost logging, budget tracking, and usage stats
/// extracted from HybridLlmService.
/// </summary>
internal interface ILlmCostService
{
    /// <summary>
    /// Log a successful LLM request: cost to DB, model usage stats, user budget,
    /// ledger event, file log, and usage service.
    /// </summary>
    Task LogSuccessAsync(
        LlmCompletionResult result,
        LlmUserContext userContext,
        long latencyMs,
        RequestSource source,
        CancellationToken ct = default);

    /// <summary>
    /// Log a failed LLM request: cost failure to DB and file log.
    /// </summary>
    Task LogFailureAsync(
        string? errorMessage,
        LlmUserContext userContext,
        long latencyMs,
        RequestSource source,
        CancellationToken ct = default);
}
