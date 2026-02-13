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
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
            // HANDLER BOUNDARY: BULK OPERATION PATTERN - Individual collection removal failure handling
            // Catches all exceptions during collection remove (not found, DB constraints, etc.)
            // to collect errors without stopping batch processing. Each failure is tracked in error list
            // for reporting. Allows partial success in bulk operation.
#pragma warning restore S125
            catch (Exception ex)
#pragma warning restore CA1031
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
