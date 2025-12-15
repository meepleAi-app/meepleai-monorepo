using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.BggApi;

/// <summary>
/// Query to search for board games on BoardGameGeek by name.
/// Returns top 5 results with basic metadata (BGG ID, name, year).
/// External API integration with caching and rate limiting.
/// </summary>
internal sealed record SearchBggGamesQuery : IQuery<List<BggSearchResultDto>>
{
    public string Query { get; init; } = string.Empty;
    public bool Exact { get; init; } = false;
}
