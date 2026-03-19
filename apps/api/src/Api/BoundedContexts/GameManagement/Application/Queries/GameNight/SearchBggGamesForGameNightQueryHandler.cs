using Api.BoundedContexts.GameManagement.Application.Queries.GameNight;
using Api.Infrastructure.ExternalServices.BoardGameGeek;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameNight;

/// <summary>
/// Handles BGG game search for the Game Night Improvvisata flow.
/// Delegates to IBggApiClient for external API calls, applies pagination in-memory.
/// Game Night Improvvisata - E1-1: User-facing BGG search via CQRS.
/// </summary>
internal sealed class SearchBggGamesForGameNightQueryHandler
    : IQueryHandler<SearchBggGamesForGameNightQuery, SearchBggGamesForGameNightResult>
{
    private readonly IBggApiClient _bggApiClient;
    private readonly ILogger<SearchBggGamesForGameNightQueryHandler> _logger;

    public SearchBggGamesForGameNightQueryHandler(
        IBggApiClient bggApiClient,
        ILogger<SearchBggGamesForGameNightQueryHandler> logger)
    {
        _bggApiClient = bggApiClient ?? throw new ArgumentNullException(nameof(bggApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SearchBggGamesForGameNightResult> Handle(
        SearchBggGamesForGameNightQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Game Night BGG search: SearchTerm={SearchTerm}, Page={Page}, PageSize={PageSize}",
            query.SearchTerm, query.Page, query.PageSize);

        var allResults = await _bggApiClient
            .SearchGamesAsync(query.SearchTerm, cancellationToken)
            .ConfigureAwait(false);

        var resultsList = allResults.ToList();
        var total = resultsList.Count;

        var pagedResults = resultsList
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new BggGameSummary(
                BggId: r.BggId,
                Title: r.Name,
                YearPublished: r.YearPublished,
                ThumbnailUrl: r.ThumbnailUrl))
            .ToList();

        var totalPages = total == 0
            ? 0
            : (int)Math.Ceiling((double)total / query.PageSize);

        _logger.LogInformation(
            "Game Night BGG search completed: {Total} total results, {PageCount} on page {Page} for '{SearchTerm}'",
            total, pagedResults.Count, query.Page, query.SearchTerm);

        return new SearchBggGamesForGameNightResult(
            Results: pagedResults,
            Total: total,
            Page: query.Page,
            PageSize: query.PageSize,
            TotalPages: totalPages);
    }
}
