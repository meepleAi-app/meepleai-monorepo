using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when an OAuth account is unlinked from a user.
/// </summary>
internal sealed class OAuthAccountUnlinkedEvent : DomainEventBase
{
    /// <summary>
    /// Gets the ID of the user who unlinked the OAuth account.
    /// </summary>
    public Guid UserId { get; }

    /// <summary>
    /// Gets the OAuth provider name that was unlinked.
    /// </summary>
    public string Provider { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="OAuthAccountUnlinkedEvent"/> class.
    /// </summary>
    public OAuthAccountUnlinkedEvent(Guid userId, string provider)
    {
        UserId = userId;
        Provider = provider;
    }
}
