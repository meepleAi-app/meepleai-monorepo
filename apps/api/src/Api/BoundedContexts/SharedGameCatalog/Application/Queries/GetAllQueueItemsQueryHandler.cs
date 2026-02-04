using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetAllQueueItemsQuery
/// Returns all queue items regardless of status (for SSE)
/// </summary>
internal class GetAllQueueItemsQueryHandler : IRequestHandler<GetAllQueueItemsQuery, List<BggImportQueueEntity>>
{
    private readonly IBggImportQueueService _queueService;

    public GetAllQueueItemsQueryHandler(IBggImportQueueService queueService)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
    }

    public async Task<List<BggImportQueueEntity>> Handle(
        GetAllQueueItemsQuery request,
        CancellationToken cancellationToken)
    {
        return await _queueService
            .GetAllQueueItemsAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
