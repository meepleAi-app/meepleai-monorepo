using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1: Ranking-based scoring (racing games, time trials).
/// Each player has integer position 1..N. Winner = player at position 1.
/// Validation: distinct positions, sequential 1..N (no gaps), N == player count.
/// </summary>
public sealed class RankingScoringStrategy : IScoringStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ScoreType Type => ScoreType.Ranking;

    public ScoringValidationResult Validate(string scoreDataJson)
    {
        if (string.IsNullOrWhiteSpace(scoreDataJson))
            return ScoringValidationResult.Failed("scoreDataJson cannot be empty");

        RankingScoreData? data;
        try
        {
            data = JsonSerializer.Deserialize<RankingScoreData>(scoreDataJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return ScoringValidationResult.Failed($"Invalid JSON: {ex.Message}");
        }

        if (data?.Positions is null || data.Positions.Count == 0)
            return ScoringValidationResult.Failed("No positions provided");

        var errors = new List<string>();
        if (data.Positions.Any(p => p.PlayerId == Guid.Empty))
            errors.Add("PlayerId cannot be empty");
        if (data.Positions.GroupBy(p => p.PlayerId).Any(g => g.Count() > 1))
            errors.Add("Duplicate playerId in positions");
        if (data.Positions.Any(p => p.Position < 1))
            errors.Add("Position must be >= 1");

        // Sequential 1..N + distinct (only check if no Position<1 errors, to avoid cascading)
        if (!data.Positions.Any(p => p.Position < 1))
        {
            var positions = data.Positions.Select(p => p.Position).OrderBy(x => x).ToList();
            var expected = Enumerable.Range(1, data.Positions.Count).ToList();
            if (!positions.SequenceEqual(expected))
                errors.Add($"Positions must be sequential 1..{data.Positions.Count}, distinct, no gaps");
        }

        return errors.Count > 0
            ? ScoringValidationResult.Failed(errors)
            : ScoringValidationResult.Valid();
    }

    public string Serialize(object scoreData)
    {
        if (scoreData is not RankingScoreData data)
            throw new ArgumentException(
                $"Expected RankingScoreData, got {scoreData?.GetType().Name ?? "null"}",
                nameof(scoreData));
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public object Deserialize(string scoreDataJson)
    {
        var data = JsonSerializer.Deserialize<RankingScoreData>(scoreDataJson, JsonOptions)
            ?? throw new JsonException("Failed to deserialize RankingScoreData");
        return data;
    }

    public Guid? ComputeWinnerPlayerId(string scoreDataJson)
    {
        var data = (RankingScoreData)Deserialize(scoreDataJson);
        var first = data.Positions.FirstOrDefault(p => p.Position == 1);
        return first?.PlayerId; // distinct positions validated, so always 1 winner if valid
    }
}

/// <summary>
/// JSON payload for ScoreType.Ranking sessions.
/// </summary>
public record RankingScoreData(IReadOnlyList<PlayerRanking> Positions);

/// <summary>
/// Single player's ranking position (1-based).
/// </summary>
public record PlayerRanking(Guid PlayerId, int Position);
