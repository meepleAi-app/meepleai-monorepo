using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handler for GetBggGameDetailsQuery.
/// Retrieves detailed board game information from BoardGameGeek API.
/// Delegates to IBggApiService infrastructure service.
/// </summary>
internal class GetBggGameDetailsQueryHandler : IQueryHandler<GetBggGameDetailsQuery, BggGameDetailsDto?>
{
    private readonly IBggApiService _bggApiService;
    private readonly ILogger<GetBggGameDetailsQueryHandler> _logger;

    public GetBggGameDetailsQueryHandler(
        IBggApiService bggApiService,
        ILogger<GetBggGameDetailsQueryHandler> logger)
    {
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BggGameDetailsDto?> Handle(
        GetBggGameDetailsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.BggId <= 0)
        {
            _logger.LogWarning("Invalid BGG ID provided: {BggId}", query.BggId);
            throw new ArgumentException("BGG ID must be positive", nameof(query));
        }

        _logger.LogInformation(
            "Retrieving BGG game details for BggId={BggId}",
            query.BggId);

        // Delegate to infrastructure service (external API integration)
        var details = await _bggApiService.GetGameDetailsAsync(
            query.BggId,
            cancellationToken).ConfigureAwait(false);

        if (details == null)
        {
            _logger.LogWarning("BGG game not found: BggId={BggId}", query.BggId);
        }
        else
        {
            _logger.LogInformation(
                "BGG game details retrieved: BggId={BggId}, Name={Name}",
                query.BggId, details.Name);
        }

        return details;
    }
}
