using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetBulkImportProgressQuery.
/// Aggregates queue item stats into a progress snapshot.
/// Issue #4353: Backend - Bulk Import SSE Progress Endpoint
/// </summary>
internal class GetBulkImportProgressQueryHandler
    : IRequestHandler<GetBulkImportProgressQuery, BulkImportProgressDto>
{
    private readonly IBggImportQueueService _queueService;

    public GetBulkImportProgressQueryHandler(IBggImportQueueService queueService)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
    }

    public async Task<BulkImportProgressDto> Handle(
        GetBulkImportProgressQuery request,
        CancellationToken cancellationToken)
    {
        var allItems = await _queueService
            .GetAllQueueItemsAsync(cancellationToken)
            .ConfigureAwait(false);

        var queuedCount = allItems.Count(q => q.Status == BggImportStatus.Queued);
        var processingCount = allItems.Count(q => q.Status == BggImportStatus.Processing);
        var completedCount = allItems.Count(q => q.Status == BggImportStatus.Completed);
        var failedCount = allItems.Count(q => q.Status == BggImportStatus.Failed);

        var currentItem = allItems
            .Where(q => q.Status == BggImportStatus.Processing)
            .Select(q => new BulkImportCurrentItemDto
            {
                BggId = q.BggId ?? 0,
                GameName = q.GameName,
                RetryCount = q.RetryCount
            })
            .FirstOrDefault();

        return new BulkImportProgressDto
        {
            Total = allItems.Count,
            Queued = queuedCount,
            Processing = processingCount,
            Completed = completedCount,
            Failed = failedCount,
            IsActive = queuedCount > 0 || processingCount > 0,
            EstimatedSecondsRemaining = queuedCount + processingCount,
            CurrentItem = currentItem,
            Timestamp = DateTime.UtcNow
        };
    }
}
