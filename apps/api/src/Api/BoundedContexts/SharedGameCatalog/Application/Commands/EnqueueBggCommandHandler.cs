using Api.Infrastructure.Entities;
using Api.Infrastructure.Services;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for EnqueueBggCommand
/// Adds a single BGG game to the import queue
/// </summary>
internal class EnqueueBggCommandHandler : IRequestHandler<EnqueueBggCommand, BggImportQueueEntity>
{
    private readonly IBggImportQueueService _queueService;
    private readonly ILogger<EnqueueBggCommandHandler> _logger;

    public EnqueueBggCommandHandler(
        IBggImportQueueService queueService,
        ILogger<EnqueueBggCommandHandler> logger)
    {
        _queueService = queueService ?? throw new ArgumentNullException(nameof(queueService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BggImportQueueEntity> Handle(
        EnqueueBggCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _queueService
                .EnqueueAsync(request.BggId, request.GameName, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            // Transform InvalidOperationException to ConflictException (Issue #3543 - Fix #2)
            _logger.LogWarning(
                ex,
                "BGG ID {BggId} is already queued or imported",
                request.BggId);
            throw new ConflictException($"BGG ID {request.BggId} is already queued or imported");
        }
    }
}
