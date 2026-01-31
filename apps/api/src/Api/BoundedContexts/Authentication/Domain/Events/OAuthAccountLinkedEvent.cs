using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when an OAuth account is linked to a user.
/// </summary>
internal sealed class OAuthAccountLinkedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user who linked the OAuth account.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the OAuth provider name (e.g., "google", "discord", "github").
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// Gets the provider-specific user ID.
    /// </summary>
    public string ProviderUserId { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthAccountLinkedEvent"/> class.
    /// </summary>
    public OAuthAccountLinkedEvent(Guid userId, string provider, string providerUserId)
    {
        UserId = userId;
        Provider = provider;
        ProviderUserId = providerUserId;
    }
}
