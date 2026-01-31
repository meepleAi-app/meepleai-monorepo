namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Latency statistics tracking for LLM provider performance monitoring
/// ISSUE-962 (BGAI-020): Tracks response time metrics per provider
/// </summary>
internal sealed class LatencyStats
{
    private const int MaxSamples = 100; // Rolling window size
    private readonly Queue<double> _samples = new();
#pragma warning disable MA0158 // Use System.Threading.Lock
    private readonly object _lock = new();
#pragma warning restore MA0158

    public int TotalRequests { get; private set; }
    public double MinLatencyMs { get; private set; } = double.MaxValue;
    public double MaxLatencyMs { get; private set; }
    public double AvgLatencyMs { get; private set; }
    public DateTime? LastRequestAt { get; private set; }

    /// <summary>
    /// Record a request latency measurement
    /// </summary>
    /// <param name="latencyMs">Latency in milliseconds</param>
    public void RecordLatency(double latencyMs)
    {
        lock (_lock)
        {
            TotalRequests++;
            LastRequestAt = DateTime.UtcNow;

            // Update min/max
            if (latencyMs < MinLatencyMs) MinLatencyMs = latencyMs;
            if (latencyMs > MaxLatencyMs) MaxLatencyMs = latencyMs;

            // Add to rolling window
            _samples.Enqueue(latencyMs);
            if (_samples.Count > MaxSamples)
            {
                _samples.Dequeue(); // Remove oldest
            }

            // Recalculate average
            AvgLatencyMs = _samples.Average();
        }
    }

    /// <summary>
    /// Get formatted statistics summary
    /// </summary>
    public string GetSummary()
    {
        lock (_lock)
        {
            if (TotalRequests == 0)
                return "No requests yet";

            return $"Requests: {TotalRequests}, " +
                   $"Avg: {AvgLatencyMs:F2}ms, " +
                   $"Min: {MinLatencyMs:F2}ms, " +
                   $"Max: {MaxLatencyMs:F2}ms, " +
                   $"Samples: {_samples.Count}";
        }
    }

    /// <summary>
    /// Get current statistics snapshot
    /// </summary>
    public (int total, double avg, double min, double max, int samples) GetSnapshot()
    {
        lock (_lock)
        {
            return (TotalRequests, AvgLatencyMs, MinLatencyMs, MaxLatencyMs, _samples.Count);
        }
    }

    /// <summary>
    /// Check if provider is performing well (avg latency below threshold)
    /// </summary>
    /// <param name="thresholdMs">Maximum acceptable average latency in ms</param>
    public bool IsPerformingWell(double thresholdMs = 5000)
    {
        lock (_lock)
        {
            return TotalRequests == 0 || AvgLatencyMs <= thresholdMs;
        }
    }
}
