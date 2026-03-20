using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for SearchUsersQuery.
/// Uses IUserProfileRepository to search users for autocomplete scenarios.
/// API-01: Authentication endpoints (versioned)
/// </summary>
internal class SearchUsersQueryHandler : IQueryHandler<SearchUsersQuery, IReadOnlyList<UserSearchResultDto>>
{
    private readonly IUserProfileRepository _userProfileRepository;
    private readonly ILogger<SearchUsersQueryHandler> _logger;

    public SearchUsersQueryHandler(
        IUserProfileRepository userProfileRepository,
        ILogger<SearchUsersQueryHandler> logger)
    {
        _userProfileRepository = userProfileRepository ?? throw new ArgumentNullException(nameof(userProfileRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<UserSearchResultDto>> Handle(SearchUsersQuery query, CancellationToken cancellationToken)
    {
        // Return empty list for short queries
        if (string.IsNullOrWhiteSpace(query.SearchQuery) || query.SearchQuery.Length < 2)
        {
            return Array.Empty<UserSearchResultDto>();
        }

        try
        {
            var users = await _userProfileRepository.SearchAsync(query.SearchQuery, cancellationToken).ConfigureAwait(false);

            var results = users
                .Take(query.MaxResults)
                .Select(u => new UserSearchResultDto(
                    Id: u.Id.ToString(),
                    DisplayName: u.DisplayName ?? string.Empty,
                    Email: u.Email
                )).ToList();

            _logger.LogInformation("Found {Count} users matching query: {Query}", results.Count, query.SearchQuery);
            return results;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: QUERY HANDLER PATTERN - CQRS query boundary
        // Generic catch handles unexpected infrastructure failures (DB, network)
        // to prevent exception propagation to API layer. Returns empty result on failure.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users with query: {Query}", query.SearchQuery);
            return Array.Empty<UserSearchResultDto>();
        }
#pragma warning restore CA1031
    }
}
