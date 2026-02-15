using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.Decisore;

/// <summary>
/// Streaming command for Decisore Agent strategic analysis.
/// Returns SSE events: StateUpdate → Progress → Complete
/// Issue #3769: Decisore Agent strategic analysis.
/// Issue #4334: Enhanced with SSE streaming for real-time analysis progress.
/// </summary>
public sealed record AnalyzeGameStateCommand(
    Guid GameSessionId,
    string PlayerName,
    string AnalysisDepth = "standard",  // "quick", "standard", "deep"
    int MaxSuggestions = 3
) : IStreamingQuery<RagStreamingEvent>;
