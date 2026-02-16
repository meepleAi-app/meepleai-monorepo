using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.MultiAgentRouter;

/// <summary>
/// Collects and exposes routing metrics for monitoring and quality tracking.
/// Issue #4336: Multi-Agent Router - Routing Metrics and Monitoring.
/// </summary>
/// <remarks>
/// Thread-safe in-memory metrics collector. Metrics are reset periodically.
/// For production: integrate with Application Insights / Prometheus.
/// </remarks>
internal sealed class RoutingMetricsCollector
{
    private readonly ILogger<RoutingMetricsCollector> _logger;
    private readonly ConcurrentQueue<RoutingMetricEntry> _recentDecisions = new();
    private long _totalDecisions;
    private long _highConfidenceCount;
    private long _mediumConfidenceCount;
    private long _lowConfidenceCount;
    private long _fallbackCount;
    private double _totalConfidence;
    private double _totalRoutingLatencyMs;

    // Per-agent counters
    private readonly ConcurrentDictionary<string, long> _agentUsageCounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<AgentIntent, long> _intentCounts = new();

    private const int MaxRecentDecisions = 1000;

    public RoutingMetricsCollector(ILogger<RoutingMetricsCollector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Records a routing decision for metrics tracking.
    /// </summary>
    public void RecordRoutingDecision(AgentRoutingDecision decision)
    {
        Interlocked.Increment(ref _totalDecisions);

        // Confidence buckets
        if (decision.Confidence >= AgentRouterService.HighConfidenceThreshold)
            Interlocked.Increment(ref _highConfidenceCount);
        else if (decision.Confidence >= AgentRouterService.MediumConfidenceThreshold)
            Interlocked.Increment(ref _mediumConfidenceCount);
        else
            Interlocked.Increment(ref _lowConfidenceCount);

        if (decision.FallbackAgents != null)
            Interlocked.Increment(ref _fallbackCount);

        // Running totals for averages (lock-free CAS loop)
        AddToDouble(ref _totalConfidence, decision.Confidence);
        AddToDouble(ref _totalRoutingLatencyMs, decision.RoutingDuration.TotalMilliseconds);

        // Per-agent usage
        _agentUsageCounts.AddOrUpdate(decision.TargetAgent, 1, (_, count) => count + 1);

        // Per-intent counts
        _intentCounts.AddOrUpdate(decision.Intent, 1, (_, count) => count + 1);

        // Keep recent decisions for inspection (bounded queue)
        _recentDecisions.Enqueue(new RoutingMetricEntry(
            decision.TargetAgent,
            decision.Intent,
            decision.Confidence,
            decision.RoutingDuration.TotalMilliseconds,
            DateTime.UtcNow));

        while (_recentDecisions.Count > MaxRecentDecisions)
            _recentDecisions.TryDequeue(out _);

        _logger.LogDebug(
            "[RoutingMetrics] Recorded decision: agent={Agent}, intent={Intent}, confidence={Confidence:F3}",
            decision.TargetAgent,
            decision.Intent,
            decision.Confidence);
    }

    /// <summary>
    /// Gets a snapshot of current routing metrics.
    /// </summary>
    public RoutingMetricsSnapshot GetSnapshot()
    {
        var total = Interlocked.Read(ref _totalDecisions);

        return new RoutingMetricsSnapshot(
            TotalDecisions: total,
            HighConfidenceCount: Interlocked.Read(ref _highConfidenceCount),
            MediumConfidenceCount: Interlocked.Read(ref _mediumConfidenceCount),
            LowConfidenceCount: Interlocked.Read(ref _lowConfidenceCount),
            FallbackCount: Interlocked.Read(ref _fallbackCount),
            AverageConfidence: total > 0 ? _totalConfidence / total : 0.0,
            AverageRoutingLatencyMs: total > 0 ? _totalRoutingLatencyMs / total : 0.0,
            AgentUsageDistribution: new Dictionary<string, long>(_agentUsageCounts, StringComparer.Ordinal),
            IntentDistribution: new Dictionary<AgentIntent, long>(_intentCounts),
            FallbackRate: total > 0 ? (double)Interlocked.Read(ref _fallbackCount) / total : 0.0
        );
    }

    #pragma warning disable S1244 // Intentional CAS loop for lock-free atomic double addition
    private static void AddToDouble(ref double target, double value)
    {
        double current;
        double newValue;
        do
        {
            current = Volatile.Read(ref target);
            newValue = current + value;
        }
        while (Interlocked.CompareExchange(ref target, newValue, current) != current);
    }
    #pragma warning restore S1244
}

/// <summary>
/// Single routing metric entry for recent decisions.
/// </summary>
internal sealed record RoutingMetricEntry(
    string TargetAgent,
    AgentIntent Intent,
    double Confidence,
    double RoutingLatencyMs,
    DateTime Timestamp);

/// <summary>
/// Snapshot of aggregated routing metrics.
/// </summary>
internal sealed record RoutingMetricsSnapshot(
    long TotalDecisions,
    long HighConfidenceCount,
    long MediumConfidenceCount,
    long LowConfidenceCount,
    long FallbackCount,
    double AverageConfidence,
    double AverageRoutingLatencyMs,
    Dictionary<string, long> AgentUsageDistribution,
    Dictionary<AgentIntent, long> IntentDistribution,
    double FallbackRate);
