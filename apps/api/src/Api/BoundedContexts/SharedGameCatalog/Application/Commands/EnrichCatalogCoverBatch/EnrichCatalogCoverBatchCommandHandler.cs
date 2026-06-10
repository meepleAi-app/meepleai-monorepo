using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch;

/// <summary>
/// Handler for <see cref="EnrichCatalogCoverBatchCommand"/>. Dispatches one
/// <see cref="EnrichCatalogCoverCommand"/> per game id through
/// <see cref="IMediator"/>, capturing per-game outcomes and aggregating
/// bucket counters. Issue #2123 Phase B.
/// </summary>
/// <remarks>
/// Sequential dispatch is intentional: parallel dispatch would defeat the
/// 1 req/sec Wikimedia SPARQL rate limit enforced by the M8 single-entry
/// handler (the rate-limiter is a singleton; parallel calls would still
/// serialize at the rate-limit boundary, but the order of completion would
/// become nondeterministic and harder to reason about for the operator).
/// </remarks>
internal sealed class EnrichCatalogCoverBatchCommandHandler
    : ICommandHandler<EnrichCatalogCoverBatchCommand, EnrichCatalogCoverBatchResult>
{
    private const string OutcomeSuccess = "success";
    private const string OutcomeSkipped = "skipped";
    private const string OutcomeFailed = "failed";
    private const string UnhandledExceptionReason = "unhandled-exception";

    private readonly IMediator _mediator;
    private readonly ILogger<EnrichCatalogCoverBatchCommandHandler> _logger;

    public EnrichCatalogCoverBatchCommandHandler(
        IMediator mediator,
        ILogger<EnrichCatalogCoverBatchCommandHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnrichCatalogCoverBatchResult> Handle(
        EnrichCatalogCoverBatchCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var totalRequested = command.GameIds.Count;
        var perGame = new List<EnrichCatalogCoverBatchEntry>(totalRequested);
        var successCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        _logger.LogInformation(
            "EnrichCatalogCoverBatch starting: {Count} game(s)", totalRequested);

        foreach (var gameId in command.GameIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            EnrichCatalogCoverResult childResult;
            try
            {
                childResult = await _mediator
                    .Send(new EnrichCatalogCoverCommand(gameId), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "EnrichCatalogCoverBatch: unhandled exception for GameId={GameId}, continuing batch",
                    gameId);
                perGame.Add(new EnrichCatalogCoverBatchEntry(gameId, OutcomeFailed, UnhandledExceptionReason));
                failedCount++;
                continue;
            }

            switch (childResult)
            {
                case EnrichCatalogCoverResult.Success:
                    perGame.Add(new EnrichCatalogCoverBatchEntry(gameId, OutcomeSuccess, Reason: null));
                    successCount++;
                    break;
                case EnrichCatalogCoverResult.Skipped skipped:
                    perGame.Add(new EnrichCatalogCoverBatchEntry(gameId, OutcomeSkipped, skipped.Reason));
                    skippedCount++;
                    break;
                case EnrichCatalogCoverResult.Failed failed:
                    perGame.Add(new EnrichCatalogCoverBatchEntry(gameId, OutcomeFailed, failed.Reason));
                    failedCount++;
                    break;
                default:
                    // Defensive: unknown discriminated variant. Surface as failed.
                    perGame.Add(new EnrichCatalogCoverBatchEntry(gameId, OutcomeFailed, "unknown-result-variant"));
                    failedCount++;
                    break;
            }
        }

        _logger.LogInformation(
            "EnrichCatalogCoverBatch completed: total={Total}, success={Success}, skipped={Skipped}, failed={Failed}",
            totalRequested, successCount, skippedCount, failedCount);

        return new EnrichCatalogCoverBatchResult(
            totalRequested, successCount, skippedCount, failedCount, perGame);
    }
}
