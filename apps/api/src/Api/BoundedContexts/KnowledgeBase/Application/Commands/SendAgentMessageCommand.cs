using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Streaming command for sending a message to a standalone agent (non-session).
/// Returns SSE events: Token(s) → Complete
/// Issue #4126: API Integration for Agent Chat
/// Issue #4386: SSE Stream → ChatThread Persistence Hook
/// </summary>
/// <summary>
/// Issue #5580: GameSessionId = optional live game session ID for session-aware RAG filtering.
/// </summary>
internal record SendAgentMessageCommand(
    Guid AgentId,
    string UserQuestion,
    Guid UserId,
    Guid? ChatThreadId = null,
    string? UserRole = null,
    Guid? GameSessionId = null
) : IStreamingQuery<RagStreamingEvent>;
