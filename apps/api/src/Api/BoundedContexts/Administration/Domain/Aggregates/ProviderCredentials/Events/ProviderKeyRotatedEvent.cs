using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials.Events;

/// <summary>
/// Raised by <see cref="ProviderCredential.Create"/> when a provider's API key has been rotated.
/// Consumed by <c>ProviderKeyRotatedEventHandler</c> which publishes a Redis pub/sub message
/// so all pods invalidate their <c>IProviderCredentialResolver</c> cache.
/// Issue #1859.
/// </summary>
public sealed record ProviderKeyRotatedEvent(
    Guid CredentialId,
    string ProviderName,
    string NewFingerprint,
    string? PreviousFingerprint,
    Guid RotatedByUserId,
    DateTime RotatedAt) : INotification;
