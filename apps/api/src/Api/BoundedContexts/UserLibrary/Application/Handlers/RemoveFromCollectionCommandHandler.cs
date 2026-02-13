using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for removing an entity from user's collection.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal class RemoveFromCollectionCommandHandler : ICommandHandler<RemoveFromCollectionCommand>
{
    private readonly IUserCollectionRepository _collectionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveFromCollectionCommandHandler(
        IUserCollectionRepository collectionRepository,
        IUnitOfWork unitOfWork)
    {
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task Handle(RemoveFromCollectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Find the collection entry
        var entry = await _collectionRepository
            .GetByUserAndEntityAsync(command.UserId, command.EntityType, command.EntityId, cancellationToken)
            .ConfigureAwait(false);

        if (entry == null)
        {
            throw new DomainException($"{command.EntityType} is not in your collection");
        }

        // Prepare for removal (raises domain event)
        entry.PrepareForRemoval();

        await _collectionRepository.DeleteAsync(entry, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
