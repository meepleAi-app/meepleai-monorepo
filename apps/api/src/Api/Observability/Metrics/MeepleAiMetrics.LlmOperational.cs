// OPS-02: LLM operational maturity metrics (Issue #5480)
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    // ─── Issue #5480: LLM Operational Maturity Metrics ──────────────────────

    #region LLM Operational Metrics (Issue #5480)

    /// <summary>
    /// Gauge for circuit breaker state per provider.
    /// Issue #5480: 0=Closed (healthy), 1=Open (blocked), 2=HalfOpen (testing).
    /// </summary>
    public static readonly UpDownCounter<int> CircuitBreakerState = Meter.CreateUpDownCounter<int>(
        name: "meepleai.llm.circuit_breaker_state",
        unit: "{state}",
        description: "Circuit breaker state per LLM provider (0=closed, 1=open, 2=halfopen)");

    /// <summary>
    /// Gauge for OpenRouter account balance in USD.
    /// Issue #5480: Scraped from /auth/key — enables budget alerting.
    /// </summary>
    public static readonly UpDownCounter<double> OpenRouterBalanceUsd = Meter.CreateUpDownCounter<double>(
        name: "meepleai.openrouter.balance_usd",
        unit: "USD",
        description: "OpenRouter account balance in USD");

    /// <summary>
    /// Gauge for OpenRouter RPM utilization (0.0 to 1.0).
    /// Issue #5480: Current RPM / Limit RPM — enables threshold alerting.
    /// </summary>
    public static readonly UpDownCounter<double> OpenRouterRpmUtilization = Meter.CreateUpDownCounter<double>(
        name: "meepleai.openrouter.rpm_utilization",
        unit: "ratio",
        description: "OpenRouter RPM utilization ratio (0.0-1.0)");

    /// <summary>
    /// Counter for total LLM cost in USD per provider and model.
    /// Issue #5480: Aggregated cost tracking for billing dashboards.
    /// </summary>
    public static readonly Counter<double> LlmCostUsdTotal = Meter.CreateCounter<double>(
        name: "meepleai.llm.cost_usd_total",
        unit: "USD",
        description: "Total LLM cost in USD by provider and model");

    /// <summary>
    /// Counter for total LLM requests by provider, model, and status.
    /// Issue #5480: Enables per-provider request volume and error rate tracking.
    /// </summary>
    public static readonly Counter<long> LlmRequestsTotal = Meter.CreateCounter<long>(
        name: "meepleai.llm.requests_total",
        unit: "requests",
        description: "Total LLM requests by provider, model, and status");

    /// <summary>
    /// Histogram for LLM request latency in seconds by provider and model.
    /// Issue #5480: Enables P50/P95/P99 latency dashboards per provider.
    /// </summary>
    public static readonly Histogram<double> LlmLatencySeconds = Meter.CreateHistogram<double>(
        name: "meepleai.llm.latency_seconds",
        unit: "s",
        description: "LLM request latency in seconds by provider and model");

    /// <summary>
    /// Records a circuit breaker state change for Prometheus export.
    /// Issue #5480: Called from CircuitBreakerState.OnStateTransition callback.
    /// </summary>
    public static void RecordCircuitBreakerState(string provider, int stateValue)
    {
        // Reset to 0 and then set to new value using UpDownCounter
        // Since this is a gauge-like metric, consumers should take the last value
        CircuitBreakerState.Add(stateValue, new TagList { { "provider", provider } });
    }

    /// <summary>
    /// Records an LLM request with provider, model, and status for Prometheus export.
    /// Issue #5480: Called from HybridLlmService after each completion.
    /// </summary>
    public static void RecordLlmRequest(
        string provider,
        string model,
        string status,
        double latencySeconds,
        double? costUsd = null)
    {
        var tags = new TagList
        {
            { "provider", provider },
            { "model", model },
            { "status", status }
        };

        LlmRequestsTotal.Add(1, tags);
        LlmLatencySeconds.Record(latencySeconds, tags);

        if (costUsd.HasValue && costUsd.Value > 0)
        {
            LlmCostUsdTotal.Add(costUsd.Value, new TagList
            {
                { "provider", provider },
                { "model", model }
            });
        }
    }

    /// <summary>
    /// Records OpenRouter operational gauges for Prometheus export.
    /// Issue #5480: Called from OpenRouterUsageService polling cycle.
    /// </summary>
    public static void RecordOpenRouterOperationalState(
        double balanceUsd,
        double rpmUtilization)
    {
        OpenRouterBalanceUsd.Add(balanceUsd, new TagList { { "provider", "openrouter" } });
        OpenRouterRpmUtilization.Add(rpmUtilization, new TagList { { "provider", "openrouter" } });
    }

    #endregion
}
