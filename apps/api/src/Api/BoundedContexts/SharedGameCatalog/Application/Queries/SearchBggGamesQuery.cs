using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to search for board games on BoardGameGeek API.
/// Used by the admin "Add from BGG" flow for autocomplete search.
/// Issue: Admin Add Shared Game from BGG flow
/// </summary>
/// <param name="SearchTerm">Game name to search for</param>
/// <param name="ExactMatch">Whether to search for exact match only</param>
internal record SearchBggGamesQuery(
    string SearchTerm,
    bool ExactMatch = false
) : IQuery<List<BggSearchResultDto>>;
