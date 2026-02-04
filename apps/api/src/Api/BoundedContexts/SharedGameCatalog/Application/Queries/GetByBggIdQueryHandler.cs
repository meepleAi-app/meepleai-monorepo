using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetByBggIdQuery
/// Returns queue entry for a specific BGG ID
/// </summary>
internal class GetByBggIdQueryHandler : IRequestHandler<GetByBggIdQuery, BggImportQueueEntity?>
{
    private readonly IBggImportQueueService _queueService;

    public GetByBggIdQueryHandler(IBggImportQueueService queueService)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
    }

    public async Task<BggImportQueueEntity?> Handle(
        GetByBggIdQuery request,
        CancellationToken cancellationToken)
    {
        return await _queueService
            .GetByBggIdAsync(request.BggId, cancellationToken)
            .ConfigureAwait(false);
    }
}
