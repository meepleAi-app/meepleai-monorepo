using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handler for SearchBggGamesQuery.
/// Searches BoardGameGeek API for board games by name.
/// Delegates to IBggApiService infrastructure service.
/// </summary>
internal class SearchBggGamesQueryHandler : IQueryHandler<SearchBggGamesQuery, List<BggSearchResultDto>>
{
    private readonly IBggApiService _bggApiService;
    private readonly ILogger<SearchBggGamesQueryHandler> _logger;

    public SearchBggGamesQueryHandler(
        IBggApiService bggApiService,
        ILogger<SearchBggGamesQueryHandler> logger)
    {
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<BggSearchResultDto>> Handle(
        SearchBggGamesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            _logger.LogWarning("Empty search term provided to SearchBggGamesQuery");
            return new List<BggSearchResultDto>();
        }

        _logger.LogInformation(
            "Searching BGG for games: SearchTerm={SearchTerm}, ExactMatch={ExactMatch}",
            query.SearchTerm, query.ExactMatch);

        // Delegate to infrastructure service (external API integration)
        var results = await _bggApiService.SearchGamesAsync(
            query.SearchTerm,
            query.ExactMatch,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "BGG search completed: Found {Count} results for '{SearchTerm}'",
            results.Count, query.SearchTerm);

        return results;
    }
}
