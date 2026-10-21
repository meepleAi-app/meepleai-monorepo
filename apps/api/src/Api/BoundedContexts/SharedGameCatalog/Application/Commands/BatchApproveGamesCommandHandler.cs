using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for batch approving multiple shared games.
/// Issue #3350: Batch Approval/Rejection for Games
/// </summary>
internal sealed class BatchApproveGamesCommandHandler
    : ICommandHandler<BatchApproveGamesCommand, BatchApproveGamesResult>
{
    private readonly ISharedGameRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISharedGameCacheInvalidator _cacheInvalidator;
    private readonly ILogger<BatchApproveGamesCommandHandler> _logger;

    public BatchApproveGamesCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ISharedGameCacheInvalidator cacheInvalidator,
        ILogger<BatchApproveGamesCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cacheInvalidator = cacheInvalidator ?? throw new ArgumentNullException(nameof(cacheInvalidator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BatchApproveGamesResult> Handle(
        BatchApproveGamesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Batch approving {Count} games by user {UserId}",
            command.GameIds.Count, command.ApprovedBy);

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
                game.ApprovePublication(command.ApprovedBy);
                _repository.Update(game);
                successCount++;
                changedGameIds.Add(gameId);

                _logger.LogDebug("Approved game {GameId}", gameId);
            }
            catch (InvalidOperationException ex)
            {
                errors.Add($"Game {gameId}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to approve game {GameId}", gameId);
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
            "Batch approval completed: {SuccessCount} succeeded, {FailureCount} failed",
            successCount, errors.Count);

        return new BatchApproveGamesResult(
            SuccessCount: successCount,
            FailureCount: errors.Count,
            Errors: errors
        );
    }
}
