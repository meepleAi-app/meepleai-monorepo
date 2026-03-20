using Api.BoundedContexts.Authentication.Domain.Entities;

namespace Api.Tests.BoundedContexts.Authentication.TestHelpers;

/// <summary>
/// Fluent builder for creating test ApiKey entities.
/// Provides convenient methods for constructing API keys with various configurations.
/// </summary>
internal class ApiKeyBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _userId = Guid.NewGuid();
    private string _keyName = "Test API Key";
    private string _scopes = "read,write";
    private DateTime? _expiresAt;
    private string? _metadata;
    private bool _isRevoked;

    /// <summary>
    /// Sets the API key ID.
    /// </summary>
    public ApiKeyBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    /// Sets the user ID for this API key.
    /// </summary>
    public ApiKeyBuilder ForUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    /// <summary>
    /// Sets the user ID from a User entity.
    /// </summary>
    public ApiKeyBuilder ForUser(User user)
    {
        _userId = user.Id;
        return this;
    }

    /// <summary>
    /// Sets the API key name.
    /// </summary>
    public ApiKeyBuilder WithName(string keyName)
    {
        _keyName = keyName;
        return this;
    }

    /// <summary>
    /// Sets the scopes for this API key.
    /// </summary>
    public ApiKeyBuilder WithScopes(params string[] scopes)
    {
        _scopes = string.Join(",", scopes);
        return this;
    }

    /// <summary>
    /// Sets the scopes from a comma-separated string.
    /// </summary>
    public ApiKeyBuilder WithScopes(string scopes)
    {
        _scopes = scopes;
        return this;
    }

    /// <summary>
    /// Sets the expiration date for this API key.
    /// </summary>
    public ApiKeyBuilder WithExpiration(DateTime expiresAt)
    {
        _expiresAt = expiresAt;
        return this;
    }

    /// <summary>
    /// Sets the API key to expire after a specified number of days.
    /// </summary>
    public ApiKeyBuilder ExpiresInDays(int days)
    {
        _expiresAt = DateTime.UtcNow.AddDays(days);
        return this;
    }

    /// <summary>
    /// Sets the API key to be expired (past expiration date).
    /// </summary>
    public ApiKeyBuilder Expired()
    {
        _expiresAt = DateTime.UtcNow.AddDays(-1);
        return this;
    }

    /// <summary>
    /// Sets metadata for this API key.
    /// </summary>
    public ApiKeyBuilder WithMetadata(string metadata)
    {
        _metadata = metadata;
        return this;
    }

    /// <summary>
    /// Marks the API key as revoked.
    /// </summary>
    public ApiKeyBuilder Revoked()
    {
        _isRevoked = true;
        return this;
    }

    /// <summary>
    /// Builds the ApiKey entity and returns both the entity and plaintext key.
    /// </summary>
    public (ApiKey apiKey, string plaintextKey) Build()
    {
        var (apiKey, plaintextKey) = ApiKey.Create(
            _id,
            _userId,
            _keyName,
            _scopes,
            _expiresAt,
            _metadata
        );

        if (_isRevoked)
        {
            apiKey.Revoke(_userId);
        }

        return (apiKey, plaintextKey);
    }

    /// <summary>
    /// Builds only the ApiKey entity (discards plaintext key).
    /// </summary>
    public ApiKey BuildEntity() => Build().apiKey;

    /// <summary>
    /// Creates a default test API key with standard configuration.
    /// </summary>
    public static (ApiKey apiKey, string plaintextKey) CreateDefault() => new ApiKeyBuilder().Build();

    /// <summary>
    /// Creates an API key for a specific user.
    /// </summary>
    public static (ApiKey apiKey, string plaintextKey) CreateForUser(User user)
        => new ApiKeyBuilder().ForUser(user).Build();

    /// <summary>
    /// Creates an expired API key for testing expiration scenarios.
    /// </summary>
    public static (ApiKey apiKey, string plaintextKey) CreateExpired() => new ApiKeyBuilder().Expired().Build();
}