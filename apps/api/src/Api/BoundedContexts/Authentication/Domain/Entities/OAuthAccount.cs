using Api.BoundedContexts.Authentication.Domain.Events;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Represents a linked OAuth provider account.
/// Supports Google, Discord, and GitHub OAuth 2.0.
/// Aggregate root for OAuth account lifecycle management.
/// </summary>
public sealed class OAuthAccount : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string Provider { get; private set; }
    public string ProviderUserId { get; private set; }
    public string AccessTokenEncrypted { get; private set; }
    public string? RefreshTokenEncrypted { get; private set; }
    public DateTime? TokenExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Navigation property for EF Core
    public User? User { get; private set; }

    public static readonly HashSet<string> SupportedProviders = new(StringComparer.OrdinalIgnoreCase)
    {
        "google", "discord", "github"
    };
    private const int DefaultOAuthTokenExpirationHours = 1; // Default OAuth token TTL when provider doesn't specify

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private OAuthAccount() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new OAuth account link.
    /// </summary>
    public OAuthAccount(
        Guid id,
        Guid userId,
        string provider,
        string providerUserId,
        string accessTokenEncrypted,
        string? refreshTokenEncrypted = null,
        DateTime? tokenExpiresAt = null) : base(id)
    {
        if (!SupportedProviders.Contains(provider))
            throw new ValidationException(nameof(provider), $"Unsupported OAuth provider: {provider}. Supported: {string.Join(", ", SupportedProviders)}");

        if (string.IsNullOrWhiteSpace(providerUserId))
            throw new ValidationException(nameof(providerUserId), "Provider user ID cannot be empty");

        if (string.IsNullOrWhiteSpace(accessTokenEncrypted))
            throw new ValidationException(nameof(accessTokenEncrypted), "Access token cannot be empty");

        UserId = userId;
        Provider = provider.ToLowerInvariant();
        ProviderUserId = providerUserId;
        AccessTokenEncrypted = accessTokenEncrypted;
        RefreshTokenEncrypted = refreshTokenEncrypted;
        TokenExpiresAt = tokenExpiresAt;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OAuthAccountLinkedEvent(userId, Provider, providerUserId));
    }

    /// <summary>
    /// Updates the OAuth tokens (e.g., after refresh).
    /// </summary>
    public void UpdateTokens(
        string newAccessTokenEncrypted,
        string? newRefreshTokenEncrypted = null,
        DateTime? newTokenExpiresAt = null)
    {
        if (string.IsNullOrWhiteSpace(newAccessTokenEncrypted))
            throw new ValidationException(nameof(newAccessTokenEncrypted), "Access token cannot be empty");

        AccessTokenEncrypted = newAccessTokenEncrypted;
        RefreshTokenEncrypted = newRefreshTokenEncrypted;
        TokenExpiresAt = newTokenExpiresAt;
        UpdatedAt = DateTime.UtcNow;

        AddDomainEvent(new OAuthTokensRefreshedEvent(Id, Provider, newTokenExpiresAt ?? DateTime.UtcNow.AddHours(DefaultOAuthTokenExpirationHours)));
    }

    /// <summary>
    /// Checks if the access token has expired.
    /// </summary>
    public bool IsTokenExpired()
    {
        return TokenExpiresAt.HasValue && DateTime.UtcNow >= TokenExpiresAt.Value;
    }

    /// <summary>
    /// Checks if this account supports token refresh.
    /// </summary>
    public bool SupportsRefresh()
    {
        // Discord, Google, and GitHub support refresh tokens
        return !string.IsNullOrWhiteSpace(RefreshTokenEncrypted) &&
               (Provider == "google" || Provider == "discord" || Provider == "github");
    }
}
