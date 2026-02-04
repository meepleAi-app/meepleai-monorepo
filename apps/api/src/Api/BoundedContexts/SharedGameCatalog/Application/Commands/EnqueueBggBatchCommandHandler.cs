using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for EnqueueBggBatchCommand
/// Adds multiple BGG games to the import queue
/// </summary>
internal class EnqueueBggBatchCommandHandler : IRequestHandler<EnqueueBggBatchCommand, List<BggImportQueueEntity>>
{
    private readonly IBggImportQueueService _queueService;
    private readonly ILogger<EnqueueBggBatchCommandHandler> _logger;

    public EnqueueBggBatchCommandHandler(
        IBggImportQueueService queueService,
        ILogger<EnqueueBggBatchCommandHandler> logger)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<BggImportQueueEntity>> Handle(
        EnqueueBggBatchCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Enqueuing batch of {Count} BGG IDs",
            request.BggIds.Count);

        return await _queueService
            .EnqueueBatchAsync(request.BggIds, cancellationToken)
            .ConfigureAwait(false);
    }
}
