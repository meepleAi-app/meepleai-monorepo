namespace Api.Infrastructure.Entities;

/// <summary>
/// Represents a single usage event of an API key.
/// Provides detailed audit trail and analytics for API key usage patterns.
/// </summary>
public class ApiKeyUsageLogEntity
{
    /// <summary>
    /// Unique identifier for this usage log entry.
    /// </summary>
    required public Guid Id { get; set; }

    /// <summary>
    /// ID of the API key that was used.
    /// </summary>
    required public Guid KeyId { get; set; }

    /// <summary>
    /// Timestamp when the API key was used (UTC).
    /// </summary>
    public DateTime UsedAt { get; set; }

    /// <summary>
    /// HTTP endpoint that was accessed (e.g., "/api/v1/games").
    /// </summary>
    required public string Endpoint { get; set; }

    /// <summary>
    /// IP address of the client making the request.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string from the request headers.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// HTTP method used (GET, POST, PUT, DELETE, etc.).
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// HTTP response status code (200, 400, 500, etc.).
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Response time in milliseconds.
    /// Useful for performance monitoring and SLA tracking.
    /// </summary>
    public long? ResponseTimeMs { get; set; }

    // Navigation properties
    /// <summary>
    /// The API key that was used.
    /// </summary>
    required public ApiKeyEntity ApiKey { get; set; }
}
