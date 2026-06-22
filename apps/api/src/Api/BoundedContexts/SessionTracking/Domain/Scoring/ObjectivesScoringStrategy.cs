using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1: Objectives-based scoring (T.I.M.E. Stories, Sherlock Holmes Consulting Detective).
/// Each player has a list of completed objective names. Winner = player with most objectives.
/// Ties (multiple players with same highest count) = null winner.
/// </summary>
public sealed class ObjectivesScoringStrategy : IScoringStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ScoreType Type => ScoreType.Objectives;

    public ScoringValidationResult Validate(string scoreDataJson)
    {
        if (string.IsNullOrWhiteSpace(scoreDataJson))
            return ScoringValidationResult.Failed("scoreDataJson cannot be empty");

        ObjectivesScoreData? data;
        try
        {
            data = JsonSerializer.Deserialize<ObjectivesScoreData>(scoreDataJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return ScoringValidationResult.Failed($"Invalid JSON: {ex.Message}");
        }

        if (data?.CompletedByPlayer is null || data.CompletedByPlayer.Count == 0)
            return ScoringValidationResult.Failed("No players provided");

        var errors = new List<string>();
        if (data.CompletedByPlayer.Any(p => p.PlayerId == Guid.Empty))
            errors.Add("PlayerId cannot be empty");
        if (data.CompletedByPlayer.GroupBy(p => p.PlayerId).Any(g => g.Count() > 1))
            errors.Add("Duplicate playerId in completedByPlayer");
        // Objectives list può essere empty per player (player non ha completato nulla = OK semanticamente)
        if (data.CompletedByPlayer.Any(p => p.Objectives is null))
            errors.Add("Objectives list cannot be null (use empty list)");
        if (data.CompletedByPlayer.Any(p => p.Objectives is not null && p.Objectives.Any(string.IsNullOrWhiteSpace)))
            errors.Add("Objective names cannot be empty/whitespace");

        return errors.Count > 0
            ? ScoringValidationResult.Failed(errors)
            : ScoringValidationResult.Valid();
    }

    public string Serialize(object scoreData)
    {
        if (scoreData is not ObjectivesScoreData data)
            throw new ArgumentException(
                $"Expected ObjectivesScoreData, got {scoreData?.GetType().Name ?? "null"}",
                nameof(scoreData));
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public object Deserialize(string scoreDataJson)
    {
        var data = JsonSerializer.Deserialize<ObjectivesScoreData>(scoreDataJson, JsonOptions)
            ?? throw new JsonException("Failed to deserialize ObjectivesScoreData");
        return data;
    }

    public Guid? ComputeWinnerPlayerId(string scoreDataJson)
    {
        var data = (ObjectivesScoreData)Deserialize(scoreDataJson);
        if (data.CompletedByPlayer.Count == 0) return null;

        var maxCount = data.CompletedByPlayer.Max(p => p.Objectives.Count);
        var winners = data.CompletedByPlayer.Where(p => p.Objectives.Count == maxCount).ToList();
        return winners.Count == 1 ? winners[0].PlayerId : null; // tie = null
    }
}

/// <summary>
/// JSON payload for ScoreType.Objectives sessions.
/// </summary>
public record ObjectivesScoreData(IReadOnlyList<PlayerObjectives> CompletedByPlayer);

/// <summary>
/// Single player's completed objective list.
/// </summary>
public record PlayerObjectives(Guid PlayerId, IReadOnlyList<string> Objectives);
