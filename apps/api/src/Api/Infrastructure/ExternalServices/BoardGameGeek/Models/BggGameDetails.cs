namespace Api.Infrastructure.ExternalServices.BoardGameGeek.Models;

/// <summary>
/// Detailed BGG game information.
/// Issue #3120: Public BGG search for users.
/// </summary>
public record BggGameDetails
{
    public required int BggId { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public int? YearPublished { get; init; }
    public int? MinPlayers { get; init; }
    public int? MaxPlayers { get; init; }
    public int? PlayingTimeMinutes { get; init; }
    public decimal? ComplexityRating { get; init; }
    public string? ImageUrl { get; init; }
    public string? ThumbnailUrl { get; init; }
    public decimal? AverageRating { get; init; }
    public int? RankPosition { get; init; }
}
