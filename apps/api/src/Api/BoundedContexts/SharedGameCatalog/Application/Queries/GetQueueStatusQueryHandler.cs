using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetQueueStatusQuery
/// Returns queued and processing BGG import items
/// </summary>
internal class GetQueueStatusQueryHandler : IRequestHandler<GetQueueStatusQuery, List<BggImportQueueEntity>>
{
    private readonly IBggImportQueueService _queueService;

    public GetQueueStatusQueryHandler(IBggImportQueueService queueService)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
    }

    public async Task<List<BggImportQueueEntity>> Handle(
        GetQueueStatusQuery request,
        CancellationToken cancellationToken)
    {
        return await _queueService
            .GetQueueStatusAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
