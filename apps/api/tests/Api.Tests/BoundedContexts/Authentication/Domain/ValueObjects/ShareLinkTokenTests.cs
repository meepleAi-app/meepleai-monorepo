using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Comprehensive tests for ShareLinkToken value object.
/// Tests JWT generation, validation, claims extraction, and equality semantics.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ShareLinkTokenTests
{
    private const string TestSecretKey = "test-secret-key-that-is-at-least-32-characters-long-for-hmac-sha256";
    private const string DifferentSecretKey = "different-secret-key-that-is-also-at-least-32-characters-long";

    #region Generate Tests

    [Fact]
    public void Generate_WithValidParameters_ReturnsToken()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        var token = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        // Assert
        Assert.NotNull(token);
        Assert.NotEmpty(token.Value);
        Assert.Equal(shareLinkId, token.ShareLinkId);
        Assert.Equal(threadId, token.ThreadId);
        Assert.Equal(creatorId, token.CreatorId);
        Assert.Equal(ShareLinkRole.View, token.Role);
        Assert.Equal(expiresAt, token.ExpiresAt);
    }

    [Fact]
    public void Generate_WithCommentRole_SetsCorrectRole()
    {
        // Arrange & Act
        var token = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.Comment,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Assert
        Assert.Equal(ShareLinkRole.Comment, token.Role);
    }

    [Fact]
    public void Generate_ProducesJwtFormat()
    {
        // Arrange & Act
        var token = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Assert - JWT has 3 parts separated by dots
        var parts = token.Value.Split('.');
        Assert.Equal(3, parts.Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_WithInvalidSecretKey_ThrowsArgumentException(string? secretKey)
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ShareLinkToken.Generate(
                shareLinkId: Guid.NewGuid(),
                threadId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                creatorId: Guid.NewGuid(),
                expiresAt: DateTime.UtcNow.AddDays(7),
                secretKey: secretKey!
            )
        );

        Assert.Contains("Secret key", exception.Message);
    }

    [Fact]
    public void Generate_WithPastExpiration_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            ShareLinkToken.Generate(
                shareLinkId: Guid.NewGuid(),
                threadId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                creatorId: Guid.NewGuid(),
                expiresAt: DateTime.UtcNow.AddSeconds(-1),
                secretKey: TestSecretKey
            )
        );

        Assert.Contains("future", exception.Message);
    }

    [Fact]
    public void Generate_WithCurrentTime_ThrowsArgumentException()
    {
        // Arrange - expiration exactly at current time
        var exactNow = DateTime.UtcNow;

        // Act & Assert - should fail since it's not in the future
        var exception = Assert.Throws<ArgumentException>(() =>
            ShareLinkToken.Generate(
                shareLinkId: Guid.NewGuid(),
                threadId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                creatorId: Guid.NewGuid(),
                expiresAt: exactNow,
                secretKey: TestSecretKey
            )
        );

        Assert.Contains("future", exception.Message);
    }

    [Fact]
    public void Generate_ProducesUniqueTokens()
    {
        // Arrange - same parameters
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        // Act
        var token1 = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        var token2 = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        // Assert - tokens should be different due to unique JTI
        Assert.NotEqual(token1.Value, token2.Value);
    }

    #endregion

    #region Validate Tests

    [Fact]
    public void Validate_WithValidToken_ReturnsToken()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        var originalToken = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: ShareLinkRole.View,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        // Act
        var validatedToken = ShareLinkToken.Validate(originalToken.Value, TestSecretKey);

        // Assert
        Assert.NotNull(validatedToken);
        Assert.Equal(shareLinkId, validatedToken.ShareLinkId);
        Assert.Equal(threadId, validatedToken.ThreadId);
        Assert.Equal(creatorId, validatedToken.CreatorId);
        Assert.Equal(ShareLinkRole.View, validatedToken.Role);
    }

    [Fact]
    public void Validate_WithCommentRole_ExtractsCorrectRole()
    {
        // Arrange
        var originalToken = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.Comment,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Act
        var validatedToken = ShareLinkToken.Validate(originalToken.Value, TestSecretKey);

        // Assert
        Assert.NotNull(validatedToken);
        Assert.Equal(ShareLinkRole.Comment, validatedToken.Role);
    }

    [Fact]
    public async Task Validate_WithExpiredToken_ReturnsNull()
    {
        // Arrange - Create token with short expiration
        var originalToken = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddMilliseconds(100),
            secretKey: TestSecretKey
        );

        // Wait for expiration
        await Task.Delay(TestConstants.Timing.MediumDelay);

        // Act
        var validatedToken = ShareLinkToken.Validate(originalToken.Value, TestSecretKey);

        // Assert
        Assert.Null(validatedToken);
    }

    [Fact]
    public void Validate_WithWrongSecretKey_ReturnsNull()
    {
        // Arrange
        var originalToken = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Act - validate with different key
        var validatedToken = ShareLinkToken.Validate(originalToken.Value, DifferentSecretKey);

        // Assert
        Assert.Null(validatedToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidTokenValue_ReturnsNull(string? tokenValue)
    {
        // Act
        var validatedToken = ShareLinkToken.Validate(tokenValue!, TestSecretKey);

        // Assert
        Assert.Null(validatedToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithInvalidSecretKey_ReturnsNull(string? secretKey)
    {
        // Arrange
        var originalToken = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Act
        var validatedToken = ShareLinkToken.Validate(originalToken.Value, secretKey!);

        // Assert
        Assert.Null(validatedToken);
    }

    [Fact]
    public void Validate_WithMalformedToken_ReturnsNull()
    {
        // Act
        var validatedToken = ShareLinkToken.Validate("not-a-valid-jwt-token", TestSecretKey);

        // Assert
        Assert.Null(validatedToken);
    }

    [Fact]
    public void Validate_WithTamperedToken_ReturnsNull()
    {
        // Arrange
        var originalToken = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Tamper with the token signature
        var tamperedToken = originalToken.Value.Substring(0, originalToken.Value.Length - 5) + "XXXXX";

        // Act
        var validatedToken = ShareLinkToken.Validate(tamperedToken, TestSecretKey);

        // Assert
        Assert.Null(validatedToken);
    }

    [Fact]
    public void Validate_WithRandomBase64_ReturnsNull()
    {
        // Arrange - create something that looks like JWT but isn't valid
        var fakeJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

        // Act
        var validatedToken = ShareLinkToken.Validate(fakeJwt, TestSecretKey);

        // Assert - wrong issuer/audience/claims should fail
        Assert.Null(validatedToken);
    }

    [Fact]
    public void Validate_PreservesExpirationTime()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var originalToken = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        // Act
        var validatedToken = ShareLinkToken.Validate(originalToken.Value, TestSecretKey);

        // Assert
        Assert.NotNull(validatedToken);
        // Allow for some time variance in JWT processing
        Assert.InRange(validatedToken.ExpiresAt, expiresAt.AddSeconds(-2), expiresAt.AddSeconds(2));
    }

    #endregion

    #region Equality and Hash Code Tests

    [Fact]
    public void Equals_WithSameTokenValue_ReturnsTrue()
    {
        // Arrange
        var token1 = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        var token2 = ShareLinkToken.Validate(token1.Value, TestSecretKey);

        // Assert
        Assert.NotNull(token2);
        Assert.Equal(token1, token2);
        Assert.True(token1.Equals(token2));
    }

    [Fact]
    public void Equals_WithDifferentTokenValue_ReturnsFalse()
    {
        // Arrange
        var token1 = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        var token2 = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Assert
        Assert.NotEqual(token1, token2);
        Assert.False(token1.Equals(token2));
    }

    [Fact]
    public void Equals_WithNull_ReturnsFalse()
    {
        // Arrange
        var token = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Assert
        Assert.False(token.Equals(null));
    }

    [Fact]
    public void Equals_WithDifferentType_ReturnsFalse()
    {
        // Arrange
        var token = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Assert
        Assert.False(token.Equals("not a token"));
    }

    [Fact]
    public void GetHashCode_ForSameTokenValue_ReturnsSameHash()
    {
        // Arrange
        var token1 = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        var token2 = ShareLinkToken.Validate(token1.Value, TestSecretKey);

        // Assert
        Assert.NotNull(token2);
        Assert.Equal(token1.GetHashCode(), token2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_ForDifferentTokenValue_ReturnsDifferentHash()
    {
        // Arrange
        var token1 = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        var token2 = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Assert
        Assert.NotEqual(token1.GetHashCode(), token2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsTokenValue()
    {
        // Arrange
        var token = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        // Act
        var stringValue = token.ToString();

        // Assert
        Assert.Equal(token.Value, stringValue);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void GenerateThenValidate_PreservesAllClaims()
    {
        // Arrange
        var shareLinkId = Guid.NewGuid();
        var threadId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var role = ShareLinkRole.Comment;

        // Act
        var originalToken = ShareLinkToken.Generate(
            shareLinkId: shareLinkId,
            threadId: threadId,
            role: role,
            creatorId: creatorId,
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        var validatedToken = ShareLinkToken.Validate(originalToken.Value, TestSecretKey);

        // Assert
        Assert.NotNull(validatedToken);
        Assert.Equal(shareLinkId, validatedToken.ShareLinkId);
        Assert.Equal(threadId, validatedToken.ThreadId);
        Assert.Equal(creatorId, validatedToken.CreatorId);
        Assert.Equal(role, validatedToken.Role);
        Assert.Equal(originalToken.Value, validatedToken.Value);
    }

    [Theory]
    [InlineData(ShareLinkRole.View)]
    [InlineData(ShareLinkRole.Comment)]
    public void GenerateThenValidate_WorksForAllRoles(ShareLinkRole role)
    {
        // Arrange & Act
        var originalToken = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: role,
            creatorId: Guid.NewGuid(),
            expiresAt: DateTime.UtcNow.AddDays(7),
            secretKey: TestSecretKey
        );

        var validatedToken = ShareLinkToken.Validate(originalToken.Value, TestSecretKey);

        // Assert
        Assert.NotNull(validatedToken);
        Assert.Equal(role, validatedToken.Role);
    }

    [Theory]
    [InlineData(1)]      // 1 hour
    [InlineData(24)]     // 1 day
    [InlineData(168)]    // 7 days
    [InlineData(720)]    // 30 days
    public void GenerateThenValidate_WorksForVariousExpirations(int hours)
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddHours(hours);

        // Act
        var originalToken = ShareLinkToken.Generate(
            shareLinkId: Guid.NewGuid(),
            threadId: Guid.NewGuid(),
            role: ShareLinkRole.View,
            creatorId: Guid.NewGuid(),
            expiresAt: expiresAt,
            secretKey: TestSecretKey
        );

        var validatedToken = ShareLinkToken.Validate(originalToken.Value, TestSecretKey);

        // Assert
        Assert.NotNull(validatedToken);
        Assert.InRange(validatedToken.ExpiresAt, expiresAt.AddSeconds(-2), expiresAt.AddSeconds(2));
    }

    #endregion
}
