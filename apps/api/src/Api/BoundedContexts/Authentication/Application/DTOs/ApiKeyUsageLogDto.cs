namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// DTO for a single API key usage log entry.
/// </summary>
public class ApiKeyUsageLogDto
{
    /// <summary>
    /// Unique identifier for this usage log.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// API key ID that was used.
    /// </summary>
    public Guid KeyId { get; set; }

    /// <summary>
    /// When the API key was used (UTC).
    /// </summary>
    public DateTime UsedAt { get; set; }

    /// <summary>
    /// HTTP endpoint accessed.
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// IP address of the client.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// HTTP method used.
    /// </summary>
    public string? HttpMethod { get; set; }

    /// <summary>
    /// HTTP status code returned.
    /// </summary>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public long? ResponseTimeMs { get; set; }
}
