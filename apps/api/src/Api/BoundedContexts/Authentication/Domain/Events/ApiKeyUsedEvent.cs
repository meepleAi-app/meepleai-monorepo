using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when an API key is successfully used for authentication.
/// This event triggers usage logging and analytics.
/// </summary>
public sealed class ApiKeyUsedEvent : DomainEventBase
{
    /// <summary>
    /// Unique identifier of the API key that was used.
    /// </summary>
    public Guid KeyId { get; }

    /// <summary>
    /// User ID associated with the API key.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// HTTP endpoint that was accessed (e.g., "/api/v1/games").
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// IP address of the client making the request.
    /// </summary>
    public string? IpAddress { get; }

    /// <summary>
    /// User agent string from the request headers.
    /// </summary>
    public string? UserAgent { get; }

    /// <summary>
    /// Timestamp when the API key was used (UTC).
    /// </summary>
    public DateTime UsedAt { get; }

    public ApiKeyUsedEvent(
        Guid keyId,
        Guid userId,
        string endpoint,
        string? ipAddress = null,
        string? userAgent = null,
        DateTime? usedAt = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("Endpoint cannot be empty", nameof(endpoint));

        KeyId = keyId;
        UserId = userId;
        Endpoint = endpoint;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        UsedAt = usedAt ?? DateTime.UtcNow;
    }
}
