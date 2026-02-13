using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.SharedKernel.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for bulk removing entities from user's collection.
/// Uses partial success pattern: continues processing on errors.
/// Issue #4268: Phase 3 - Bulk Collection Actions
/// </summary>
internal class BulkRemoveFromCollectionCommandHandler : ICommandHandler<BulkRemoveFromCollectionCommand, BulkOperationResult>
{
    private readonly IUserCollectionRepository _collectionRepository;
    private readonly IUnitOfWork _unitOfWork;

    public BulkRemoveFromCollectionCommandHandler(
        IUserCollectionRepository collectionRepository,
        IUnitOfWork unitOfWork)
    {
        _collectionRepository = collectionRepository ?? throw new ArgumentNullException(nameof(collectionRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<BulkOperationResult> Handle(BulkRemoveFromCollectionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var errors = new List<string>();
        var successCount = 0;

        foreach (var entityId in command.EntityIds)
        {
            try
            {
                // Find collection entry
                var entry = await _collectionRepository
                    .GetByUserAndEntityAsync(command.UserId, command.EntityType, entityId, cancellationToken)
                    .ConfigureAwait(false);

                if (entry == null)
                {
                    errors.Add($"{command.EntityType} {entityId}: Not in collection");
                    continue;
                }

                // Prepare for removal (raises domain event)
                entry.PrepareForRemoval();

                await _collectionRepository.DeleteAsync(entry, cancellationToken).ConfigureAwait(false);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"{command.EntityType} {entityId}: {ex.Message}");
            }
        }

        // Only save if at least one entity was removed successfully
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
