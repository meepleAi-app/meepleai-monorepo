using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Represents a single usage event of an API key.
/// Provides detailed audit trail and analytics for API key usage.
/// Entity for logging API key access patterns.
/// </summary>
public sealed class ApiKeyUsageLog : Entity<Guid>
{
    /// <summary>
    /// The API key that was used.
    /// </summary>
    public Guid KeyId { get; private set; }

    /// <summary>
    /// When the API key was used (UTC).
    /// </summary>
    public DateTime UsedAt { get; private set; }

    /// <summary>
    /// HTTP endpoint that was accessed (e.g., "/api/v1/games").
    /// </summary>
    public string Endpoint { get; private set; }

    /// <summary>
    /// IP address of the client.
    /// </summary>
    public string? IpAddress { get; private set; }

    /// <summary>
    /// User agent string from the request.
    /// </summary>
    public string? UserAgent { get; private set; }

    /// <summary>
    /// HTTP method used (GET, POST, etc.).
    /// </summary>
    public string? HttpMethod { get; private set; }

    /// <summary>
    /// Response status code (200, 400, etc.).
    /// </summary>
    public int? StatusCode { get; private set; }

    /// <summary>
    /// Response time in milliseconds.
    /// </summary>
    public long? ResponseTimeMs { get; private set; }

    // Navigation property for EF Core
    public ApiKey? ApiKey { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private ApiKeyUsageLog() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new API key usage log entry.
    /// </summary>
    public static ApiKeyUsageLog Create(
        Guid id,
        Guid keyId,
        string endpoint,
        string? ipAddress = null,
        string? userAgent = null,
        string? httpMethod = null,
        int? statusCode = null,
        long? responseTimeMs = null,
        DateTime? usedAt = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));

        return new ApiKeyUsageLog
        {
            Id = id,
            KeyId = keyId,
            Endpoint = endpoint,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            HttpMethod = httpMethod,
            StatusCode = statusCode,
            ResponseTimeMs = responseTimeMs,
            UsedAt = usedAt ?? DateTime.UtcNow
        };
    }
}
