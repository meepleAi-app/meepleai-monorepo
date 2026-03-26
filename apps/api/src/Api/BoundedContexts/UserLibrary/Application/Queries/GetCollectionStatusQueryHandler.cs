using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Handler for checking if an entity is in the user's collection.
/// Routes Game entities to the game-specific handler for backward compatibility.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
internal class GetCollectionStatusQueryHandler : IQueryHandler<GetCollectionStatusQuery, CollectionStatusDto>
{
    private readonly IUserCollectionRepository _collectionRepository;
    private readonly IMediator _mediator;

    public GetCollectionStatusQueryHandler(
        IUserCollectionRepository collectionRepository,
        IMediator mediator)
    {
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    public async Task<CollectionStatusDto> Handle(
        GetCollectionStatusQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Route Game entities to the game-specific handler for backward compatibility
        // This ensures we get full game-specific associated data (CustomAgent, PrivatePdf, etc.)
        if (query.EntityType == EntityType.Game)
        {
            var gameStatus = await _mediator
                .Send(new GetGameInLibraryStatusQuery(query.UserId, query.EntityId), cancellationToken)
                .ConfigureAwait(false);

            return new CollectionStatusDto(
                InCollection: gameStatus.InLibrary,
                IsFavorite: gameStatus.IsFavorite,
                AssociatedData: gameStatus.AssociatedData
            );
        }

        // For non-game entities, use the generic collection repository
        var entry = await _collectionRepository
            .GetByUserAndEntityAsync(query.UserId, query.EntityType, query.EntityId, cancellationToken)
            .ConfigureAwait(false);

        // Not in collection
        if (entry == null)
        {
            return new CollectionStatusDto(
                InCollection: false,
                IsFavorite: false,
                AssociatedData: null
            );
        }

        // Phase 2: No associated data for non-game entities (simplified)
        // Phase 3 will implement entity-specific associated data
        return new CollectionStatusDto(
            InCollection: true,
            IsFavorite: entry.IsFavorite,
            AssociatedData: null
        );
    }
}
