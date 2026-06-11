using Polly;
using Polly.CircuitBreaker;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Resilience;

/// <summary>
/// Issue #1823 Wave 3 M10 (ADR DEC-3f) — Polly v8 circuit-breaker delegating
/// handler for the Wikidata + Commons HttpClients. OPEN the circuit when 3
/// consecutive 5xx (or transient HttpRequestException) occur within a 60s
/// window; remain OPEN for 5 minutes before half-opening.
/// </summary>
/// <remarks>
/// <para>One handler instance is registered PER TYPED CLIENT (Wikidata SPARQL
/// vs Commons API) so a SPARQL outage does NOT collapse Commons traffic and
/// vice-versa. ADR DEC-3e keeps the rate-limiter shared; ADR DEC-3f keeps the
/// circuit breaker per-endpoint.</para>
///
/// <para>Failure mapping:</para>
/// <list type="bullet">
///   <item><c>HttpRequestException</c> — transient network failure (DNS, TCP
///   reset, TLS handshake). Counts towards the threshold.</item>
///   <item><c>5xx HTTP status</c> — server-side outage signal. Counts.</item>
///   <item><c>4xx HTTP status</c> — client-side. Does NOT count (the breaker
///   is for upstream-health detection, not for app-logic errors).</item>
///   <item><see cref="OperationCanceledException"/> — caller cancellation.
///   Not handled by the breaker, propagates verbatim.</item>
/// </list>
///
/// <para>While the circuit is OPEN, callers receive a
/// <c>BrokenCircuitException</c> (Polly.CircuitBreaker namespace) immediately.
/// The M8 client wrappers already catch <c>HttpRequestException</c> + return
/// their "not available" sentinel; the broken-circuit exception derives from
/// <c>ExecutionRejectedException</c>, not from <c>HttpRequestException</c>, so
/// it propagates uncaught to the M9 scheduler which records the failure under
/// the catch-all per-game exception handler in
/// <c>WikidataCoverEnrichmentJob.RunBatchAsync</c>. A follow-up will add a
/// dedicated <c>circuit-open</c> outcome reason as part of the M11 metric
/// expansion.</para>
/// </remarks>
internal sealed class WikimediaCircuitBreakerHandler : DelegatingHandler
{
    /// <summary>Threshold for OPENing the circuit per DEC-3f.</summary>
    internal const int ConsecutiveFailureThreshold = 3;

    /// <summary>Sampling window per DEC-3f.</summary>
    internal static readonly TimeSpan SamplingWindow = TimeSpan.FromSeconds(60);

    /// <summary>OPEN-state duration per DEC-3f.</summary>
    internal static readonly TimeSpan BreakDuration = TimeSpan.FromMinutes(5);

    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ILogger<WikimediaCircuitBreakerHandler> _logger;
    private readonly string _clientName;

    public WikimediaCircuitBreakerHandler(
        ILogger<WikimediaCircuitBreakerHandler> logger,
        string clientName)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientName = string.IsNullOrWhiteSpace(clientName)
            ? throw new ArgumentException("Client name required.", nameof(clientName))
            : clientName;

        _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                // FailureRatio = 1.0 + MinimumThroughput = 3 + SamplingDuration = 60s
                // = "open after 3 failures-in-a-row inside any rolling 60s window".
                // This is the closest Polly-native equivalent to DEC-3f's
                // "3 consecutive 5xx within 60s" — the breaker DOES require all
                // 3 calls in the throughput window to be failures, which on small
                // samples (3-5 calls/min during a batch) maps cleanly to the
                // "consecutive" intent.
                FailureRatio = 1.0,
                MinimumThroughput = ConsecutiveFailureThreshold,
                SamplingDuration = SamplingWindow,
                BreakDuration = BreakDuration,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(static r => (int)r.StatusCode >= 500),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "Wikimedia circuit breaker OPENED for client '{Client}' after {Threshold} failures inside {Window}s. Breaking for {BreakSeconds}s.",
                        _clientName,
                        ConsecutiveFailureThreshold,
                        SamplingWindow.TotalSeconds,
                        args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    _logger.LogInformation(
                        "Wikimedia circuit breaker CLOSED for client '{Client}' — upstream recovered.",
                        _clientName);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = args =>
                {
                    _logger.LogInformation(
                        "Wikimedia circuit breaker HALF-OPENED for client '{Client}' — probing upstream.",
                        _clientName);
                    return ValueTask.CompletedTask;
                },
            })
            .Build();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // Delegate the inner call inside the resilience pipeline. The pipeline
        // catches HttpRequestException + 5xx responses to drive the breaker
        // state machine; BrokenCircuitException is thrown on OPEN state.
        return await _pipeline.ExecuteAsync(
            async token => await base.SendAsync(request, token).ConfigureAwait(false),
            cancellationToken).ConfigureAwait(false);
    }
}
