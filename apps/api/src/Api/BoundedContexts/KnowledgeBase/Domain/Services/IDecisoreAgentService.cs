using Api.BoundedContexts.KnowledgeBase.Application.DTOs.Decisore;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for strategic game analysis and move suggestion.
/// Issue #3769: Core Decisore Agent orchestrating analysis pipeline.
/// </summary>
public interface IDecisoreAgentService
{
    /// <summary>
    /// Analyzes current game position and suggests optimal moves with reasoning.
    /// </summary>
    /// <param name="state">Parsed game state</param>
    /// <param name="playerColor">Player to analyze for ("White" or "Black")</param>
    /// <param name="maxSuggestions">Number of move suggestions to return (1-10)</param>
    /// <param name="useEnsemble">Use multi-model ensemble for deep analysis</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Strategic analysis with ranked move suggestions</returns>
    Task<StrategicAnalysisResultDto> AnalyzePositionAsync(
        ParsedGameState state,
        string playerColor,
        int maxSuggestions = 3,
        bool useEnsemble = false,
        CancellationToken cancellationToken = default);
}
