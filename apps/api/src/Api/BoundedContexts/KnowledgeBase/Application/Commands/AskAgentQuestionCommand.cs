using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command to ask a question to the agent with selectable search strategy
/// POC: Agent default behavior evaluation with cost/token tracking
/// </summary>
public record AskAgentQuestionCommand : IRequest<AgentChatResponse>
{
    /// <summary>
    /// User question
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Search strategy (RetrievalOnly, SingleModel, MultiModelConsensus)
    /// </summary>
    public required AgentSearchStrategy Strategy { get; init; }

    /// <summary>
    /// Optional: Session ID for conversation continuity
    /// </summary>
    public string? SessionId { get; init; }

    /// <summary>
    /// Optional: Game ID to filter PDF documents
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Optional: User ID for content-gating (ownership check).
    /// When provided, non-owners see reference-only sources (no text/images).
    /// </summary>
    public Guid? UserId { get; init; }

    /// <summary>
    /// Optional: Live game session ID for session-aware RAG filtering.
    /// When provided, vector search filters by all game IDs from the session context
    /// (primary game + expansions) instead of a single GameId.
    /// Issue #5580: Session-aware RAG chat.
    /// </summary>
    public Guid? GameSessionId { get; init; }

    /// <summary>
    /// Optional: Language filter (en, it, de, fr, es)
    /// </summary>
    public string? Language { get; init; }

    /// <summary>
    /// Top-K results to retrieve from vector search
    /// </summary>
    public int TopK { get; init; } = 5;

    /// <summary>
    /// Minimum relevance score threshold (0-1)
    /// </summary>
    public double MinScore { get; init; } = 0.6;
}
