using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Streaming command for chatting with session-based agent.
/// Returns SSE events: StateUpdate → Token(s) → Complete
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// Issue #4386: SSE Stream → ChatThread Persistence Hook
/// Issue #4282: <c>GameSessionId</c> è la sessione di gioco del percorso; il gestore controlla che la
/// sessione agente le appartenga e che <c>UserId</c> l'abbia avviata (<see cref="AgentSessionAccess"/>).
/// </summary>
internal record ChatWithSessionAgentCommand(
    Guid AgentSessionId,
    Guid GameSessionId,
    string UserQuestion,
    Guid UserId,
    GameSessionContext? GameContext = null,
    Guid? ChatThreadId = null
) : IStreamingQuery<RagStreamingEvent>;
