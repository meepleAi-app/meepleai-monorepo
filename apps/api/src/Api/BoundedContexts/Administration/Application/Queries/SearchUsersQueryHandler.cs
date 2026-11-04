using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure.Security;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for SearchUsersQuery.
/// Uses IUserProfileRepository to search users for autocomplete scenarios.
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
        var term = query.SearchQuery?.Trim() ?? string.Empty;

        // Return empty list for short queries
        if (term.Length < 2)
        {
            return Array.Empty<UserSearchResultDto>();
        }

        try
        {
            // #4727: any signed-in user can call this, so it must not be a way to list the addresses of
            // every account. A name is matched partially (what an autocomplete needs); an email is matched
            // only whole, so the caller already has to know the address to find the person. A term with an
            // "@" is an email lookup and never a name search.
            IReadOnlyList<UserProfile> users;
            if (term.Contains('@', StringComparison.Ordinal))
            {
                var match = await _userProfileRepository.GetByEmailAsync(term, cancellationToken).ConfigureAwait(false);
                users = match is null ? Array.Empty<UserProfile>() : [match];
            }
            else
            {
                users = await _userProfileRepository.SearchByDisplayNameAsync(term, query.MaxResults, cancellationToken).ConfigureAwait(false);
            }

            // The address is masked (j***n@example.com): enough to tell two people of the same name apart,
            // not enough to write to them. A user without a display name is shown by the masked address.
            var results = users
                .Select(u =>
                {
                    var maskedEmail = DataMasking.MaskEmail(u.Email);
                    return new UserSearchResultDto(
                        Id: u.Id.ToString(),
                        DisplayName: string.IsNullOrWhiteSpace(u.DisplayName) ? maskedEmail : u.DisplayName,
                        Email: maskedEmail);
                }).ToList();

            // The term is not logged: for an email lookup it is somebody's address.
            _logger.LogInformation("Found {Count} users matching a user search", results.Count);
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
            _logger.LogError(ex, "Error searching users");
            return Array.Empty<UserSearchResultDto>();
        }
#pragma warning restore CA1031
    }
}
