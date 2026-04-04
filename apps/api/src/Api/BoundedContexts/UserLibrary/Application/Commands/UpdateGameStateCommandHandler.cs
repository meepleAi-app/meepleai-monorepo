using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for updating game state with validation and cache invalidation.
/// Delegates to domain logic for state transition validation.
/// </summary>
internal class UpdateGameStateCommandHandler : ICommandHandler<UpdateGameStateCommand>
{
    private readonly IUserLibraryRepository _libraryRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HybridCache _cache;
    private readonly IPublisher _publisher;
    private readonly ILogger<UpdateGameStateCommandHandler> _logger;

    public UpdateGameStateCommandHandler(
        IUserLibraryRepository libraryRepository,
        IUnitOfWork unitOfWork,
        HybridCache cache,
        IPublisher publisher,
        ILogger<UpdateGameStateCommandHandler> logger)
    {
        _libraryRepository = libraryRepository ?? throw new ArgumentNullException(nameof(libraryRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(UpdateGameStateCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Find library entry
        var entry = await _libraryRepository.GetByUserAndGameAsync(command.UserId, command.GameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Game {command.GameId} not found in your library");

        // Parse and create new game state
        var newState = command.NewState.ToLowerInvariant() switch
        {
            "nuovo" => GameState.Nuovo(command.StateNotes),
            "inprestito" => GameState.InPrestito(command.StateNotes),
            "wishlist" => GameState.Wishlist(command.StateNotes),
            "owned" => GameState.Owned(command.StateNotes),
            _ => throw new ConflictException($"Invalid state: {command.NewState}")
        };

        // Delegate to domain logic (validates transitions)
        entry.ChangeState(newState);

        // Persist changes
        await _libraryRepository.UpdateAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate game detail cache
        var cacheKey = $"game-detail:{command.UserId}:{command.GameId}";
        await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Updated game {GameId} state to {NewState} for user {UserId}",
            command.GameId, command.NewState, command.UserId);

        // Issue #3974: Publish cache invalidation event for wishlist changes
        if (command.NewState.Equals("wishlist", StringComparison.OrdinalIgnoreCase))
        {
            await _publisher.Publish(
                new UserWishlistUpdatedEvent(command.UserId, command.GameId),
                cancellationToken).ConfigureAwait(false);
        }
    }
}
