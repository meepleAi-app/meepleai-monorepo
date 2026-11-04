using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for batch rejecting multiple shared games.
/// Issue #3350: Batch Approval/Rejection for Games
/// </summary>
internal sealed class BatchRejectGamesCommandHandler
    : ICommandHandler<BatchRejectGamesCommand, BatchRejectGamesResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISharedGameCacheInvalidator _cacheInvalidator;
    private readonly ILogger<BatchRejectGamesCommandHandler> _logger;

    public BatchRejectGamesCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ISharedGameCacheInvalidator cacheInvalidator,
        ILogger<BatchRejectGamesCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BatchRejectGamesResult> Handle(
        BatchRejectGamesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Batch rejecting {Count} games by user {UserId}. Reason: {Reason}",
            command.GameIds.Count, command.RejectedBy, command.Reason);

        var successCount = 0;
        var errors = new List<string>();
        var changedGameIds = new List<Guid>();

        // Process all games in a single transaction for atomicity
        foreach (var gameId in command.GameIds)
        {
            try
            {
                var game = await _repository.GetByIdAsync(gameId, cancellationToken).ConfigureAwait(false);

                if (game is null)
                {
                    errors.Add($"Game {gameId} not found");
                    continue;
                }

                // Call domain method (validates status and raises event)
                game.RejectPublication(command.RejectedBy, command.Reason);
                _repository.Update(game);
                successCount++;
                changedGameIds.Add(gameId);

                _logger.LogDebug("Rejected game {GameId}", gameId);
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"Game {gameId}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to reject game {GameId}", gameId);
            }
        }

        // Save all changes in a single transaction
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // #4603: il dettaglio e la lista del catalogo in cache portano lo stato del gioco (la lista, la sua
        // stessa presenza), e l'endpoint pubblico nasconde i non pubblicati DOPO la cache: senza questa
        // evizione il pubblico vedeva lo stato di prima fino alla scadenza della voce. Dopo il salvataggio,
        // mai prima. Vedi ISharedGameCacheInvalidator.
        await _cacheInvalidator.EvictDetailsAndListAsync(changedGameIds, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Batch rejection completed: {SuccessCount} succeeded, {FailureCount} failed",
            successCount, errors.Count);

        return new BatchRejectGamesResult(
            SuccessCount: successCount,
            FailureCount: errors.Count,
            Errors: errors
        );
    }
}
