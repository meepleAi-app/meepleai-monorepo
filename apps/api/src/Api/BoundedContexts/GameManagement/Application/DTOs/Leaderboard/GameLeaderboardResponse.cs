namespace Api.BoundedContexts.GameManagement.Application.DTOs.Leaderboard;

/// <summary>
/// One ranked player in a game leaderboard (#1467).
/// Only registered users are ranked; <see cref="PlayerId"/> is the user id.
/// </summary>
public sealed record GameLeaderboardEntryDto
{
    /// <summary>Registered user id of the player.</summary>
    public required Guid PlayerId { get; init; }

    /// <summary>Display name taken from the player's most recent in-scope record.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Up to two uppercase initials derived from <see cref="DisplayName"/>.</summary>
    public required string Initials { get; init; }

    /// <summary>Total of the "wins" scoring dimension summed across the player's in-scope records.</summary>
    public required int Wins { get; init; }

    /// <summary>Number of in-scope completed records the player took part in.</summary>
    public required int Plays { get; init; }

    /// <summary>Average of the "points" dimension across in-scope records; null when never scored on points.</summary>
    public double? AvgScore { get; init; }

    /// <summary>Most recent session date among the player's in-scope records.</summary>
    public required DateTime LastPlayedAt { get; init; }
}

/// <summary>
/// Game leaderboard response (#1467). Top-N players ranked by wins, then average score, then plays.
/// Scope is "social": the caller's own records plus records of groups the caller belongs to.
/// </summary>
public sealed record GameLeaderboardResponse
{
    public required Guid GameId { get; init; }

    public required IReadOnlyList<GameLeaderboardEntryDto> Entries { get; init; }

    /// <summary>Number of entries actually returned (top-N), not the total player population.</summary>
    public required int ReturnedCount { get; init; }

    /// <summary>Echo of the optional temporal filter applied to SessionDate.</summary>
    public DateTime? Since { get; init; }
}
