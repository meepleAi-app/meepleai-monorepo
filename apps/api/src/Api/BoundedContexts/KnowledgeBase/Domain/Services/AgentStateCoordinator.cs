using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Coordinates shared state across multiple agents (Tutor, Arbitro, Decisore).
/// Issue #4337: Agent State Coordination - Shared Context Management.
/// </summary>
/// <remarks>
/// Loads conversation history from ConversationMemory, game state from
/// AgentGameStateSnapshot, and provides context handoff between agents.
/// Uses optimistic concurrency via state versioning.
/// </remarks>
internal sealed class AgentStateCoordinator
{
    private readonly IConversationMemoryRepository _conversationMemoryRepository;
    private readonly IAgentGameStateSnapshotRepository _gameStateSnapshotRepository;
    private readonly ILogger<AgentStateCoordinator> _logger;

    private const int MaxConversationHistory = 10;

    public AgentStateCoordinator(
        IConversationMemoryRepository conversationMemoryRepository,
        IAgentGameStateSnapshotRepository gameStateSnapshotRepository,
        ILogger<AgentStateCoordinator> logger)
    {
        _conversationMemoryRepository = conversationMemoryRepository ?? throw new ArgumentNullException(nameof(conversationMemoryRepository));
        _gameStateSnapshotRepository = gameStateSnapshotRepository ?? throw new ArgumentNullException(nameof(gameStateSnapshotRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Loads shared context for an agent invocation from persistent storage.
    /// Includes conversation history and latest game state snapshot.
    /// </summary>
    public async Task<SharedAgentContext> GetSharedContextAsync(
        Guid sessionId,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[StateCoordinator] Loading shared context: session={SessionId}, game={GameId}",
            sessionId,
            gameId);

        // Load conversation history from ConversationMemory
        var conversationHistory = new List<string>();
        try
        {
            var memories = await _conversationMemoryRepository
                .GetBySessionIdAsync(sessionId, MaxConversationHistory, cancellationToken)
                .ConfigureAwait(false);

            conversationHistory = memories
                .Select(m => $"[{m.MessageType}] {m.Content}")
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[StateCoordinator] Failed to load conversation history for session {SessionId}",
                sessionId);
        }

        // Load latest game state snapshot
        string? currentGameState = null;
        int stateVersion = 1;
        try
        {
            var latestSnapshot = await _gameStateSnapshotRepository
                .GetLatestByGameIdAsync(gameId, cancellationToken)
                .ConfigureAwait(false);

            if (latestSnapshot != null)
            {
                currentGameState = latestSnapshot.BoardStateJson;
                stateVersion = latestSnapshot.TurnNumber;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[StateCoordinator] Failed to load game state for game {GameId}",
                gameId);
        }

        var context = new SharedAgentContext(
            SessionId: sessionId,
            GameId: gameId,
            ConversationHistory: conversationHistory,
            CurrentGameState: currentGameState,
            LastAgentUsed: null,
            StateVersion: stateVersion
        );

        _logger.LogInformation(
            "[StateCoordinator] Loaded context: session={SessionId}, historyCount={HistoryCount}, hasGameState={HasState}, version={Version}",
            sessionId,
            conversationHistory.Count,
            currentGameState != null,
            stateVersion);

        return context;
    }

    /// <summary>
    /// Performs context handoff from one agent to another.
    /// Logs the transition for audit trail and monitoring.
    /// </summary>
    public SharedAgentContext HandoffContext(
        string fromAgent,
        string toAgent,
        SharedAgentContext context)
    {
        if (string.IsNullOrWhiteSpace(fromAgent))
            throw new ArgumentException("Source agent name cannot be empty", nameof(fromAgent));
        if (string.IsNullOrWhiteSpace(toAgent))
            throw new ArgumentException("Target agent name cannot be empty", nameof(toAgent));
        ArgumentNullException.ThrowIfNull(context);

        _logger.LogInformation(
            "[StateCoordinator] Context handoff: from={From}, to={To}, session={SessionId}, version={Version}, historyCount={HistoryCount}",
            fromAgent,
            toAgent,
            context.SessionId,
            context.StateVersion,
            context.ConversationHistory.Count);

        // Create new context with updated agent and incremented version
        return context with
        {
            LastAgentUsed = fromAgent,
            StateVersion = context.StateVersion + 1
        };
    }

    /// <summary>
    /// Validates that the context version matches expected version (optimistic concurrency).
    /// </summary>
    public bool ValidateStateVersion(SharedAgentContext context, int expectedVersion)
    {
        if (context.StateVersion != expectedVersion)
        {
            _logger.LogWarning(
                "[StateCoordinator] State version mismatch: expected={Expected}, actual={Actual}, session={SessionId}",
                expectedVersion,
                context.StateVersion,
                context.SessionId);
            return false;
        }

        return true;
    }
}

/// <summary>
/// Shared context available to all agents with conversation history and game state.
/// Immutable record for thread-safety; versioned for optimistic concurrency.
/// </summary>
internal sealed record SharedAgentContext(
    Guid SessionId,
    Guid GameId,
    List<string> ConversationHistory,
    string? CurrentGameState,
    string? LastAgentUsed,
    int StateVersion
);
