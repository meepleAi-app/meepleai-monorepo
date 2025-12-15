using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Streaming query for RAG-based Q&A with token-by-token delivery.
/// Returns progressive events: StateUpdate → Citations → Token(s) → Complete
/// CHAT-01: Supports chat context integration via ThreadId
/// Issue #2051: Supports document filtering via DocumentIds
/// </summary>
/// <param name="GameId">The game ID to ask question about</param>
/// <param name="Query">The user's question</param>
/// <param name="ThreadId">Optional chat thread ID for context</param>
/// <param name="DocumentIds">Optional document IDs to filter sources (null = all documents)</param>
internal record StreamQaQuery(
    string GameId,
    string Query,
    Guid? ThreadId = null,
    IReadOnlyList<Guid>? DocumentIds = null
) : IStreamingQuery<RagStreamingEvent>;
