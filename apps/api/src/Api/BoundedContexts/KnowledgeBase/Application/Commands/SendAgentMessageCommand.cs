using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Streaming command for sending a message to a standalone agent (non-session).
/// Returns SSE events: Token(s) → Complete
/// Issue #4126: API Integration for Agent Chat
/// </summary>
internal record SendAgentMessageCommand(
    Guid AgentId,
    string UserQuestion
) : IStreamingQuery<RagStreamingEvent>;
