using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when OAuth tokens are refreshed for an account.
/// </summary>
public sealed class OAuthTokensRefreshedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the OAuth account whose tokens were refreshed.
    /// </summary>
    public Guid OAuthAccountId { get; }

    /// <summary>
    /// Gets the OAuth provider name.
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// Gets the new token expiration time.
    /// </summary>
    public DateTime ExpiresAt { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthTokensRefreshedEvent"/> class.
    /// </summary>
    public OAuthTokensRefreshedEvent(Guid oauthAccountId, string provider, DateTime expiresAt)
    {
        OAuthAccountId = oauthAccountId;
        Provider = provider;
        ExpiresAt = expiresAt;
    }
}
