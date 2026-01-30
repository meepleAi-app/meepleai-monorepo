using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using System.Globalization;
using System.Text;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Implementation of IAgentPromptBuilder for game state context injection.
/// Issue #3184 (AGT-010): Session-Based Agent Lifecycle.
/// </summary>
internal sealed class AgentPromptBuilder : IAgentPromptBuilder
{
    public string BuildSessionPrompt(
        string typologyName,
        string gameTitle,
        GameState gameState,
        string userQuestion)
    {
        ArgumentNullException.ThrowIfNull(typologyName);
        ArgumentNullException.ThrowIfNull(gameTitle);
        ArgumentNullException.ThrowIfNull(gameState);
        ArgumentNullException.ThrowIfNull(userQuestion);

        var prompt = new StringBuilder();
        prompt.AppendLine(CultureInfo.InvariantCulture, $"You are a {typologyName} for {gameTitle}.");
        prompt.AppendLine();
        prompt.AppendLine(CultureInfo.InvariantCulture, $"Current game situation:");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"- Turn: {gameState.CurrentTurn}");
        prompt.AppendLine(CultureInfo.InvariantCulture, $"- Active player ID: {gameState.ActivePlayer}");

        if (gameState.PlayerScores.Count > 0)
        {
            var scoresText = string.Join(", ", gameState.PlayerScores.Select(kvp => $"Player {kvp.Key}: {kvp.Value}"));
            prompt.AppendLine(CultureInfo.InvariantCulture, $"- Scores: {scoresText}");
        }

        if (!string.IsNullOrWhiteSpace(gameState.GamePhase))
        {
            prompt.AppendLine(CultureInfo.InvariantCulture, $"- Phase: {gameState.GamePhase}");
        }

        if (!string.IsNullOrWhiteSpace(gameState.LastAction))
        {
            prompt.AppendLine(CultureInfo.InvariantCulture, $"- Last action: {gameState.LastAction}");
        }

        prompt.AppendLine();
        prompt.AppendLine(CultureInfo.InvariantCulture, $"User question: {userQuestion}");

        return prompt.ToString();
    }
}
