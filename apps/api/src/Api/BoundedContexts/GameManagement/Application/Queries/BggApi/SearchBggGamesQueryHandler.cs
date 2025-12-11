using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Queries.BggApi;

/// <summary>
/// Handles board game search on BoardGameGeek external API.
/// Business logic: Input validation, empty query handling.
/// Infrastructure delegation: HTTP calls, XML parsing, caching, rate limiting via BggApiService.
/// External API: BoardGameGeek XML API v2.
/// </summary>
public sealed class SearchBggGamesQueryHandler : IQueryHandler<SearchBggGamesQuery, List<BggSearchResultDto>>
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

    public async Task<List<BggSearchResultDto>> Handle(SearchBggGamesQuery query, CancellationToken cancellationToken)
    {
        // Business logic validation
        if (string.IsNullOrWhiteSpace(query.Query))
        {
            _logger.LogWarning("Empty BGG search query");
            return new List<BggSearchResultDto>();
        }

        _logger.LogInformation("Searching BGG for: {Query}, Exact: {Exact}", query.Query, query.Exact);

        // Delegate to infrastructure service for:
        // - HTTP call to BoardGameGeek XML API
        // - XML parsing
        // - Caching (HybridCache)
        // - Rate limiting
        var results = await _bggApiService.SearchGamesAsync(query.Query, query.Exact, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("BGG search returned {Count} results for: {Query}", results.Count, query.Query);

        return results;
    }
}
