using Api.Infrastructure.Services;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for CancelQueuedImportCommand
/// Removes a queued BGG import job
/// </summary>
internal class CancelQueuedImportCommandHandler : IRequestHandler<CancelQueuedImportCommand, bool>
{
    private readonly IBggImportQueueService _queueService;
    private readonly ILogger<CancelQueuedImportCommandHandler> _logger;

    public CancelQueuedImportCommandHandler(
        IBggImportQueueService queueService,
        ILogger<CancelQueuedImportCommandHandler> logger)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(
        CancelQueuedImportCommand request,
        CancellationToken cancellationToken)
    {
        var cancelled = await _queueService
            .CancelAsync(request.Id, cancellationToken)
            .ConfigureAwait(false);

        if (cancelled)
        {
            _logger.LogInformation(
                "Successfully cancelled BGG import queue entry: {Id}",
                request.Id);
        }
        else
        {
            _logger.LogWarning(
                "Could not cancel BGG import queue entry {Id}: not found or not in Queued status",
                request.Id);
        }

        return cancelled;
    }
}
