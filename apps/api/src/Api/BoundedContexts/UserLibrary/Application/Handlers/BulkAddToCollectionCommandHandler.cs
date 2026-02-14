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
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
            // HANDLER BOUNDARY: BULK OPERATION PATTERN - Individual collection operation failure handling
            // Catches all exceptions during collection add (validation, DB constraints, quota exceeded, etc.)
            // to collect errors without stopping batch processing. Each failure is tracked in error list
            // for reporting. Allows partial success in bulk operation.
#pragma warning restore S125
            catch (Exception ex)
#pragma warning restore CA1031
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
