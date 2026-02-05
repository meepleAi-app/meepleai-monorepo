using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to find similar games based on semantic similarity (RAG).
/// Issue #3353: Similar Games Discovery with RAG
/// </summary>
/// <param name="GameId">The ID of the source game to find similar games for</param>
/// <param name="UserId">Optional user ID to filter out already-owned games</param>
/// <param name="TopK">Maximum number of similar games to return (default: 10)</param>
/// <param name="MinSimilarity">Minimum similarity score threshold (default: 0.7)</param>
internal sealed record GetSimilarGamesQuery(
    Guid GameId,
    Guid? UserId = null,
    int TopK = 10,
    double MinSimilarity = 0.7
) : IQuery<GetSimilarGamesResult>;

/// <summary>
/// Result containing similar games with similarity scores.
/// </summary>
/// <param name="Games">List of similar games with similarity metadata</param>
/// <param name="SourceGameId">The ID of the source game</param>
/// <param name="SourceGameTitle">The title of the source game</param>
internal sealed record GetSimilarGamesResult(
    IReadOnlyList<SimilarGameDto> Games,
    Guid SourceGameId,
    string SourceGameTitle
);

/// <summary>
/// DTO for a similar game with similarity information.
/// </summary>
/// <param name="Id">Game ID</param>
/// <param name="Title">Game title</param>
/// <param name="ThumbnailUrl">Thumbnail image URL</param>
/// <param name="MinPlayers">Minimum player count</param>
/// <param name="MaxPlayers">Maximum player count</param>
/// <param name="PlayingTimeMinutes">Average playing time</param>
/// <param name="ComplexityRating">BGG complexity rating (1-5)</param>
/// <param name="AverageRating">BGG average rating (1-10)</param>
/// <param name="SimilarityScore">Similarity score (0-1)</param>
/// <param name="SimilarityReason">Human-readable reason for similarity</param>
internal sealed record SimilarGameDto(
    Guid Id,
    string Title,
    string? ThumbnailUrl,
    int? MinPlayers,
    int? MaxPlayers,
    int? PlayingTimeMinutes,
    decimal? ComplexityRating,
    decimal? AverageRating,
    double SimilarityScore,
    string SimilarityReason
);
