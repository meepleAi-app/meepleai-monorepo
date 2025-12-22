using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to search for board games on BoardGameGeek API.
/// Returns top 5 results with basic metadata (ID, name, year).
/// AI-13: External API integration for game discovery.
/// </summary>
/// <param name="SearchTerm">Game name to search for</param>
/// <param name="ExactMatch">Whether to search for exact match only</param>
internal record SearchBggGamesQuery(
    string SearchTerm,
    bool ExactMatch = false
) : IQuery<List<BggSearchResultDto>>;
