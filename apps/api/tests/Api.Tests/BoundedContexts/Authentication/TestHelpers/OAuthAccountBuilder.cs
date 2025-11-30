using Api.BoundedContexts.Authentication.Domain.Entities;

namespace Api.Tests.BoundedContexts.Authentication.TestHelpers;

/// <summary>
/// Fluent builder for creating test OAuthAccount entities.
/// Provides convenient methods for constructing OAuth accounts with various configurations.
/// </summary>
public class OAuthAccountBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _userId = Guid.NewGuid();
    private string _provider = "google";
    private string _providerUserId = "oauth_provider_user_123";
    private string _accessToken = "encrypted_access_token_test";
    private string? _refreshToken;
    private DateTime? _tokenExpiresAt;

    /// <summary>
    /// Sets the OAuth account ID.
    /// </summary>
    public OAuthAccountBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the user ID for this OAuth account.
    /// </summary>
    public OAuthAccountBuilder ForUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Sets the user ID from a User entity.
    /// </summary>
    public OAuthAccountBuilder ForUser(User user)
    {
        _userId = user.Id;
        return this;
    }

    /// <summary>
    /// Sets the OAuth provider (google, discord, github).
    /// </summary>
    public OAuthAccountBuilder WithProvider(string provider)
    {
        _provider = provider;
        return this;
    }

    /// <summary>
    /// Sets this as a Google OAuth account.
    /// </summary>
    public OAuthAccountBuilder AsGoogle()
    {
        _provider = "google";
        _refreshToken = "encrypted_google_refresh_token";
        return this;
    }

    /// <summary>
    /// Sets this as a Discord OAuth account.
    /// </summary>
    public OAuthAccountBuilder AsDiscord()
    {
        _provider = "discord";
        _refreshToken = "encrypted_discord_refresh_token";
        return this;
    }

    /// <summary>
    /// Sets this as a GitHub OAuth account (no refresh token support).
    /// </summary>
    public OAuthAccountBuilder AsGitHub()
    {
        _provider = "github";
        _refreshToken = null;
        return this;
    }

    /// <summary>
    /// Sets the provider user ID.
    /// </summary>
    public OAuthAccountBuilder WithProviderUserId(string providerUserId)
    {
        _providerUserId = providerUserId;
        return this;
    }

    /// <summary>
    /// Sets the encrypted access token.
    /// </summary>
    public OAuthAccountBuilder WithAccessToken(string encryptedAccessToken)
    {
        _accessToken = encryptedAccessToken;
        return this;
    }

    /// <summary>
    /// Sets the encrypted refresh token.
    /// </summary>
    public OAuthAccountBuilder WithRefreshToken(string encryptedRefreshToken)
    {
        _refreshToken = encryptedRefreshToken;
        return this;
    }

    /// <summary>
    /// Sets the token expiration date.
    /// </summary>
    public OAuthAccountBuilder WithTokenExpiry(DateTime expiresAt)
    {
        _tokenExpiresAt = expiresAt;
        return this;
    }

    /// <summary>
    /// Sets the token to expire after a specified number of hours.
    /// </summary>
    public OAuthAccountBuilder ExpiresInHours(int hours)
    {
        _tokenExpiresAt = DateTime.UtcNow.AddHours(hours);
        return this;
    }

    /// <summary>
    /// Sets the token to be expired (past expiration date).
    /// </summary>
    public OAuthAccountBuilder Expired()
    {
        _tokenExpiresAt = DateTime.UtcNow.AddHours(-1);
        return this;
    }

    /// <summary>
    /// Builds the OAuthAccount entity.
    /// </summary>
    public OAuthAccount Build()
    {
        return new OAuthAccount(
            _id,
            _userId,
            _provider,
            _providerUserId,
            _accessToken,
            _refreshToken,
            _tokenExpiresAt
        );
    }

    /// <summary>
    /// Creates a default test OAuth account with standard configuration.
    /// </summary>
    public static OAuthAccount CreateDefault() => new OAuthAccountBuilder().Build();

    /// <summary>
    /// Creates a Google OAuth account for a specific user.
    /// </summary>
    public static OAuthAccount CreateGoogleForUser(User user)
        => new OAuthAccountBuilder().ForUser(user).AsGoogle().Build();

    /// <summary>
    /// Creates a Discord OAuth account for a specific user.
    /// </summary>
    public static OAuthAccount CreateDiscordForUser(User user)
        => new OAuthAccountBuilder().ForUser(user).AsDiscord().Build();

    /// <summary>
    /// Creates a GitHub OAuth account for a specific user.
    /// </summary>
    public static OAuthAccount CreateGitHubForUser(User user)
        => new OAuthAccountBuilder().ForUser(user).AsGitHub().Build();
}

