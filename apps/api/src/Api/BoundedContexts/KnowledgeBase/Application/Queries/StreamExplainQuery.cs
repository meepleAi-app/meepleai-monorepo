using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Streaming query for RAG-based explain feature.
/// Returns progressive events: StateUpdate → Citations → Outline → ScriptChunk(s) → Complete
/// </summary>
/// <param name="GameId">The game ID to explain topic for</param>
/// <param name="Topic">The topic to explain</param>
internal record StreamExplainQuery(
    string GameId,
    string Topic
) : IStreamingQuery<RagStreamingEvent>;
