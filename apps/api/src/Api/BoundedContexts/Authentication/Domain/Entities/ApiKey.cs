using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;
using System.Security.Cryptography;

namespace Api.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Represents an API key for programmatic access.
/// API keys provide scoped access and can be revoked.
/// Aggregate root for API key lifecycle management.
/// </summary>
public sealed class ApiKey : AggregateRoot<Guid>
{
    public Guid UserId { get; private set; }
    public string KeyName { get; private set; }
    public string KeyHash { get; private set; }
    public string KeyPrefix { get; private set; }
    public string Scopes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    public bool IsActive { get; private set; }
    public string? Metadata { get; private set; }

    // Navigation property for EF Core
    public User? User { get; private set; }

    private const int KeySizeBytes = 32; // 256 bits
    private const int PrefixLength = 8;

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private ApiKey() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates a new API key.
    /// </summary>
    public static (ApiKey apiKey, string plaintextKey) Create(
        Guid id,
        Guid userId,
        string keyName,
        string scopes,
        DateTime? expiresAt = null,
        string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(keyName))
            throw new ValidationException(nameof(keyName), "API key name cannot be empty");

        if (string.IsNullOrWhiteSpace(scopes))
            throw new ValidationException(nameof(scopes), "API key scopes cannot be empty");

        // Generate cryptographically secure key
        var keyBytes = RandomNumberGenerator.GetBytes(KeySizeBytes);
        var plaintextKey = Convert.ToBase64String(keyBytes);

        // Extract prefix for identification
        var keyPrefix = plaintextKey[..PrefixLength];

        // Hash the key for storage (SHA256)
        var keyHash = Convert.ToBase64String(SHA256.HashData(keyBytes));

        var apiKey = new ApiKey
        {
            Id = id,
            UserId = userId,
            KeyName = keyName,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Scopes = scopes,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsActive = true,
            Metadata = metadata
        };

        return (apiKey, plaintextKey);
    }

    /// <summary>
    /// Verifies if the provided plaintext key matches this API key.
    /// </summary>
    public bool VerifyKey(string plaintextKey)
    {
        if (string.IsNullOrWhiteSpace(plaintextKey))
            return false;

        if (!IsActive || RevokedAt != null)
            return false;

        if (ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value)
            return false;

        try
        {
            var keyBytes = Convert.FromBase64String(plaintextKey);
            var computedHash = Convert.ToBase64String(SHA256.HashData(keyBytes));
            return computedHash == KeyHash;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Checks if this API key has a specific scope.
    /// </summary>
    public bool HasScope(string scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
            return false;

        var scopeList = Scopes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return scopeList.Contains(scope, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Updates the last used timestamp.
    /// </summary>
    public void MarkAsUsed()
    {
        LastUsedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Revokes this API key.
    /// </summary>
    public void Revoke(Guid revokedByUserId, string? reason = null)
    {
        if (RevokedAt != null)
            throw new DomainException("API key is already revoked");

        RevokedAt = DateTime.UtcNow;
        RevokedBy = revokedByUserId;
        IsActive = false;

        AddDomainEvent(new ApiKeyRevokedEvent(Id, UserId, reason));
    }

    /// <summary>
    /// Checks if this API key is expired.
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;
    }

    /// <summary>
    /// Checks if this API key is currently valid.
    /// </summary>
    public bool IsValidKey()
    {
        return IsActive && RevokedAt == null && !IsExpired();
    }
}
