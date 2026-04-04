using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;

internal sealed class BatchAddRagToSharedGameCommandHandler
    : ICommandHandler<BatchAddRagToSharedGameCommand, BatchAddRagToSharedGameResult>
{
    private readonly IMediator _mediator;
    private readonly ILogger<BatchAddRagToSharedGameCommandHandler> _logger;

    public BatchAddRagToSharedGameCommandHandler(
        IMediator mediator,
        ILogger<BatchAddRagToSharedGameCommandHandler> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<BatchAddRagToSharedGameResult> Handle(
        BatchAddRagToSharedGameCommand request, CancellationToken cancellationToken)
    {
        var results = new List<BatchItemResult>();

        foreach (var item in request.Items)
        {
            try
            {
                var result = await _mediator.Send(item, cancellationToken).ConfigureAwait(false);
                results.Add(new BatchItemResult(item.SharedGameId, item.File.FileName, result, null));
            }
            catch (OperationCanceledException)
            {
                throw; // propagate cancellation
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Batch RAG: Failed for SharedGame {GameId}, file {FileName}",
                    item.SharedGameId, item.File.FileName);
                results.Add(new BatchItemResult(item.SharedGameId, item.File.FileName, null, ex.Message));
            }
        }

        return new BatchAddRagToSharedGameResult(
            Results: results,
            SuccessCount: results.Count(r => r.Result != null),
            FailureCount: results.Count(r => r.Error != null)
        );
    }
}
