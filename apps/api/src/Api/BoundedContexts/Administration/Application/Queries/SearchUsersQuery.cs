using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to search users by display name, or to look one up by their whole email address.
/// Used for @mention autocomplete in comments and player invite autocomplete.
/// #4727: the result carries a masked email, and a partial email matches nobody.
/// API-01: Authentication endpoints (versioned)
/// </summary>
internal sealed record SearchUsersQuery(
    string SearchQuery,
    int MaxResults = 10
) : IQuery<IReadOnlyList<UserSearchResultDto>>;
