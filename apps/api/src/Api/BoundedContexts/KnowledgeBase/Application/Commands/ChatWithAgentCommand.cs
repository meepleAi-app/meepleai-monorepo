using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Streaming command for chatting with a standalone agent (non-session).
/// Returns SSE events: Token(s) → Complete
/// Issue #4126: API Integration for Agent Chat
/// </summary>
internal record ChatWithAgentCommand(
    Guid AgentId,
    string UserQuestion
) : IStreamingQuery<RagStreamingEvent>;
