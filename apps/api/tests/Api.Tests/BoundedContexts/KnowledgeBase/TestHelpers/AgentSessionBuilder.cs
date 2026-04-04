using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.Tests.BoundedContexts.KnowledgeBase.TestHelpers;

/// <summary>
/// Builder for creating AgentSession test instances with sensible defaults.
/// </summary>
/// <remarks>
/// <para><strong>FK Dependencies:</strong></para>
/// <para>
/// AgentSession requires 4 parent entities (all with valid Guid FK):
/// - AgentDefinition (AgentDefinitionId)
/// - User (UserId)
/// - Game (GameId)
/// - GameSession (GameSessionId)
/// </para>
///
/// <para><strong>Usage:</strong></para>
/// <code>
/// // With default dependencies (random GUIDs)
/// var session = new AgentSessionBuilder().Build();
///
/// // With specific dependencies
/// var session = new AgentSessionBuilder()
///     .WithAgentDefinitionId(agentDefinitionId)
///     .WithUserId(userId)
///     .WithGameId(gameId)
///     .WithGameSessionId(gameSessionId)
///     .Build();
///
/// // With initial game state
/// var session = new AgentSessionBuilder()
///     .WithInitialState(GameState.Create(1, playerId, scores, "setup", "started"))
///     .Build();
/// </code>
/// </remarks>
internal class AgentSessionBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _agentDefinitionId = Guid.NewGuid();
    private Guid _gameSessionId = Guid.NewGuid();
    private Guid _userId = Guid.NewGuid();
    private Guid _gameId = Guid.NewGuid();
    private GameState? _initialState;
    private bool _shouldEnd;

    /// <summary>
    /// Sets the session ID.
    /// </summary>
    public AgentSessionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the agent definition ID (FK to agent_definitions table).
    /// </summary>
    public AgentSessionBuilder WithAgentDefinitionId(Guid agentDefinitionId)
    {
        _agentDefinitionId = agentDefinitionId;
        return this;
    }

    /// <summary>
    /// Sets the game session ID (FK to game_sessions table).
    /// </summary>
    public AgentSessionBuilder WithGameSessionId(Guid gameSessionId)
    {
        _gameSessionId = gameSessionId;
        return this;
    }

    /// <summary>
    /// Sets the user ID (FK to users table).
    /// </summary>
    public AgentSessionBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Sets the game ID (FK to games table).
    /// </summary>
    public AgentSessionBuilder WithGameId(Guid gameId)
    {
        _gameId = gameId;
        return this;
    }

    /// <summary>
    /// Sets the initial game state.
    /// </summary>
    /// <param name="initialState">GameState to start the session with.</param>
    public AgentSessionBuilder WithInitialState(GameState initialState)
    {
        _initialState = initialState;
        return this;
    }

    /// <summary>
    /// Sets the session to ended status.
    /// </summary>
    public AgentSessionBuilder ThatIsEnded()
    {
        _shouldEnd = true;
        return this;
    }

    /// <summary>
    /// Creates a session with minimal default state (setup phase, no scores).
    /// </summary>
    public AgentSessionBuilder WithMinimalState()
    {
        _initialState = GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, decimal>(),
            gamePhase: "setup",
            lastAction: "session started");
        return this;
    }

    /// <summary>
    /// Creates a session with in-progress state (active game, some scores).
    /// </summary>
    public AgentSessionBuilder WithInProgressState(int turn = 3)
    {
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var scores = new Dictionary<Guid, decimal>
        {
            { player1Id, 10m },
            { player2Id, 15m }
        };

        _initialState = GameState.Create(
            currentTurn: turn,
            activePlayer: player1Id,
            playerScores: scores,
            gamePhase: "playing",
            lastAction: $"turn {turn} in progress");
        return this;
    }

    /// <summary>
    /// Creates a session with completed state (final scores, game ended).
    /// </summary>
    public AgentSessionBuilder WithCompletedState()
    {
        var player1Id = Guid.NewGuid();
        var player2Id = Guid.NewGuid();
        var scores = new Dictionary<Guid, decimal>
        {
            { player1Id, 42m },
            { player2Id, 38m }
        };

        _initialState = GameState.Create(
            currentTurn: 10,
            activePlayer: player1Id,
            playerScores: scores,
            gamePhase: "completed",
            lastAction: "game ended");

        _shouldEnd = true;
        return this;
    }

    /// <summary>
    /// Builds the AgentSession instance.
    /// </summary>
    public AgentSession Build()
    {
        // Use minimal state if none specified
        var state = _initialState ?? GameState.Create(
            currentTurn: 1,
            activePlayer: Guid.NewGuid(),
            playerScores: new Dictionary<Guid, decimal>(),
            gamePhase: "setup",
            lastAction: "session started");

        var session = new AgentSession(
            _id,
            _agentDefinitionId,
            _gameSessionId,
            _userId,
            _gameId,
            state);

        if (_shouldEnd)
        {
            session.End();
        }

        return session;
    }

    /// <summary>
    /// Implicit conversion to AgentSession for convenience.
    /// </summary>
    public static implicit operator AgentSession(AgentSessionBuilder builder) => builder.Build();
}
