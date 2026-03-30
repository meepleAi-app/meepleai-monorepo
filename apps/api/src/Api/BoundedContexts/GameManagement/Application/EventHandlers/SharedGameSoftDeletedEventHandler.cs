using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// Cross-BC handler: when a SharedGame is soft-deleted,
/// all linked Games in GameManagement must have their SharedGameId cleared.
///
/// The FK is OnDelete(SetNull) but that only fires on hard delete.
/// Soft delete (IsDeleted=true) leaves the FK intact — this handler fixes it.
///
/// Spec-panel recommendation C-2.
/// </summary>
internal sealed class SharedGameSoftDeletedEventHandler : INotificationHandler<SharedGameDeletedEvent>
{
    private readonly IGameRepository _gameRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SharedGameSoftDeletedEventHandler> _logger;

    public SharedGameSoftDeletedEventHandler(
        IGameRepository gameRepository,
        IUnitOfWork unitOfWork,
        ILogger<SharedGameSoftDeletedEventHandler> logger = null!)
    {
        _gameRepository = gameRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(SharedGameDeletedEvent notification, CancellationToken cancellationToken)
    {
        var linkedGames = await _gameRepository
            .GetBySharedGameIdAsync(notification.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (linkedGames.Count == 0) return;

        _logger?.LogInformation(
            "SharedGame {SharedGameId} was deleted. Unlinking {Count} linked game(s).",
            notification.GameId,
            linkedGames.Count);

        foreach (var game in linkedGames)
        {
            game.UnlinkFromSharedGame();
            await _gameRepository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
