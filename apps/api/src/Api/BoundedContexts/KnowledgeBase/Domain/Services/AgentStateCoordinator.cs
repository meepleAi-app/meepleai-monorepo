using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Coordinates shared state across multiple agents (Tutor, Arbitro, Decisore).
/// Issue #4337: Agent State Coordination - Shared Context Management.
/// </summary>
/// <remarks>
/// Simplified initial implementation. Future enhancements:
/// - Load conversation history from ConversationMemory
/// - Load game snapshots from AgentGameStateSnapshot
/// - Track agent sessions for handoff
/// </remarks>
internal sealed class AgentStateCoordinator
{
    private readonly ILogger<AgentStateCoordinator> _logger;

    public AgentStateCoordinator(ILogger<AgentStateCoordinator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets shared context for an agent invocation.
    /// </summary>
    public Task<SharedAgentContext> GetSharedContextAsync(
        Guid sessionId,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[StateCoordinator] Loading shared context: session={SessionId}, game={GameId}",
            sessionId,
            gameId);

        var context = new SharedAgentContext(
            SessionId: sessionId,
            GameId: gameId,
            ConversationHistory: new List<string>(),
            CurrentGameState: null,
            LastAgentUsed: null,
            StateVersion: 1
        );

        return Task.FromResult(context);
    }

    /// <summary>
    /// Performs context handoff from one agent to another.
    /// </summary>
    public void HandoffContext(string fromAgent, string toAgent, SharedAgentContext context)
    {
        _logger.LogInformation(
            "[StateCoordinator] Context handoff: from={From}, to={To}, session={SessionId}, version={Version}",
            fromAgent,
            toAgent,
            context.SessionId,
            context.StateVersion);

        // Handoff is implicit via shared context retrieval
        // Both agents access same ConversationMemory + GameStateSnapshot
        // Future: Add explicit handoff events for audit trail
    }
}

/// <summary>
/// Shared context available to all agents.
/// </summary>
internal sealed record SharedAgentContext(
    Guid SessionId,
    Guid GameId,
    List<string> ConversationHistory,
    string? CurrentGameState,
    string? LastAgentUsed,
    int StateVersion
);
