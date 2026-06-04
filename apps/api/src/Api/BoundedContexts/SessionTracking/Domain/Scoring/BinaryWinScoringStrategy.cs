using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Enums;

namespace Api.BoundedContexts.SessionTracking.Domain.Scoring;

/// <summary>
/// DEC-1: Binary win/loss scoring (cooperative Pandemic, Forbidden Island).
/// Winner = single IsWinner=true player. Cooperative all-win or all-lose = null winner.
/// </summary>
public sealed class BinaryWinScoringStrategy : IScoringStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public ScoreType Type => ScoreType.BinaryWin;

    public ScoringValidationResult Validate(string scoreDataJson)
    {
        if (string.IsNullOrWhiteSpace(scoreDataJson))
            return ScoringValidationResult.Failed("scoreDataJson cannot be empty");

        BinaryWinScoreData? data;
        try
        {
            data = JsonSerializer.Deserialize<BinaryWinScoreData>(scoreDataJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            return ScoringValidationResult.Failed($"Invalid JSON: {ex.Message}");
        }

        if (data?.Results is null || data.Results.Count == 0)
            return ScoringValidationResult.Failed("No results provided");

        var errors = new List<string>();
        if (data.Results.Any(r => r.PlayerId == Guid.Empty))
            errors.Add("PlayerId cannot be empty");
        if (data.Results.GroupBy(r => r.PlayerId).Any(g => g.Count() > 1))
            errors.Add("Duplicate playerId in results");

        return errors.Count > 0
            ? ScoringValidationResult.Failed(errors)
            : ScoringValidationResult.Valid();
    }

    public string Serialize(object scoreData)
    {
        if (scoreData is not BinaryWinScoreData data)
            throw new ArgumentException(
                $"Expected BinaryWinScoreData, got {scoreData?.GetType().Name ?? "null"}",
                nameof(scoreData));
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public object Deserialize(string scoreDataJson)
    {
        var data = JsonSerializer.Deserialize<BinaryWinScoreData>(scoreDataJson, JsonOptions)
            ?? throw new JsonException("Failed to deserialize BinaryWinScoreData");
        return data;
    }

    public Guid? ComputeWinnerPlayerId(string scoreDataJson)
    {
        var data = (BinaryWinScoreData)Deserialize(scoreDataJson);
        if (data.Results.Count == 0) return null;

        var winners = data.Results.Where(r => r.IsWinner).ToList();
        // Single winner = that player; cooperative all-win or all-lose = null
        return winners.Count == 1 ? winners[0].PlayerId : null;
    }
}

/// <summary>
/// JSON payload for ScoreType.BinaryWin sessions.
/// </summary>
public record BinaryWinScoreData(IReadOnlyList<BinaryPlayerResult> Results);

/// <summary>
/// Single player's binary win/lose result.
/// </summary>
public record BinaryPlayerResult(Guid PlayerId, bool IsWinner);
