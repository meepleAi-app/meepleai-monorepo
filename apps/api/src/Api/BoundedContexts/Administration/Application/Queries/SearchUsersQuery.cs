using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to search users by display name or email.
/// Used for @mention autocomplete in comments.
/// API-01: Authentication endpoints (versioned)
/// </summary>
internal sealed record SearchUsersQuery(
    string SearchQuery,
    int MaxResults = 10
) : IQuery<IReadOnlyList<UserSearchResultDto>>;
