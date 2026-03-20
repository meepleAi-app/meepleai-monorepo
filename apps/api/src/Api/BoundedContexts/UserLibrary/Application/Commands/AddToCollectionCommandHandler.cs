using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Handler for adding an entity to user's collection.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal class AddToCollectionCommandHandler : ICommandHandler<AddToCollectionCommand>
{
    private readonly IUserCollectionRepository _collectionRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddToCollectionCommandHandler(
        IUserCollectionRepository collectionRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork)
    {
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(AddToCollectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Phase 2: Only validate Game entities via SharedGameCatalog
        // Other entity types (Player, Event, Session, etc.) are deferred to Phase 3
        if (command.EntityType == EntityType.Game)
        {
            var sharedGame = await _sharedGameRepository
                .GetByIdAsync(command.EntityId, cancellationToken)
                .ConfigureAwait(false);

            if (sharedGame == null)
            {
                throw new DomainException($"Game with ID {command.EntityId} not found in catalog");
            }
        }

        // Check if already in collection
        var exists = await _collectionRepository
            .ExistsAsync(command.UserId, command.EntityType, command.EntityId, cancellationToken)
            .ConfigureAwait(false);

        if (exists)
        {
            throw new DomainException($"{command.EntityType} is already in your collection");
        }

        // Create collection entry
        var entry = new UserCollectionEntry(
            Guid.NewGuid(),
            command.UserId,
            command.EntityType,
            command.EntityId);

        // Set notes if provided
        if (!string.IsNullOrWhiteSpace(command.Notes))
        {
            entry.UpdateNotes(command.Notes);
        }

        // Set favorite status if requested
        if (command.IsFavorite)
        {
            entry.MarkAsFavorite();
        }

        await _collectionRepository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
