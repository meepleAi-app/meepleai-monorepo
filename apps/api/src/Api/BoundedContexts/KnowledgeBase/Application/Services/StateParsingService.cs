using System.Text.RegularExpressions;
using Api.BoundedContexts.KnowledgeBase.Domain.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Service for parsing natural language messages to extract game state changes.
/// Issue #2405: Ledger Mode - Full State Tracking.
/// </summary>
internal sealed class StateParsingService
{
    private static readonly Regex ScorePattern = new(
        @"(?:ho|hai|hanno|ha|sono a|adesso ho)\s+(?<score>\d+)\s+(?:punti?|vittoria|VP)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ResourceGainPattern = new(
        @"(?:ho guadagnato|prendo|ottengo|ricevo)\s+(?<amount>\d+)\s+(?<resource>\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ResourceLossPattern = new(
        @"(?:ho perso|pago|spendo|perdo)\s+(?<amount>\d+)\s+(?<resource>\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ActionPattern = new(
        @"(?:ho|hai|ha)\s+(?:costruito|comprato|venduto|giocato)\s+(?<action>.*)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TurnPattern = new(
        @"(?:tocca a|è il turno di|passa il turno a)\s+(?<player>\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PhasePattern = new(
        @"(?:fase di|inizia il|inizia la)\s+(?<phase>\w+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RoadPattern = new(
        @"(?:ho costruito|costruisco)\s+(?:una\s+)?(?<count>\d+)?\s*(?:strada|strade)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parses a message and extracts state changes.
    /// </summary>
    public List<StateChangeInfo> ParseMessage(string message, string? defaultPlayerName = null)
    {
        if (string.IsNullOrWhiteSpace(message))
            return new List<StateChangeInfo>();

        var changes = new List<StateChangeInfo>();

        // Parse score changes
        var scoreMatch = ScorePattern.Match(message);
        if (scoreMatch.Success)
        {
            changes.Add(new StateChangeInfo
            {
                PlayerName = defaultPlayerName,
                ChangeType = "score",
                FieldName = "score",
                NewValue = scoreMatch.Groups["score"].Value,
                ConfidenceScore = 0.9
            });
        }

        // Parse resource gains
        var resourceGainMatch = ResourceGainPattern.Match(message);
        if (resourceGainMatch.Success)
        {
            var resourceName = resourceGainMatch.Groups["resource"].Value.ToLowerInvariant();
            var amount = int.Parse(resourceGainMatch.Groups["amount"].Value);

            changes.Add(new StateChangeInfo
            {
                PlayerName = defaultPlayerName,
                ChangeType = "resource",
                FieldName = resourceName,
                NewValue = amount.ToString(),
                ConfidenceScore = 0.85
            });
        }

        // Parse resource losses
        var resourceLossMatch = ResourceLossPattern.Match(message);
        if (resourceLossMatch.Success)
        {
            var resourceName = resourceLossMatch.Groups["resource"].Value.ToLowerInvariant();
            var amount = -int.Parse(resourceLossMatch.Groups["amount"].Value); // Negative for loss

            changes.Add(new StateChangeInfo
            {
                PlayerName = defaultPlayerName,
                ChangeType = "resource",
                FieldName = resourceName,
                NewValue = amount.ToString(),
                ConfidenceScore = 0.85
            });
        }

        // Parse road construction (Catan-specific)
        var roadMatch = RoadPattern.Match(message);
        if (roadMatch.Success)
        {
            var count = roadMatch.Groups["count"].Success
                ? int.Parse(roadMatch.Groups["count"].Value)
                : 1;

            changes.Add(new StateChangeInfo
            {
                PlayerName = defaultPlayerName,
                ChangeType = "resource",
                FieldName = "strade",
                NewValue = count.ToString(),
                ConfidenceScore = 0.8
            });
        }

        // Parse turn changes
        var turnMatch = TurnPattern.Match(message);
        if (turnMatch.Success)
        {
            changes.Add(new StateChangeInfo
            {
                PlayerName = turnMatch.Groups["player"].Value,
                ChangeType = "turn",
                FieldName = "currentTurn",
                NewValue = turnMatch.Groups["player"].Value,
                ConfidenceScore = 0.95
            });
        }

        // Parse phase changes
        var phaseMatch = PhasePattern.Match(message);
        if (phaseMatch.Success)
        {
            changes.Add(new StateChangeInfo
            {
                PlayerName = null,
                ChangeType = "phase",
                FieldName = "currentPhase",
                NewValue = phaseMatch.Groups["phase"].Value,
                ConfidenceScore = 0.9
            });
        }

        return changes;
    }

    /// <summary>
    /// Attempts to infer player name from message context.
    /// </summary>
    public string? InferPlayerName(string message)
    {
        // Try to extract player name from first-person statements
        var firstPersonPatterns = new[]
        {
            @"^(?:io\s+)?(?<player>\w+)[\s,]+ho",
            @"sono\s+(?<player>\w+)",
            @"mi\s+chiamo\s+(?<player>\w+)"
        };

        foreach (var pattern in firstPersonPatterns)
        {
            var match = Regex.Match(message, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups["player"].Value;
            }
        }

        return null;
    }

    /// <summary>
    /// Detects potential conflicts between current state and parsed changes.
    /// </summary>
    public bool DetectConflict(
        StateChangeInfo change,
        Dictionary<string, object> currentState)
    {
        if (string.IsNullOrWhiteSpace(change.PlayerName))
            return false;

        // Check if player exists in current state
        if (!currentState.ContainsKey(change.PlayerName))
            return false;

        // Confidence threshold for automatic conflict detection
        return change.ConfidenceScore < 0.7;
    }
}
