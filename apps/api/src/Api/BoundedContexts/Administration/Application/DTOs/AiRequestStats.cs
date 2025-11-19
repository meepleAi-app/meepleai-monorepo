namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Statistics about AI requests for analytics and monitoring.
/// </summary>
public record AiRequestStats
{
    /// <summary>
    /// Total number of AI requests.
    /// </summary>
    public required int TotalRequests { get; init; }

    /// <summary>
    /// Average latency in milliseconds.
    /// </summary>
    public required double AvgLatencyMs { get; init; }

    /// <summary>
    /// Total number of tokens consumed.
    /// </summary>
    public required long TotalTokens { get; init; }

    /// <summary>
    /// Success rate (0.0 to 1.0).
    /// </summary>
    public required double SuccessRate { get; init; }

    /// <summary>
    /// Request counts grouped by endpoint.
    /// </summary>
    public required Dictionary<string, int> EndpointCounts { get; init; }
}
