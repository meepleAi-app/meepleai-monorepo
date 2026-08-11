using Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch;

/// <summary>
/// Handler for <see cref="EnrichCatalogCoverBatchCommand"/>. Delegates each game
/// id to the <see cref="IWikidataCoverEnrichmentRunner"/> (Issue #3369) — the
/// single source of truth that records a <c>WikidataCoverEnrichmentAttempt</c>
/// row, applies the DEC-3j retry/dead-letter policy and broadcasts SSE — instead
/// of dispatching the raw <see cref="EnrichCatalogCoverCommand"/> via IMediator,
/// then aggregates the per-game outcomes into bucket counters. Batch items now
/// get the same observability/retry parity as the M9 scheduler and M12 admin
/// trigger. Issue #2123 Phase B + Issue #2157 (per-game resilience).
/// </summary>
/// <remarks>
/// <para>
/// Sequential dispatch is intentional: parallel dispatch would defeat the
/// 1 req/sec Wikimedia SPARQL rate limit enforced by the M8 single-entry
/// handler (the rate-limiter is a singleton; parallel calls would still
/// serialize at the rate-limit boundary, but the order of completion would
/// become nondeterministic and harder to reason about for the operator).
/// </para>
/// <para>
/// Issue #2157 defense-in-depth catch hierarchy: even though the M3/M4
/// providers map <see cref="System.Net.Http.HttpClient.Timeout"/> to NotFound
/// internally, the batch handler still guards against any future leak from
/// the M8 single-entry handler — a single per-game exception MUST NOT abort
/// the entire batch. The hierarchy distinguishes:
/// <list type="bullet">
///   <item><c>OperationCanceledException</c> + caller token cancelled → rethrow (batch aborts cleanly)</item>
///   <item><c>OperationCanceledException</c> + caller token NOT cancelled → <c>"child-timeout"</c> (HttpClient.Timeout leak)</item>
///   <item><c>HttpRequestException</c> → <c>"http-error"</c> (DNS, connection refused, 5xx that bypasses provider catch)</item>
///   <item>Polly <c>BrokenCircuitException</c> → <c>"circuit-open"</c> (M10 circuit breaker tripped)</item>
///   <item>Any other <see cref="System.Exception"/> → <c>"unhandled-exception"</c> (catch-all fallback)</item>
/// </list>
/// Each non-cancellation path records an entry in <see cref="EnrichCatalogCoverBatchResult.FailedDetails"/>
/// so the admin UI can drill down into the underlying exception type + message.
/// </para>
/// </remarks>
internal sealed class EnrichCatalogCoverBatchCommandHandler
    : ICommandHandler<EnrichCatalogCoverBatchCommand, EnrichCatalogCoverBatchResult>
{
    private const string OutcomeSuccess = "success";
    private const string OutcomeSkipped = "skipped";
    private const string OutcomeFailed = "failed";
    private const string UnhandledExceptionReason = "unhandled-exception";
    private const string ChildTimeoutReason = "child-timeout";
    private const string HttpErrorReason = "http-error";
    private const string CircuitOpenReason = "circuit-open";
    private const string BrokenCircuitExceptionTypeName = "BrokenCircuitException";

    private readonly IWikidataCoverEnrichmentRunner _runner;
    private readonly ILogger<EnrichCatalogCoverBatchCommandHandler> _logger;

    public EnrichCatalogCoverBatchCommandHandler(
        IWikidataCoverEnrichmentRunner runner,
        ILogger<EnrichCatalogCoverBatchCommandHandler> logger)
    {
        _runner = runner ?? throw new ArgumentNullException(nameof(runner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EnrichCatalogCoverBatchResult> Handle(
        EnrichCatalogCoverBatchCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var totalRequested = command.GameIds.Count;
        var perGame = new List<EnrichCatalogCoverBatchEntry>(totalRequested);
        var failedDetails = new List<EnrichCatalogCoverBatchFailedDetail>();
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
                // #3369: delegate through the runner (SSOT) so each batch item
                // records an attempt row + retry/dead-letter classification + SSE,
                // matching the M9 scheduler and M12 admin trigger. forceRefresh is
                // false (the batch honours the DEC-3i freshness window); no admin
                // trigger id is threaded yet (follow-up note in #3369).
                childResult = await _runner
                    .EnrichAndRecordAsync(gameId, forceRefresh: false, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }
            // Issue #2157: real caller cancellation propagates so the admin
            // client sees a clean abort. The `when (cancellationToken.IsCancellationRequested)`
            // guard MUST come before the timeout-leak branch — otherwise
            // HttpClient.Timeout leaks would be conflated with user cancel.
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            // Issue #2157: HttpClient.Timeout leak (TaskCanceledException raised
            // from inside the provider chain when the caller token is NOT
            // cancelled). Map to per-game "child-timeout" so the batch
            // continues with the next id and the admin UI can retry just the
            // timed-out subset.
            catch (OperationCanceledException ex)
            {
                _logger.LogError(ex,
                    "EnrichCatalogCoverBatch: child timeout for GameId={GameId}, continuing batch",
                    gameId);
                perGame.Add(new EnrichCatalogCoverBatchEntry(gameId, OutcomeFailed, ChildTimeoutReason));
                failedDetails.Add(new EnrichCatalogCoverBatchFailedDetail(gameId, ex.GetType().Name, ex.Message));
                failedCount++;
                continue;
            }
            // Issue #2157: any HttpRequestException leaked from the M8 chain
            // (DNS failure, connection refused, 5xx that bypassed the
            // provider's catch) maps to "http-error" so the admin UI can
            // distinguish upstream HTTP outages from circuit-breaker trips.
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex,
                    "EnrichCatalogCoverBatch: HTTP error for GameId={GameId}, continuing batch",
                    gameId);
                perGame.Add(new EnrichCatalogCoverBatchEntry(gameId, OutcomeFailed, HttpErrorReason));
                failedDetails.Add(new EnrichCatalogCoverBatchFailedDetail(gameId, ex.GetType().Name, ex.Message));
                failedCount++;
                continue;
            }
            // Issue #2157: Polly BrokenCircuitException raised by the
            // WikimediaCircuitBreakerHandler (M10) when the circuit is OPEN.
            // Detected via reflection (Polly v7+v8 export the same FQN, see
            // CircuitBreakerExceptionDetector). Recorded as "circuit-open" so
            // the M9 scheduler / admin retry surface can distinguish transient
            // breaker trips from genuine HTTP failures.
            catch (Exception ex) when (CircuitBreakerExceptionDetector.IsBrokenCircuit(ex))
            {
                _logger.LogError(ex,
                    "EnrichCatalogCoverBatch: circuit OPEN for GameId={GameId}, continuing batch",
                    gameId);
                perGame.Add(new EnrichCatalogCoverBatchEntry(gameId, OutcomeFailed, CircuitOpenReason));
                failedDetails.Add(new EnrichCatalogCoverBatchFailedDetail(gameId, BrokenCircuitExceptionTypeName, ex.Message));
                failedCount++;
                continue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "EnrichCatalogCoverBatch: unhandled exception for GameId={GameId}, continuing batch",
                    gameId);
                perGame.Add(new EnrichCatalogCoverBatchEntry(gameId, OutcomeFailed, UnhandledExceptionReason));
                failedDetails.Add(new EnrichCatalogCoverBatchFailedDetail(gameId, ex.GetType().Name, ex.Message));
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

        // Issue #2157: FailedDetails stays null when no exception path fired
        // (in-band Failed results carry their reason on the per-game entry).
        // Preserves the JSON contract for the green-path case.
        return new EnrichCatalogCoverBatchResult(
            totalRequested,
            successCount,
            skippedCount,
            failedCount,
            perGame,
            failedDetails.Count > 0 ? failedDetails : null);
    }
}
