using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Streaming query for generating step-by-step setup guides.
/// Returns progressive events: StateUpdate → SetupStep(s) → Complete
/// AI-03: RAG-based setup guide generation with streaming delivery
/// </summary>
/// <param name="GameId">The game ID to generate setup guide for</param>
/// <param name="PlayerCount">Number of players (0 = generic setup, >0 = adapted to player count)</param>
internal record StreamSetupGuideQuery(
    string GameId,
    int PlayerCount = 0
) : IStreamingQuery<RagStreamingEvent>;
