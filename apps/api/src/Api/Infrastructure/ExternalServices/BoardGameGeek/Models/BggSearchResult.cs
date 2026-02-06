namespace Api.Infrastructure.ExternalServices.BoardGameGeek.Models;

/// <summary>
/// BGG search result.
/// Issue #3120: Public BGG search for users.
/// </summary>
public record BggSearchResult
{
    public required int BggId { get; init; }
    public required string Name { get; init; }
    public int? YearPublished { get; init; }
}
