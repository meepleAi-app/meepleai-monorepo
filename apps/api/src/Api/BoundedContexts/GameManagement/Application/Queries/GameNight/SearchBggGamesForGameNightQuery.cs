using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNight;

/// <summary>
/// Query to search BoardGameGeek for games during the Game Night Improvvisata flow.
/// Returns paginated results with BGG metadata for game selection.
/// Game Night Improvvisata - E1-1: User-facing BGG search via CQRS.
/// </summary>
/// <param name="SearchTerm">Game name to search for on BGG</param>
/// <param name="Page">Page number (1-based)</param>
/// <param name="PageSize">Number of results per page (1-50)</param>
public sealed record SearchBggGamesForGameNightQuery(
    string SearchTerm,
    int Page = 1,
    int PageSize = 20
) : IQuery<SearchBggGamesForGameNightResult>;

/// <summary>
/// Paginated result for BGG game search in the Game Night Improvvisata flow.
/// </summary>
/// <param name="Results">The page of BGG game summaries</param>
/// <param name="Total">Total number of results across all pages</param>
/// <param name="Page">Current page number</param>
/// <param name="PageSize">Number of items per page</param>
/// <param name="TotalPages">Total number of pages</param>
public sealed record SearchBggGamesForGameNightResult(
    IReadOnlyList<BggGameSummary> Results,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);

/// <summary>
/// Lightweight BGG game summary for game selection in Game Night Improvvisata.
/// </summary>
/// <param name="BggId">BoardGameGeek game identifier</param>
/// <param name="Title">Game title</param>
/// <param name="YearPublished">Year the game was published, if known</param>
/// <param name="ThumbnailUrl">URL of the game thumbnail image, if available</param>
public sealed record BggGameSummary(
    int BggId,
    string Title,
    int? YearPublished,
    string? ThumbnailUrl
);
