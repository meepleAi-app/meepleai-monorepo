using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
        token.Should().NotBeNull();
        token.Value.Should().NotBeEmpty();
        token.ShareLinkId.Should().Be(shareLinkId);
        token.ThreadId.Should().Be(threadId);
        token.CreatorId.Should().Be(creatorId);
        token.Role.Should().Be(ShareLinkRole.View);
        token.ExpiresAt.Should().Be(expiresAt);
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
        token.Role.Should().Be(ShareLinkRole.Comment);
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
        parts.Length.Should().Be(3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Generate_WithInvalidSecretKey_ThrowsArgumentException(string? secretKey)
    {
        // Arrange & Act & Assert
        var act = () =>
            ShareLinkToken.Generate(
                shareLinkId: Guid.NewGuid(),
                threadId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                creatorId: Guid.NewGuid(),
                expiresAt: DateTime.UtcNow.AddDays(7),
                secretKey: secretKey!
            );
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.Message.Should().Contain("Secret key");
    }

    [Fact]
    public void Generate_WithPastExpiration_ThrowsArgumentException()
    {
        // Arrange & Act & Assert
        var act = () =>
            ShareLinkToken.Generate(
                shareLinkId: Guid.NewGuid(),
                threadId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                creatorId: Guid.NewGuid(),
                expiresAt: DateTime.UtcNow.AddSeconds(-1),
                secretKey: TestSecretKey
            );
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.Message.Should().Contain("future");
    }

    [Fact]
    public void Generate_WithCurrentTime_ThrowsArgumentException()
    {
        // Arrange - expiration exactly at current time
        var exactNow = DateTime.UtcNow;

        // Act & Assert - should fail since it's not in the future
        var act = () =>
            ShareLinkToken.Generate(
                shareLinkId: Guid.NewGuid(),
                threadId: Guid.NewGuid(),
                role: ShareLinkRole.View,
                creatorId: Guid.NewGuid(),
                expiresAt: exactNow,
                secretKey: TestSecretKey
            );
        var exception = act.Should().Throw<ArgumentException>().Which;

        exception.Message.Should().Contain("future");
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
        token2.Value.Should().NotBe(token1.Value);
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
        validatedToken.Should().NotBeNull();
        validatedToken.ShareLinkId.Should().Be(shareLinkId);
        validatedToken.ThreadId.Should().Be(threadId);
        validatedToken.CreatorId.Should().Be(creatorId);
        validatedToken.Role.Should().Be(ShareLinkRole.View);
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
        validatedToken.Should().NotBeNull();
        validatedToken.Role.Should().Be(ShareLinkRole.Comment);
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
        validatedToken.Should().BeNull();
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
        validatedToken.Should().BeNull();
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
        validatedToken.Should().BeNull();
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
        validatedToken.Should().BeNull();
    }

    [Fact]
    public void Validate_WithMalformedToken_ReturnsNull()
    {
        // Act
        var validatedToken = ShareLinkToken.Validate("not-a-valid-jwt-token", TestSecretKey);

        // Assert
        validatedToken.Should().BeNull();
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
        validatedToken.Should().BeNull();
    }

    [Fact]
    public void Validate_WithRandomBase64_ReturnsNull()
    {
        // Arrange - create something that looks like JWT but isn't valid
        var fakeJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0NTY3ODkwIiwibmFtZSI6IkpvaG4gRG9lIiwiaWF0IjoxNTE2MjM5MDIyfQ.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c";

        // Act
        var validatedToken = ShareLinkToken.Validate(fakeJwt, TestSecretKey);

        // Assert - wrong issuer/audience/claims should fail
        validatedToken.Should().BeNull();
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
        validatedToken.Should().NotBeNull();
        // Allow for some time variance in JWT processing
        validatedToken.ExpiresAt.Should().BeOnOrAfter(expiresAt.AddSeconds(-2)).And.BeOnOrBefore(expiresAt.AddSeconds(2));
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
        token2.Should().NotBeNull();
        token2.Should().Be(token1);
        token1.Equals(token2).Should().BeTrue();
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
        token2.Should().NotBe(token1);
        token1.Equals(token2).Should().BeFalse();
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
        token.Equals(null).Should().BeFalse();
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
        token.Equals("not a token").Should().BeFalse();
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
        token2.Should().NotBeNull();
        token2.GetHashCode().Should().Be(token1.GetHashCode());
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
        token2.GetHashCode().Should().NotBe(token1.GetHashCode());
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
        stringValue.Should().Be(token.Value);
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
        validatedToken.Should().NotBeNull();
        validatedToken.ShareLinkId.Should().Be(shareLinkId);
        validatedToken.ThreadId.Should().Be(threadId);
        validatedToken.CreatorId.Should().Be(creatorId);
        validatedToken.Role.Should().Be(role);
        validatedToken.Value.Should().Be(originalToken.Value);
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
        validatedToken.Should().NotBeNull();
        validatedToken.Role.Should().Be(role);
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
        validatedToken.Should().NotBeNull();
        validatedToken.ExpiresAt.Should().BeOnOrAfter(expiresAt.AddSeconds(-2)).And.BeOnOrBefore(expiresAt.AddSeconds(2));
    }

    #endregion
}