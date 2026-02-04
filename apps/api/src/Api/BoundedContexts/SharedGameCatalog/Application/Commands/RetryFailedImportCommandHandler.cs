using Api.Infrastructure.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for RetryFailedImportCommand
/// Re-queues a failed BGG import job
/// </summary>
internal class RetryFailedImportCommandHandler : IRequestHandler<RetryFailedImportCommand, bool>
{
    private readonly IBggImportQueueService _queueService;
    private readonly ILogger<RetryFailedImportCommandHandler> _logger;

    public RetryFailedImportCommandHandler(
        IBggImportQueueService queueService,
        ILogger<RetryFailedImportCommandHandler> logger)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(
        RetryFailedImportCommand request,
        CancellationToken cancellationToken)
    {
        var retried = await _queueService
            .RetryFailedAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (retried)
        {
            _logger.LogInformation(
                "Successfully retried failed BGG import: {Id}",
                request.Id);
        }
        else
        {
            _logger.LogWarning(
                "Could not retry BGG import {Id}: not found or not in Failed status",
                request.Id);
        }

        return retried;
    }
}
