// GameSearchResultDto - Multi-source game search result
// Issue #4273: Game Search Autocomplete

namespace Api.BoundedContexts.GameManagement.Application.DTOs;

public class GameSearchResultDto
{
    /// <summary>
    /// Game ID (SharedGame ID or LibraryEntry ID for private games)
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Game name/title
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Source of the game: "library" | "catalog" | "private"
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Optional game image URL
    /// </summary>
    public string? ImageUrl { get; init; }
}
