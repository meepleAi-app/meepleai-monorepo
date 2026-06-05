using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1: Numeric points scoring (Wingspan, Catan, Azul, etc.).
/// Winner = player with highest points. Ties = null winner.
/// </summary>
public sealed class PointsScoringStrategy : IScoringStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ScoreType Type => ScoreType.Points;

    public ScoringValidationResult Validate(string scoreDataJson)
    {
        if (string.IsNullOrWhiteSpace(scoreDataJson))
            return ScoringValidationResult.Failed("scoreDataJson cannot be empty");

        PointsScoreData? data;
        try
        {
            data = JsonSerializer.Deserialize<PointsScoreData>(scoreDataJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return ScoringValidationResult.Failed($"Invalid JSON: {ex.Message}");
        }

        if (data?.Scores is null || data.Scores.Count == 0)
            return ScoringValidationResult.Failed("No scores provided");

        var errors = new List<string>();
        if (data.Scores.Any(s => s.Points < 0))
            errors.Add("Points must be non-negative");
        if (data.Scores.Any(s => s.PlayerId == Guid.Empty))
            errors.Add("PlayerId cannot be empty");
        if (data.Scores.GroupBy(s => s.PlayerId).Any(g => g.Count() > 1))
            errors.Add("Duplicate playerId in scores");

        return errors.Count > 0
            ? ScoringValidationResult.Failed(errors)
            : ScoringValidationResult.Valid();
    }

    public string Serialize(object scoreData)
    {
        if (scoreData is not PointsScoreData data)
            throw new ArgumentException(
                $"Expected PointsScoreData, got {scoreData?.GetType().Name ?? "null"}",
                nameof(scoreData));
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public object Deserialize(string scoreDataJson)
    {
        var data = JsonSerializer.Deserialize<PointsScoreData>(scoreDataJson, JsonOptions)
            ?? throw new JsonException("Failed to deserialize PointsScoreData");
        return data;
    }

    public Guid? ComputeWinnerPlayerId(string scoreDataJson)
    {
        var data = (PointsScoreData)Deserialize(scoreDataJson);
        if (data.Scores.Count == 0) return null;

        var maxPoints = data.Scores.Max(s => s.Points);
        var winners = data.Scores.Where(s => s.Points == maxPoints).ToList();
        return winners.Count == 1 ? winners[0].PlayerId : null; // tie = null
    }
}

/// <summary>
/// JSON payload for ScoreType.Points sessions.
/// </summary>
public record PointsScoreData(IReadOnlyList<PlayerScore> Scores);

/// <summary>
/// Single player's numeric points entry.
/// </summary>
public record PlayerScore(Guid PlayerId, int Points);
