using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Admin-only streaming query for RAG Q&amp;A with real-time pipeline debug events.
/// Returns normal streaming events interleaved with DebugXxx events for pipeline tracing.
/// Separate from StreamQaQuery to avoid any risk to the production code path.
/// </summary>
/// <param name="GameId">The game ID to ask question about</param>
/// <param name="Query">The user's question</param>
/// <param name="ThreadId">Optional chat thread ID for context</param>
/// <param name="DocumentIds">Optional document IDs to filter sources (null = all documents)</param>
/// <param name="StrategyOverride">Optional RAG strategy name to override default selection</param>
internal record StreamDebugQaQuery(
    string GameId,
    string Query,
    Guid? ThreadId = null,
    IReadOnlyList<Guid>? DocumentIds = null,
    string? StrategyOverride = null
) : IStreamingQuery<RagStreamingEvent>;
