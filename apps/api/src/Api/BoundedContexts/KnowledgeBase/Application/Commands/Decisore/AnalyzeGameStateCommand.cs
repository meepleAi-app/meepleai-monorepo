using Api.BoundedContexts.KnowledgeBase.Application.DTOs.Decisore;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.Decisore;

/// <summary>
/// Command to analyze game state and get strategic move suggestions.
/// Issue #3769: Decisore Agent strategic analysis.
/// </summary>
public sealed record AnalyzeGameStateCommand(
    Guid GameSessionId,
    string PlayerName,
    string AnalysisDepth = "standard",  // "quick", "standard", "deep"
    int MaxSuggestions = 3
) : IRequest<StrategicAnalysisResultDto>;
