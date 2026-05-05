using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Command for the fast 1-click "Tutor" onboarding flow consumed by the frontend
/// <c>agentsClient.quickCreateTutor</c> helper. Issue #659 (Phase δ.1).
/// </summary>
/// <remarks>
/// Orchestration: <see cref="QuickCreateAgentCommandHandler"/> reuses
/// <c>CreateUserAgentCommand</c> (β.2) with hardcoded <c>AgentType = "Tutor"</c>
/// and the default <c>HybridSearch</c> strategy. The agent name is auto-derived
/// as <c>"Tutor for {GameName}"</c> (resolved from the SharedGame catalog).
/// MVP scope:
/// <list type="bullet">
///   <item><c>ChatThreadId</c> placeholder Guid (chat-thread BC integration deferred).</item>
///   <item><c>KbCardCount</c> = 0 (KB query deferred — separate followup if needed).</item>
/// </list>
/// </remarks>
internal sealed record QuickCreateAgentCommand(
    Guid UserId,
    Guid GameId
) : IRequest<QuickCreateAgentResult>;

/// <summary>
/// Result of <see cref="QuickCreateAgentCommand"/>. Mirrors the frontend
/// <c>QuickCreateResultSchema</c> shape consumed by <c>agentsClient.quickCreateTutor</c>.
/// </summary>
internal sealed record QuickCreateAgentResult(
    Guid AgentId,
    Guid ChatThreadId,
    string AgentName,
    int KbCardCount
);
