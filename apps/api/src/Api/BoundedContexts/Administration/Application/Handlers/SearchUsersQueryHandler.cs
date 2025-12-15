using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for SearchUsersQuery.
/// Uses IUserRepository to search users for autocomplete scenarios.
/// API-01: Authentication endpoints (versioned)
/// </summary>
internal class SearchUsersQueryHandler : IQueryHandler<SearchUsersQuery, IReadOnlyList<UserSearchResultDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<SearchUsersQueryHandler> _logger;

    public SearchUsersQueryHandler(
        IUserRepository userRepository,
        ILogger<SearchUsersQueryHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
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
            var users = await _userRepository.SearchAsync(query.SearchQuery, query.MaxResults, cancellationToken).ConfigureAwait(false);

            var results = users.Select(u => new UserSearchResultDto(
                Id: u.Id.ToString(),
                DisplayName: u.DisplayName,
                Email: u.Email.Value
            )).ToList();

            _logger.LogInformation("Found {Count} users matching query: {Query}", results.Count, query.SearchQuery);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching users with query: {Query}", query.SearchQuery);
            return Array.Empty<UserSearchResultDto>();
        }
    }
}
