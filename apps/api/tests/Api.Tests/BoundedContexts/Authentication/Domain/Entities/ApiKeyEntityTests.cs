using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Comprehensive domain tests for ApiKey entity.
/// Tests key generation, verification, scopes, expiration, revocation, and security.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ApiKeyEntityTests
{
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
        apiKey.Id.Should().Be(id);
        apiKey.UserId.Should().Be(userId);
        apiKey.KeyName.Should().Be(keyName);
        apiKey.Scopes.Should().Be(scopes);
        apiKey.KeyHash.Should().NotBeNull();
        apiKey.KeyPrefix.Should().NotBeNull();
        plaintextKey.Should().NotBeNull();
        apiKey.IsActive.Should().BeTrue();
        apiKey.RevokedAt.Should().BeNull();
        apiKey.ExpiresAt.Should().BeNull();
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
        plaintextKey.Should().NotBeNull();
        (plaintextKey.Length > 40).Should().BeTrue(); // Base64 of 32 bytes is ~44 chars
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
        apiKey.KeyPrefix.Length.Should().Be(8);
        apiKey.KeyPrefix.Should().Be(plaintextKey[..8]);
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
        apiKey.KeyHash.Should().NotBe(plaintextKey);
        apiKey.KeyHash.Should().NotBeNull();
        (apiKey.KeyHash.Length > 0).Should().BeTrue();
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
        apiKey.ExpiresAt.Should().Be(expiresAt);
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
        apiKey.Metadata.Should().Be(metadata);
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "", "read");
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("name");
    }

    [Fact]
    public void Create_WithEmptyScopes_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "Test", "");
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().ContainEquivalentOf("scopes");
    }

    [Fact]
    public void Create_GeneratesUniqueKeys_ForMultipleCalls()
    {
        // Act
        var (_, key1) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "Key1", "read");
        var (_, key2) = ApiKey.Create(Guid.NewGuid(), Guid.NewGuid(), "Key2", "read");

        // Assert
        key2.Should().NotBe(key1);
    }
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
        isValid.Should().BeTrue();
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
        isValid.Should().BeFalse();
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
        isValid.Should().BeFalse();
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
        isValid.Should().BeFalse();
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
        isValid.Should().BeFalse();
        apiKey.IsActive.Should().BeFalse();
    }

    [Fact]
    public void VerifyKey_WithNullKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isValid = apiKey.VerifyKey(null!);

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyKey_WithEmptyKey_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isValid = apiKey.VerifyKey("");

        // Assert
        isValid.Should().BeFalse();
    }

    [Fact]
    public void VerifyKey_WithInvalidBase64_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isValid = apiKey.VerifyKey("not-valid-base64!");

        // Assert
        isValid.Should().BeFalse();
    }
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
        hasScope.Should().BeTrue();
    }

    [Fact]
    public void HasScope_WithMultipleScopes_FindsCorrectScope()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("read", "write", "delete")
            .Build();

        // Act & Assert
        apiKey.HasScope("read").Should().BeTrue();
        apiKey.HasScope("write").Should().BeTrue();
        apiKey.HasScope("delete").Should().BeTrue();
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
        hasScope.Should().BeFalse();
    }

    [Fact]
    public void HasScope_IsCaseInsensitive()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("READ", "WRITE")
            .Build();

        // Act & Assert
        apiKey.HasScope("read").Should().BeTrue();
        apiKey.HasScope("Write").Should().BeTrue();
        apiKey.HasScope("READ").Should().BeTrue();
    }

    [Fact]
    public void HasScope_WithNullScope_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var hasScope = apiKey.HasScope(null!);

        // Assert
        hasScope.Should().BeFalse();
    }

    [Fact]
    public void HasScope_WithEmptyScope_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var hasScope = apiKey.HasScope("");

        // Assert
        hasScope.Should().BeFalse();
    }

    [Fact]
    public void HasScope_WithWhitespaceInScopes_HandlesCorrectly()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("read, write, delete")
            .Build();

        // Act & Assert
        apiKey.HasScope("read").Should().BeTrue();
        apiKey.HasScope("write").Should().BeTrue();
        apiKey.HasScope("delete").Should().BeTrue();
    }
    [Fact]
    public void MarkAsUsed_UpdatesLastUsedTimestamp()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();
        var beforeUpdate = DateTime.UtcNow;

        // Act
        apiKey.MarkAsUsed();

        // Assert
        apiKey.LastUsedAt.Should().NotBeNull();
        (apiKey.LastUsedAt >= beforeUpdate).Should().BeTrue();
        (apiKey.LastUsedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsUsed_CalledMultipleTimes_UpdatesTimestamp()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();
        apiKey.MarkAsUsed();
        var firstUpdate = apiKey.LastUsedAt;

        await Task.Delay(TestConstants.Timing.TinyDelay); // Ensure different timestamp

        // Act
        apiKey.MarkAsUsed();

        // Assert
        apiKey.LastUsedAt.Should().NotBeNull();
        (apiKey.LastUsedAt > firstUpdate).Should().BeTrue();
    }
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
        apiKey.RevokedAt.Should().NotBeNull();
        (apiKey.RevokedAt >= beforeRevoke).Should().BeTrue();
        apiKey.RevokedBy.Should().Be(revokedBy);
        apiKey.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Revoke_AlreadyRevoked_ThrowsDomainException()
    {
        // Arrange
        var (apiKey, _) = new ApiKeyBuilder()
            .Revoked()
            .Build();

        // Act & Assert
        var act = () =>
            apiKey.Revoke(Guid.NewGuid());
        var exception = act.Should().Throw<DomainException>().Which;

        exception.Message.Should().ContainEquivalentOf("already revoked");
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
        apiKey.RevokedAt.Should().NotBeNull();
        apiKey.RevokedBy.Should().Be(revokedBy);
        apiKey.IsActive.Should().BeFalse();
    }
    [Fact]
    public void IsExpired_WithNoExpiration_ReturnsFalse()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isExpired = apiKey.IsExpired();

        // Assert
        isExpired.Should().BeFalse();
        apiKey.ExpiresAt.Should().BeNull();
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
        isExpired.Should().BeFalse();
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
        isExpired.Should().BeTrue();
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
        isExpired.Should().BeTrue();
    }
    [Fact]
    public void IsValidKey_WithValidKey_ReturnsTrue()
    {
        // Arrange
        var (apiKey, _) = ApiKeyBuilder.CreateDefault();

        // Act
        var isValid = apiKey.IsValidKey();

        // Assert
        isValid.Should().BeTrue();
        apiKey.IsActive.Should().BeTrue();
        apiKey.RevokedAt.Should().BeNull();
        apiKey.IsExpired().Should().BeFalse();
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
        isValid.Should().BeFalse();
        apiKey.IsActive.Should().BeFalse();
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
        isValid.Should().BeFalse();
        apiKey.RevokedAt.Should().NotBeNull();
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
        isValid.Should().BeFalse();
        apiKey.IsExpired().Should().BeTrue();
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
        validKey.IsValidKey().Should().BeTrue();

        // Revoke it - should fail
        validKey.Revoke(Guid.NewGuid());
        validKey.IsValidKey().Should().BeFalse();
    }
    [Fact]
    public void Builder_CreateDefault_ProducesValidApiKey()
    {
        // Act
        var (apiKey, plaintextKey) = ApiKeyBuilder.CreateDefault();

        // Assert
        apiKey.Id.Should().NotBe(Guid.Empty);
        plaintextKey.Should().NotBeNull();
        apiKey.IsActive.Should().BeTrue();
        apiKey.IsValidKey().Should().BeTrue();
    }

    [Fact]
    public void Builder_CreateExpired_ProducesExpiredApiKey()
    {
        // Act
        var (apiKey, _) = ApiKeyBuilder.CreateExpired();

        // Assert
        apiKey.IsExpired().Should().BeTrue();
        apiKey.IsValidKey().Should().BeFalse();
    }

    [Fact]
    public void Builder_WithScopes_ConfiguresMultipleScopes()
    {
        // Act
        var (apiKey, _) = new ApiKeyBuilder()
            .WithScopes("read", "write", "admin")
            .Build();

        // Assert
        apiKey.HasScope("read").Should().BeTrue();
        apiKey.HasScope("write").Should().BeTrue();
        apiKey.HasScope("admin").Should().BeTrue();
    }
}
