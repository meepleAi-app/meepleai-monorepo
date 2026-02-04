using Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.ContextEngineering.Commands;

/// <summary>
/// Command to assemble context from multiple sources for AI agent prompts.
/// Issue #3491: Context Engineering Framework Implementation.
/// </summary>
/// <remarks>
/// Orchestrates multi-source context retrieval with:
/// - Priority-based token allocation
/// - Source-specific retrieval strategies
/// - Adaptive budget management
/// - Quality metrics tracking
/// </remarks>
/// <param name="Query">User's query for context relevance</param>
/// <param name="GameId">Optional game context filter</param>
/// <param name="UserId">Optional user context filter</param>
/// <param name="SessionId">Optional session context filter</param>
/// <param name="MaxTotalTokens">Maximum tokens for assembled context (default: 8000)</param>
/// <param name="MinRelevance">Minimum relevance score threshold (0.0-1.0, default: 0.5)</param>
/// <param name="SourcePriorities">Optional priority overrides by source ID (0-100)</param>
/// <param name="MinTokensPerSource">Optional minimum token guarantees per source</param>
/// <param name="MaxTokensPerSource">Optional maximum token caps per source</param>
/// <param name="IncludeEmbedding">Whether to generate query embedding for semantic search</param>
internal sealed record AssembleContextCommand(
    string Query,
    Guid? GameId = null,
    Guid? UserId = null,
    Guid? SessionId = null,
    int MaxTotalTokens = 8000,
    double MinRelevance = 0.5,
    IDictionary<string, int>? SourcePriorities = null,
    IDictionary<string, int>? MinTokensPerSource = null,
    IDictionary<string, int>? MaxTokensPerSource = null,
    bool IncludeEmbedding = true
) : IRequest<AssembledContextDto>;
