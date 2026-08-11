using Api.Observability;
using Api.SharedKernel.Infrastructure.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

namespace Api.SharedKernel.Infrastructure.Http;

/// <summary>
/// Issue #3495 findings H8/C3 (Slice E) — per-sink resilience for an outbound egress client: a
/// per-try timeout budget wrapped in a circuit breaker, both owned by THIS client only.
/// <para>
/// C3 is the reason this is a handler and not a shared <c>HttpClient</c>: the SSRF policy is shared
/// (one connect-pin callback), the budgets are not. A BGG CDN outage must not open the breaker of
/// the Slack or Commons sink, and a slow sink must not eat another sink's budget.
/// </para>
/// <para>
/// The timeout is a Polly PER-TRY budget rather than <see cref="HttpClient.Timeout"/>: the client
/// timeout is a single wall-clock ceiling shared by the whole exchange (including the streamed body
/// read), so it cannot express "give up on a stalled connect/headers quickly, but still allow a slow
/// large body". The client timeout stays on as the outer ceiling.
/// </para>
/// <para>
/// Rejections are counted on the egress metrics with their own bounded reason (<c>timeout</c> /
/// <c>breaker_open</c>, #3495 M2) BEFORE propagating, so a sink that silently degrades is visible
/// next to the SSRF blocks instead of being hidden inside a caller's catch-all.
/// </para>
/// </summary>
internal sealed class EgressResilienceHandler : DelegatingHandler
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly ILogger<EgressResilienceHandler> _logger;
    private readonly string _sink;

    /// <param name="logger">Logger for breaker state transitions.</param>
    /// <param name="sink">A bounded <c>MeepleAiMetrics.EgressSinks</c> constant — the metric label
    /// and the breaker's identity in the logs. Never a host or IP.</param>
    /// <param name="perTryTimeout">Budget for a single attempt (connect + response headers).</param>
    /// <param name="failureThreshold">Failures inside <paramref name="samplingWindow"/> that open the circuit.</param>
    /// <param name="samplingWindow">Rolling window the threshold is measured over.</param>
    /// <param name="breakDuration">How long the circuit stays open before probing again. Keep it at or
    /// below the client's handler lifetime: the factory recycles the handler chain (and with it this
    /// breaker's state) on that cadence, so a longer break would be cut short anyway.</param>
    public EgressResilienceHandler(
        ILogger<EgressResilienceHandler> logger,
        string sink,
        TimeSpan perTryTimeout,
        int failureThreshold,
        TimeSpan samplingWindow,
        TimeSpan breakDuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _sink = string.IsNullOrWhiteSpace(sink)
            ? throw new ArgumentException("Sink required.", nameof(sink))
            : sink;

        _pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            // Outer: the breaker observes the outcome of the timed attempt below, so a run of
            // timeouts opens the circuit just like a run of 5xx.
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 1.0,
                MinimumThroughput = failureThreshold,
                SamplingDuration = samplingWindow,
                BreakDuration = breakDuration,
                // Polly v7 and v8 both export TimeoutRejectedException, so it is matched by shape
                // rather than by symbolic type (CS0433) — see PollyRejectionDetector.
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<Exception>(static ex => PollyRejectionDetector.IsTimeoutRejected(ex))
                    .HandleResult(static r => (int)r.StatusCode >= 500),
                OnOpened = args =>
                {
                    _logger.LogError(
                        "Egress circuit breaker OPENED for sink '{Sink}' after {Threshold} failures in {Window}s; breaking for {BreakSeconds}s.",
                        _sink, failureThreshold, samplingWindow.TotalSeconds, args.BreakDuration.TotalSeconds);
                    return ValueTask.CompletedTask;
                },
                OnClosed = _ =>
                {
                    _logger.LogInformation("Egress circuit breaker CLOSED for sink '{Sink}' — upstream recovered.", _sink);
                    return ValueTask.CompletedTask;
                },
                OnHalfOpened = _ =>
                {
                    _logger.LogInformation("Egress circuit breaker HALF-OPENED for sink '{Sink}' — probing upstream.", _sink);
                    return ValueTask.CompletedTask;
                },
            })
            // Inner: per-attempt budget. Optimistic strategy — it cancels the token the inner send
            // observes, so the socket work actually stops instead of being abandoned.
            .AddTimeout(perTryTimeout)
            .Build();
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await _pipeline.ExecuteAsync(
                async token => await base.SendAsync(request, token).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (PollyRejectionDetector.IsBrokenCircuit(ex))
        {
            MeepleAiMetrics.RecordEgressBlocked(_sink, MeepleAiMetrics.EgressBlockReasons.BreakerOpen);
            throw;
        }
        catch (Exception ex) when (PollyRejectionDetector.IsTimeoutRejected(ex))
        {
            MeepleAiMetrics.RecordEgressBlocked(_sink, MeepleAiMetrics.EgressBlockReasons.Timeout);
            throw;
        }
    }
}
