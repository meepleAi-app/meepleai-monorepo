using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Streaming command for chatting with session-based agent.
/// Returns SSE events: StateUpdate → Token(s) → Complete
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal record ChatWithSessionAgentCommand(
    Guid AgentSessionId,
    string UserQuestion
) : IStreamingQuery<RagStreamingEvent>;
