using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Domain.Events;

namespace Api.BoundedContexts.KnowledgeBase.Application.EventHandlers.Shared;

/// <summary>
/// Helper class for mapping GST events to AgentSession GameState updates.
/// Provides pure functions for state transformations based on domain events.
/// </summary>
internal static class AgentStateMapper
{
    /// <summary>
    /// Updates player scores in GameState from ScoreUpdatedEvent.
    /// </summary>
    /// <param name="currentState">Current GameState from AgentSession.</param>
    /// <param name="scoreEvent">ScoreUpdatedEvent containing updated score.</param>
    /// <returns>New GameState with updated player scores.</returns>
    public static GameState UpdateScores(GameState currentState, ScoreUpdatedEvent scoreEvent)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(scoreEvent);

        // Create mutable copy for updates
        var updatedScores = new Dictionary<Guid, decimal>(currentState.PlayerScores);

        // Update or add participant score (preserve decimal precision)
        updatedScores[scoreEvent.ParticipantId] = scoreEvent.NewScore;

        // Build last action description
        var lastAction = scoreEvent.RoundNumber.HasValue
            ? $"Score updated to {scoreEvent.NewScore} (Round {scoreEvent.RoundNumber.Value})"
            : $"Score updated to {scoreEvent.NewScore}";

        if (!string.IsNullOrWhiteSpace(scoreEvent.Category))
            lastAction += $" - {scoreEvent.Category}";

        return GameState.Create(
            currentTurn: currentState.CurrentTurn,
            activePlayer: currentState.ActivePlayer,
            playerScores: updatedScores,
            gamePhase: currentState.GamePhase,
            lastAction: lastAction);
    }

    /// <summary>
    /// Updates turn progression in GameState from TurnAdvancedEvent.
    /// </summary>
    /// <param name="currentState">Current GameState from AgentSession.</param>
    /// <param name="turnEvent">TurnAdvancedEvent containing turn progression.</param>
    /// <returns>New GameState with updated turn and active player.</returns>
    public static GameState UpdateTurn(GameState currentState, TurnAdvancedEvent turnEvent)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(turnEvent);

        // Note: TurnAdvancedEvent uses string player names, not Guids
        // We maintain the current ActivePlayer Guid and increment turn count
        var lastAction = !string.IsNullOrWhiteSpace(turnEvent.PreviousPlayer)
            ? $"Turn advanced from {turnEvent.PreviousPlayer} to {turnEvent.CurrentPlayer}"
            : $"Turn advanced to {turnEvent.CurrentPlayer}";

        return GameState.Create(
            currentTurn: currentState.CurrentTurn + 1,
            activePlayer: currentState.ActivePlayer, // Keep current (event uses string names)
            playerScores: new Dictionary<Guid, decimal>(currentState.PlayerScores),
            gamePhase: currentState.GamePhase,
            lastAction: lastAction);
    }

    /// <summary>
    /// Updates game phase in GameState from GamePhaseChangedEvent.
    /// </summary>
    /// <param name="currentState">Current GameState from AgentSession.</param>
    /// <param name="phaseEvent">GamePhaseChangedEvent containing phase transition.</param>
    /// <returns>New GameState with updated game phase.</returns>
    public static GameState UpdatePhase(GameState currentState, GamePhaseChangedEvent phaseEvent)
    {
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(phaseEvent);

        var lastAction = $"Phase changed from {phaseEvent.OldPhase} to {phaseEvent.NewPhase}";

        return GameState.Create(
            currentTurn: currentState.CurrentTurn,
            activePlayer: currentState.ActivePlayer,
            playerScores: new Dictionary<Guid, decimal>(currentState.PlayerScores),
            gamePhase: phaseEvent.NewPhase.ToString(),
            lastAction: lastAction);
    }
}
