using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for bulk adding entities to user's collection.
/// Uses partial success pattern: continues processing on errors.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
internal class BulkAddToCollectionCommandHandler : ICommandHandler<BulkAddToCollectionCommand, BulkOperationResult>
{
    private readonly IUserCollectionRepository _collectionRepository;
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkAddToCollectionCommandHandler(
        IUserCollectionRepository collectionRepository,
        ISharedGameRepository sharedGameRepository,
        IUnitOfWork unitOfWork)
    {
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<BulkOperationResult> Handle(BulkAddToCollectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        var successCount = 0;

        foreach (var entityId in command.EntityIds)
        {
            try
            {
                // Validate Game entities via SharedGameCatalog (Phase 2 implementation)
                if (command.EntityType == EntityType.Game)
                {
                    var sharedGame = await _sharedGameRepository
                        .GetByIdAsync(entityId, cancellationToken)
                        .ConfigureAwait(false);

                    if (sharedGame == null)
                    {
                        errors.Add($"Game {entityId}: Not found in catalog");
                        continue;
                    }
                }

                // Check if already in collection
                var exists = await _collectionRepository
                    .ExistsAsync(command.UserId, command.EntityType, entityId, cancellationToken)
                    .ConfigureAwait(false);

                if (exists)
                {
                    errors.Add($"{command.EntityType} {entityId}: Already in collection");
                    continue;
                }

                // Create collection entry
                var entry = new UserCollectionEntry(
                    Guid.NewGuid(),
                    command.UserId,
                    command.EntityType,
                    entityId);

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
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{command.EntityType} {entityId}: {ex.Message}");
            }
        }

        // Only save if at least one entity was added successfully
        if (successCount > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return new BulkOperationResult(
            TotalRequested: command.EntityIds.Count,
            SuccessCount: successCount,
            FailedCount: errors.Count,
            Errors: errors
        );
    }
}
