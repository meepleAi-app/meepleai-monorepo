namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

internal static class HealthThresholds
{
    public static readonly IReadOnlyDictionary<string, int> LatencyDegradedMs =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["embedding"] = 2000,
            ["reranker"] = 2000,
            ["smoldocling"] = 5000,
            ["unstructured"] = 5000,
            ["orchestrator"] = 3000,
            ["ollama"] = 5000,
            ["postgres"] = 500,
            ["redis"] = 100
        };

    public const double ErrorRateDegradedPercent = 5.0;
    public const double ErrorRateCriticalPercent = 20.0;

    public static ServiceHealthLevel DetermineHealth(
        string serviceName, bool healthCheckPassed, double avgLatencyMs, double errorRate24h)
    {
        if (!healthCheckPassed)
            return ServiceHealthLevel.Down;

        var latencyThreshold = LatencyDegradedMs.GetValueOrDefault(serviceName, 2000);

        if (errorRate24h >= ErrorRateCriticalPercent)
            return ServiceHealthLevel.Down;

        if (avgLatencyMs > latencyThreshold || errorRate24h >= ErrorRateDegradedPercent)
            return ServiceHealthLevel.Degraded;

        return ServiceHealthLevel.Healthy;
    }
}

internal enum ServiceHealthLevel
{
    Healthy = 0,
    Degraded = 1,
    Down = 2,
    Restarting = 3,
    Unknown = 4
}
