using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Comprehensive domain tests for ApiKey entity.
/// Tests key generation, verification, scopes, expiration, revocation, and security.
/// </summary>
public class ApiKeyEntityTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidData_GeneratesApiKey()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var keyName = "Production API Key";
        var scopes = "read,write";

        // Act
        var (apiKey, plaintextKey) = ApiKey.Create(id, userId, keyName, scopes);

        // Assert
        Assert.Equal(id, apiKey.Id);
        Assert.Equal(userId, apiKey.UserId);
        Assert.Equal(keyName, apiKey.KeyName);
        Assert.Equal(scopes, apiKey.Scopes);
        Assert.NotNull(apiKey.KeyHash);
        Assert.NotNull(apiKey.KeyPrefix);
        Assert.NotNull(plaintextKey);
        Assert.True(apiKey.IsActive);
        Assert.Null(apiKey.RevokedAt);
        Assert.Null(apiKey.ExpiresAt);
    }

    [Fact]
    public void Create_GeneratesSecureKey_WithCorrectLength()
    {
        // Act
        var (_, plaintextKey) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read"
        );

        // Assert
        Assert.NotNull(plaintextKey);
        Assert.True(plaintextKey.Length > 40); // Base64 of 32 bytes is ~44 chars
    }

    [Fact]
    public void Create_ExtractsPrefixCorrectly()
    {
        // Act
        var (apiKey, plaintextKey) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read"
        );

        // Assert
        Assert.Equal(8, apiKey.KeyPrefix.Length);
        Assert.Equal(plaintextKey[..8], apiKey.KeyPrefix);
    }

    [Fact]
    public void Create_HashesKeySecurely()
    {
        // Act
        var (apiKey, plaintextKey) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read"
        );

        // Assert
        Assert.NotEqual(plaintextKey, apiKey.KeyHash);
        Assert.NotNull(apiKey.KeyHash);
        Assert.True(apiKey.KeyHash.Length > 0);
    }

    [Fact]
    public void Create_WithExpiration_SetsExpiryDate()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddDays(90);

        // Act
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read",
            expiresAt
        );

        // Assert
        Assert.Equal(expiresAt, apiKey.ExpiresAt);
    }

    [Fact]
    public void Create_WithMetadata_StoresMetadata()
    {
        // Arrange
        var metadata = "{\"environment\":\"production\"}";

        // Act
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read",
            metadata: metadata
        );

        // Assert
        Assert.Equal(metadata, apiKey.Metadata);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "", "read"));

        Assert.Contains("name", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_WithEmptyScopes_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "Test", ""));

        Assert.Contains("scopes", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Create_GeneratesUniqueKeys_ForMultipleCalls()
    {
        // Act
        var (_, key1) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "Key1", "read");
        var (_, key2) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "Key2", "read");

        // Assert
        Assert.NotEqual(key1, key2);
    }

    #endregion

    #region VerifyKey Tests

    [Fact]
    public void VerifyKey_WithCorrectKey_ReturnsTrue()
    {
        // Arrange
        var (apiKey, plaintextKey) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read"
        );

        // Act
        var isValid = apiKey.VerifyKey(plaintextKey);

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyKey_WithWrongKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKey.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test Key",
            "read"
        );
        var wrongKey = Convert.ToBase64String(new byte[32]);

        // Act
        var isValid = apiKey.VerifyKey(wrongKey);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifyKey_WithRevokedKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, plaintextKey) = new ApiKeyBuilder()
            .Revoked()
            .Build();

        // Act
        var isValid = apiKey.VerifyKey(plaintextKey);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifyKey_WithExpiredKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, plaintextKey) = new ApiKeyBuilder()
            .Expired()
            .Build();

        // Act
        var isValid = apiKey.VerifyKey(plaintextKey);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifyKey_WithInactiveKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, plaintextKey) = new ApiKeyBuilder()
            .Revoked() // Revocation sets IsActive to false
            .Build();

        // Act
        var isValid = apiKey.VerifyKey(plaintextKey);

        // Assert
        Assert.False(isValid);
        Assert.False(apiKey.IsActive);
    }

    [Fact]
    public void VerifyKey_WithNullKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isValid = apiKey.VerifyKey(null!);

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifyKey_WithEmptyKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isValid = apiKey.VerifyKey("");

        // Assert
        Assert.False(isValid);
    }

    [Fact]
    public void VerifyKey_WithInvalidBase64_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isValid = apiKey.VerifyKey("not-valid-base64!");

        // Assert
        Assert.False(isValid);
    }

    #endregion

    #region HasScope Tests

    [Fact]
    public void HasScope_WithSingleScope_ReturnsTrue()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("read")
            .Build();

        // Act
        var hasScope = apiKey.HasScope("read");

        // Assert
        Assert.True(hasScope);
    }

    [Fact]
    public void HasScope_WithMultipleScopes_FindsCorrectScope()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("read", "write", "delete")
            .Build();

        // Act & Assert
        Assert.True(apiKey.HasScope("read"));
        Assert.True(apiKey.HasScope("write"));
        Assert.True(apiKey.HasScope("delete"));
    }

    [Fact]
    public void HasScope_WithNonExistentScope_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("read")
            .Build();

        // Act
        var hasScope = apiKey.HasScope("admin");

        // Assert
        Assert.False(hasScope);
    }

    [Fact]
    public void HasScope_IsCaseInsensitive()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("READ", "WRITE")
            .Build();

        // Act & Assert
        Assert.True(apiKey.HasScope("read"));
        Assert.True(apiKey.HasScope("Write"));
        Assert.True(apiKey.HasScope("READ"));
    }

    [Fact]
    public void HasScope_WithNullScope_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var hasScope = apiKey.HasScope(null!);

        // Assert
        Assert.False(hasScope);
    }

    [Fact]
    public void HasScope_WithEmptyScope_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var hasScope = apiKey.HasScope("");

        // Assert
        Assert.False(hasScope);
    }

    [Fact]
    public void HasScope_WithWhitespaceInScopes_HandlesCorrectly()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("read, write, delete")
            .Build();

        // Act & Assert
        Assert.True(apiKey.HasScope("read"));
        Assert.True(apiKey.HasScope("write"));
        Assert.True(apiKey.HasScope("delete"));
    }

    #endregion

    #region MarkAsUsed Tests

    [Fact]
    public void MarkAsUsed_UpdatesLastUsedTimestamp()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();
        var beforeUpdate = DateTime.UtcNow;

        // Act
        apiKey.MarkAsUsed();

        // Assert
        Assert.NotNull(apiKey.LastUsedAt);
        Assert.True(apiKey.LastUsedAt >= beforeUpdate);
        Assert.True(apiKey.LastUsedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void MarkAsUsed_CalledMultipleTimes_UpdatesTimestamp()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();
        apiKey.MarkAsUsed();
        var firstUpdate = apiKey.LastUsedAt;

        Thread.Sleep(10); // Small delay

        // Act
        apiKey.MarkAsUsed();

        // Assert
        Assert.NotNull(apiKey.LastUsedAt);
        Assert.True(apiKey.LastUsedAt > firstUpdate);
    }

    #endregion

    #region Revoke Tests

    [Fact]
    public void Revoke_WithActiveKey_RevokesSuccessfully()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();
        var revokedBy = Guid.NewGuid();
        var beforeRevoke = DateTime.UtcNow;

        // Act
        apiKey.Revoke(revokedBy);

        // Assert
        Assert.NotNull(apiKey.RevokedAt);
        Assert.True(apiKey.RevokedAt >= beforeRevoke);
        Assert.Equal(revokedBy, apiKey.RevokedBy);
        Assert.False(apiKey.IsActive);
    }

    [Fact]
    public void Revoke_AlreadyRevoked_ThrowsDomainException()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .Revoked()
            .Build();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            apiKey.Revoke(Guid.NewGuid()));

        Assert.Contains("already revoked", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Revoke_SetsAllRevocationFields()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();
        var revokedBy = Guid.NewGuid();

        // Act
        apiKey.Revoke(revokedBy);

        // Assert
        Assert.NotNull(apiKey.RevokedAt);
        Assert.Equal(revokedBy, apiKey.RevokedBy);
        Assert.False(apiKey.IsActive);
    }

    #endregion

    #region IsExpired Tests

    [Fact]
    public void IsExpired_WithNoExpiration_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isExpired = apiKey.IsExpired();

        // Assert
        Assert.False(isExpired);
        Assert.Null(apiKey.ExpiresAt);
    }

    [Fact]
    public void IsExpired_WithFutureExpiration_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .ExpiresInDays(30)
            .Build();

        // Act
        var isExpired = apiKey.IsExpired();

        // Assert
        Assert.False(isExpired);
    }

    [Fact]
    public void IsExpired_WithPastExpiration_ReturnsTrue()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .Expired()
            .Build();

        // Act
        var isExpired = apiKey.IsExpired();

        // Assert
        Assert.True(isExpired);
    }

    [Fact]
    public void IsExpired_ExactlyAtExpiration_ReturnsTrue()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow;
        var (apiKey, _) = new ApiKeyBuilder()
            .WithExpiration(expiresAt)
            .Build();

        // Act
        var isExpired = apiKey.IsExpired();

        // Assert
        Assert.True(isExpired);
    }

    #endregion

    #region IsValidKey Tests

    [Fact]
    public void IsValidKey_WithValidKey_ReturnsTrue()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isValid = apiKey.IsValidKey();

        // Assert
        Assert.True(isValid);
        Assert.True(apiKey.IsActive);
        Assert.Null(apiKey.RevokedAt);
        Assert.False(apiKey.IsExpired());
    }

    [Fact]
    public void IsValidKey_WithInactiveKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .Revoked()
            .Build();

        // Act
        var isValid = apiKey.IsValidKey();

        // Assert
        Assert.False(isValid);
        Assert.False(apiKey.IsActive);
    }

    [Fact]
    public void IsValidKey_WithRevokedKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .Revoked()
            .Build();

        // Act
        var isValid = apiKey.IsValidKey();

        // Assert
        Assert.False(isValid);
        Assert.NotNull(apiKey.RevokedAt);
    }

    [Fact]
    public void IsValidKey_WithExpiredKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .Expired()
            .Build();

        // Act
        var isValid = apiKey.IsValidKey();

        // Assert
        Assert.False(isValid);
        Assert.True(apiKey.IsExpired());
    }

    [Fact]
    public void IsValidKey_CompositeValidation_AllConditionsMustPass()
    {
        // Test that all three conditions are checked

        // Arrange - valid key
        var (validKey, _) = new ApiKeyBuilder()
            .ExpiresInDays(30)
            .Build();

        // Act & Assert - all conditions pass
        Assert.True(validKey.IsValidKey());

        // Revoke it - should fail
        validKey.Revoke(Guid.NewGuid());
        Assert.False(validKey.IsValidKey());
    }

    #endregion

    #region Builder Integration Tests

    [Fact]
    public void Builder_CreateDefault_ProducesValidApiKey()
    {
        // Act
        var (apiKey, plaintextKey) = ApiKeyBuilder.CreateDefault();

        // Assert
        Assert.NotEqual(Guid.Empty, apiKey.Id);
        Assert.NotNull(plaintextKey);
        Assert.True(apiKey.IsActive);
        Assert.True(apiKey.IsValidKey());
    }

    [Fact]
    public void Builder_CreateExpired_ProducesExpiredApiKey()
    {
        // Act
        var (apiKey, _) = ApiKeyBuilder.CreateExpired();

        // Assert
        Assert.True(apiKey.IsExpired());
        Assert.False(apiKey.IsValidKey());
    }

    [Fact]
    public void Builder_WithScopes_ConfiguresMultipleScopes()
    {
        // Act
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("read", "write", "admin")
            .Build();

        // Assert
        Assert.True(apiKey.HasScope("read"));
        Assert.True(apiKey.HasScope("write"));
        Assert.True(apiKey.HasScope("admin"));
    }

    #endregion
}
