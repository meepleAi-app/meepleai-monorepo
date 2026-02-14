using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetBggGameDetailsQuery.
/// Fetches detailed board game information from BoardGameGeek API.
/// Delegates to IBggApiService infrastructure service.
/// Issue #4139: Backend - API Endpoints PDF Wizard
/// </summary>
internal sealed class GetBggGameDetailsQueryHandler : IQueryHandler<GetBggGameDetailsQuery, BggGameDetailsDto?>
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

        _logger.LogInformation(
            "Fetching BGG game details: BggId={BggId}",
            query.BggId);

        var details = await _bggApiService.GetGameDetailsAsync(
            query.BggId,
            cancellationToken).ConfigureAwait(false);

        if (details == null)
        {
            _logger.LogWarning(
                "BGG game not found: BggId={BggId}",
                query.BggId);
        }
        else
        {
            _logger.LogInformation(
                "BGG game details fetched successfully: {Name} (BggId={BggId})",
                details.Name, query.BggId);
        }

        return details;
    }
}
