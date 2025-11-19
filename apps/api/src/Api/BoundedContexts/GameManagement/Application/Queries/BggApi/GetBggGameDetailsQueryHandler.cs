using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Queries.BggApi;

/// <summary>
/// Handles retrieval of detailed board game information from BoardGameGeek external API.
/// Business logic: BGG ID validation.
/// Infrastructure delegation: HTTP calls, XML parsing, caching via BggApiService.
/// External API: BoardGameGeek XML API v2 (thing endpoint).
/// </summary>
public sealed class GetBggGameDetailsQueryHandler : IQueryHandler<GetBggGameDetailsQuery, BggGameDetailsDto?>
{
    private readonly IBggApiService _bggApiService;
    private readonly ILogger<GetBggGameDetailsQueryHandler> _logger;

    public GetBggGameDetailsQueryHandler(
        IBggApiService bggApiService,
        ILogger<GetBggGameDetailsQueryHandler> logger)
    {
        _bggApiService = bggApiService;
        _logger = logger;
    }

    public async Task<BggGameDetailsDto?> Handle(GetBggGameDetailsQuery query, CancellationToken cancellationToken)
    {
        // Business logic validation
        if (query.BggId <= 0)
        {
            _logger.LogWarning("Invalid BGG ID: {BggId}", query.BggId);
            return null;
        }

        _logger.LogInformation("Fetching BGG game details for ID: {BggId}", query.BggId);

        // Delegate to infrastructure service for:
        // - HTTP call to BoardGameGeek XML API (thing endpoint)
        // - XML parsing
        // - Caching (HybridCache with 24h expiration)
        var details = await _bggApiService.GetGameDetailsAsync(query.BggId, cancellationToken);

        if (details == null)
        {
            _logger.LogWarning("BGG game not found: {BggId}", query.BggId);
        }
        else
        {
            _logger.LogInformation("BGG game details retrieved: {BggId} - {Name}", query.BggId, details.Name);
        }

        return details;
    }
}
