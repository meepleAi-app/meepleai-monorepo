using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Domain event raised when a new user account is provisioned through an OAuth
/// callback (i.e. first-time login via Google/GitHub/Discord, no prior local
/// account). C3+I8: marks the OAuth-only path so listeners can distinguish
/// password-less accounts from those that registered through /auth/register.
/// </summary>
internal sealed class UserCreatedViaOAuthEvent : DomainEventBase
{
    /// <summary>The ID assigned to the newly created user.</summary>
    public Guid UserId { get; }

    /// <summary>Lower-cased email reported by the OAuth provider.</summary>
    public string Email { get; }

    /// <summary>OAuth provider name (e.g. "google", "github", "discord").</summary>
    public string Provider { get; }

    public UserCreatedViaOAuthEvent(Guid userId, string email, string provider)
    {
        UserId = userId;
        Email = email;
        Provider = provider;
    }
}
