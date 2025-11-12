namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Health status for LLM provider monitoring
/// ISSUE-962 (BGAI-020): Tracks provider availability and health
/// </summary>
public enum HealthStatus
{
    /// <summary>Provider is healthy and responding</summary>
    Healthy,

    /// <summary>Provider is degraded but operational</summary>
    Degraded,

    /// <summary>Provider is unhealthy and unavailable</summary>
    Unhealthy,

    /// <summary>Provider health is unknown (not yet checked)</summary>
    Unknown
}

/// <summary>
/// Provider health tracking with history
/// </summary>
public sealed class ProviderHealthStatus
{
    private const int MaxHealthCheckHistory = 10;
    private readonly Queue<(DateTime timestamp, bool success)> _healthCheckHistory = new();
    private readonly object _lock = new();

    public HealthStatus Status { get; private set; } = HealthStatus.Unknown;
    public DateTime? LastCheckAt { get; private set; }
    public DateTime? LastSuccessAt { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public int ConsecutiveSuccesses { get; private set; }
    public double SuccessRate { get; private set; } = 100.0;

    /// <summary>
    /// Record a health check result
    /// </summary>
    public void RecordHealthCheck(bool success)
    {
        lock (_lock)
        {
            LastCheckAt = DateTime.UtcNow;

            if (success)
            {
                LastSuccessAt = DateTime.UtcNow;
                ConsecutiveFailures = 0;
                ConsecutiveSuccesses++;

                // Healthy: 3+ consecutive successes OR success rate > 80%
                if (ConsecutiveSuccesses >= 3 || SuccessRate > 80)
                    Status = HealthStatus.Healthy;
                else if (SuccessRate > 50)
                    Status = HealthStatus.Degraded;
            }
            else
            {
                ConsecutiveSuccesses = 0;
                ConsecutiveFailures++;

                // Unhealthy: 3+ consecutive failures OR success rate < 20%
                if (ConsecutiveFailures >= 3 || SuccessRate < 20)
                    Status = HealthStatus.Unhealthy;
                else if (SuccessRate < 50)
                    Status = HealthStatus.Degraded;
            }

            // Update history
            _healthCheckHistory.Enqueue((DateTime.UtcNow, success));
            if (_healthCheckHistory.Count > MaxHealthCheckHistory)
            {
                _healthCheckHistory.Dequeue();
            }

            // Recalculate success rate
            var successCount = _healthCheckHistory.Count(h => h.success);
            SuccessRate = _healthCheckHistory.Count > 0
                ? (successCount * 100.0) / _healthCheckHistory.Count
                : 100.0;
        }
    }

    /// <summary>
    /// Check if provider should be used for routing
    /// </summary>
    public bool IsAvailable()
    {
        lock (_lock)
        {
            // Available if: Healthy, Degraded, or Unknown (benefit of the doubt)
            return Status != HealthStatus.Unhealthy;
        }
    }

    /// <summary>
    /// Get formatted health status
    /// </summary>
    public string GetStatusSummary()
    {
        lock (_lock)
        {
            var timeSinceLastCheck = LastCheckAt.HasValue
                ? $"{(DateTime.UtcNow - LastCheckAt.Value).TotalSeconds:F0}s ago"
                : "never";

            return $"{Status} (success rate: {SuccessRate:F1}%, " +
                   $"consecutive: {(ConsecutiveSuccesses > 0 ? $"+{ConsecutiveSuccesses}" : $"-{ConsecutiveFailures}")}, " +
                   $"last check: {timeSinceLastCheck})";
        }
    }

    /// <summary>
    /// Get health check history snapshot
    /// </summary>
    public List<(DateTime timestamp, bool success)> GetHistory()
    {
        lock (_lock)
        {
            return _healthCheckHistory.ToList();
        }
    }
}
