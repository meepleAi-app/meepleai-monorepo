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
    private readonly ILogger<BatchRejectGamesCommandHandler> _logger;

    public BatchRejectGamesCommandHandler(
        ISharedGameRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<BatchRejectGamesCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
