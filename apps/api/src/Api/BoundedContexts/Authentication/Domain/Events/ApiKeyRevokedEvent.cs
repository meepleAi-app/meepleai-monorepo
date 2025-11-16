using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when an API key is revoked.
/// </summary>
public sealed class ApiKeyRevokedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the revoked API key.
    /// </summary>
    public Guid ApiKeyId { get; }

    /// <summary>
    /// Gets the ID of the user who owned the API key.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the reason for revocation.
    /// </summary>
    public string? Reason { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiKeyRevokedEvent"/> class.
    /// </summary>
    public ApiKeyRevokedEvent(Guid apiKeyId, Guid userId, string? reason = null)
    {
        ApiKeyId = apiKeyId;
        UserId = userId;
        Reason = reason;
    }
}
