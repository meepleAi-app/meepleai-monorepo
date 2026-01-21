using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for checking BGG duplicate and fetching both existing and BGG data for diff.
/// Used in the admin "Add from BGG" flow to detect duplicates and propose updates.
/// Issue: Admin Add Shared Game from BGG flow
/// </summary>
internal sealed class CheckBggDuplicateQueryHandler : IRequestHandler<CheckBggDuplicateQuery, BggDuplicateCheckResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IBggApiService _bggApiService;
    private readonly IMediator _mediator;
    private readonly ILogger<CheckBggDuplicateQueryHandler> _logger;

    public CheckBggDuplicateQueryHandler(
        ISharedGameRepository repository,
        IBggApiService bggApiService,
        IMediator mediator,
        ILogger<CheckBggDuplicateQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BggDuplicateCheckResult> Handle(CheckBggDuplicateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Checking BGG duplicate for BggId: {BggId}", query.BggId);

        // Fetch fresh BGG data first (we always need this)
        var bggData = await _bggApiService.GetGameDetailsAsync(query.BggId, cancellationToken).ConfigureAwait(false);
        if (bggData is null)
        {
            _logger.LogWarning("Game not found on BGG: {BggId}", query.BggId);
            return new BggDuplicateCheckResult(
                IsDuplicate: false,
                ExistingGameId: null,
                ExistingGame: null,
                BggData: null);
        }

        // Check if game already exists by BGG ID
        var existingGame = await _repository.GetByBggIdAsync(query.BggId, cancellationToken).ConfigureAwait(false);

        if (existingGame is null)
        {
            _logger.LogInformation("No duplicate found for BggId: {BggId}", query.BggId);
            return new BggDuplicateCheckResult(
                IsDuplicate: false,
                ExistingGameId: null,
                ExistingGame: null,
                BggData: bggData);
        }

        _logger.LogInformation("Duplicate found for BggId: {BggId}, ExistingGameId: {GameId}", query.BggId, existingGame.Id);

        // Fetch full existing game details using existing query
        var existingGameDetails = await _mediator.Send(
            new GetSharedGameByIdQuery(existingGame.Id),
            cancellationToken).ConfigureAwait(false);

        return new BggDuplicateCheckResult(
            IsDuplicate: true,
            ExistingGameId: existingGame.Id,
            ExistingGame: existingGameDetails,
            BggData: bggData);
    }
}
