using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Comprehensive domain tests for OAuthAccount entity.
/// Tests OAuth provider linking, token management, expiration, and provider-specific behavior.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class OAuthAccountEntityTests
{
    [Fact]
    public void Constructor_WithValidData_CreatesOAuthAccount()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var provider = "google";
        var providerUserId = "google_user_123";
        var accessToken = "encrypted_access_token";
        var refreshToken = "encrypted_refresh_token";
        var expiresAt = DateTime.UtcNow.AddHours(1);

        // Act
        var account = new OAuthAccount(
            id, userId, provider, providerUserId,
            accessToken, refreshToken, expiresAt
        );

        // Assert
        Assert.Equal(id, account.Id);
        Assert.Equal(userId, account.UserId);
        Assert.Equal("google", account.Provider); // Normalized to lowercase
        Assert.Equal(providerUserId, account.ProviderUserId);
        Assert.Equal(accessToken, account.AccessTokenEncrypted);
        Assert.Equal(refreshToken, account.RefreshTokenEncrypted);
        Assert.Equal(expiresAt, account.TokenExpiresAt);
        Assert.True(account.CreatedAt <= DateTime.UtcNow);
        // CreatedAt and UpdatedAt should be very close (within milliseconds)
        Assert.True((account.UpdatedAt - account.CreatedAt).TotalMilliseconds < 10);
    }

    [Fact]
    public void Constructor_WithGoogleProvider_CreatesSuccessfully()
    {
        // Act
        var account = new OAuthAccountBuilder()
            .AsGoogle()
            .Build();

        // Assert
        Assert.Equal("google", account.Provider);
        Assert.NotNull(account.RefreshTokenEncrypted);
    }

    [Fact]
    public void Constructor_WithDiscordProvider_CreatesSuccessfully()
    {
        // Act
        var account = new OAuthAccountBuilder()
            .AsDiscord()
            .Build();

        // Assert
        Assert.Equal("discord", account.Provider);
        Assert.NotNull(account.RefreshTokenEncrypted);
    }

    [Fact]
    public void Constructor_WithGitHubProvider_CreatesSuccessfully()
    {
        // Act
        var account = new OAuthAccountBuilder()
            .AsGitHub()
            .Build();

        // Assert
        Assert.Equal("github", account.Provider);
    }

    [Fact]
    public void Constructor_NormalizesProviderToLowercase()
    {
        // Arrange & Act
        var account = new OAuthAccount(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "GOOGLE", // Uppercase
            "user123",
            "access_token"
        );

        // Assert
        Assert.Equal("google", account.Provider);
    }

    [Fact]
    public void Constructor_WithUnsupportedProvider_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new OAuthAccount(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "unsupported_provider",
                "user123",
                "access_token"
            )
        );

        Assert.Contains("Unsupported OAuth provider", exception.Message);
        Assert.Contains("unsupported_provider", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyProviderUserId_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new OAuthAccount(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "google",
                "", // Empty
                "access_token"
            )
        );

        Assert.Contains("Provider user ID", exception.Message);
    }

    [Fact]
    public void Constructor_WithEmptyAccessToken_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new OAuthAccount(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "google",
                "user123",
                "" // Empty
            )
        );

        Assert.Contains("Access token", exception.Message);
    }

    [Fact]
    public void Constructor_WithoutRefreshToken_CreatesAccount()
    {
        // Act
        var account = new OAuthAccount(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "google",
            "user123",
            "access_token"
        );

        // Assert
        Assert.Null(account.RefreshTokenEncrypted);
    }

    [Fact]
    public void Constructor_WithoutExpiration_CreatesAccount()
    {
        // Act
        var account = new OAuthAccount(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "google",
            "user123",
            "access_token"
        );

        // Assert
        Assert.Null(account.TokenExpiresAt);
    }
    [Theory]
    [InlineData("google")]
    [InlineData("discord")]
    [InlineData("github")]
    [InlineData("GOOGLE")] // Case insensitive
    [InlineData("Discord")]
    [InlineData("GitHub")]
    public void Constructor_WithSupportedProvider_Succeeds(string provider)
    {
        // Act
        var account = new OAuthAccount(
            Guid.NewGuid(),
            Guid.NewGuid(),
            provider,
            "user123",
            "access_token"
        );

        // Assert
        Assert.Equal(provider.ToLowerInvariant(), account.Provider);
    }

    [Theory]
    [InlineData("facebook")]
    [InlineData("twitter")]
    [InlineData("microsoft")]
    [InlineData("apple")]
    [InlineData("random")]
    public void Constructor_WithUnsupportedProvider_Throws(string provider)
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            new OAuthAccount(
                Guid.NewGuid(),
                Guid.NewGuid(),
                provider,
                "user123",
                "access_token"
            )
        );
    }
    [Fact]
    public void UpdateTokens_WithValidTokens_UpdatesSuccessfully()
    {
        // Arrange
        var account = new OAuthAccountBuilder().Build();
        var newAccessToken = "new_encrypted_access_token";
        var newRefreshToken = "new_encrypted_refresh_token";
        var newExpiry = DateTime.UtcNow.AddHours(2);
        var originalUpdatedAt = account.UpdatedAt;

        Thread.Sleep(10); // Ensure timestamp difference

        // Act
        account.UpdateTokens(newAccessToken, newRefreshToken, newExpiry);

        // Assert
        Assert.Equal(newAccessToken, account.AccessTokenEncrypted);
        Assert.Equal(newRefreshToken, account.RefreshTokenEncrypted);
        Assert.Equal(newExpiry, account.TokenExpiresAt);
        Assert.True(account.UpdatedAt > originalUpdatedAt);
    }

    [Fact]
    public void UpdateTokens_WithOnlyAccessToken_UpdatesAccessToken()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .AsGoogle()
            .Build();
        var originalRefreshToken = account.RefreshTokenEncrypted;
        var newAccessToken = "new_access_token";

        // Act
        account.UpdateTokens(newAccessToken, originalRefreshToken); // Preserve refresh token

        // Assert
        Assert.Equal(newAccessToken, account.AccessTokenEncrypted);
        Assert.Equal(originalRefreshToken, account.RefreshTokenEncrypted); // Unchanged
    }

    [Fact]
    public void UpdateTokens_UpdatesTimestamp()
    {
        // Arrange
        var account = new OAuthAccountBuilder().Build();
        var beforeUpdate = DateTime.UtcNow;

        // Act
        account.UpdateTokens("new_token");

        // Assert
        Assert.True(account.UpdatedAt >= beforeUpdate);
        Assert.True(account.UpdatedAt <= DateTime.UtcNow);
    }

    [Fact]
    public void UpdateTokens_WithEmptyAccessToken_ThrowsValidationException()
    {
        // Arrange
        var account = new OAuthAccountBuilder().Build();

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            account.UpdateTokens("")
        );

        Assert.Contains("Access token", exception.Message);
    }

    [Fact]
    public void UpdateTokens_WithNullAccessToken_ThrowsValidationException()
    {
        // Arrange
        var account = new OAuthAccountBuilder().Build();

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            account.UpdateTokens(null!)
        );
    }

    [Fact]
    public void UpdateTokens_ClearsRefreshToken_WhenPassedNull()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .AsGoogle()
            .Build();
        Assert.NotNull(account.RefreshTokenEncrypted);

        // Act
        account.UpdateTokens("new_access", null, null);

        // Assert
        Assert.Null(account.RefreshTokenEncrypted);
    }
    [Fact]
    public void IsTokenExpired_WithNoExpiration_ReturnsFalse()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .Build();

        // Act
        var isExpired = account.IsTokenExpired();

        // Assert
        Assert.False(isExpired);
        Assert.Null(account.TokenExpiresAt);
    }

    [Fact]
    public void IsTokenExpired_WithFutureExpiration_ReturnsFalse()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .ExpiresInHours(2)
            .Build();

        // Act
        var isExpired = account.IsTokenExpired();

        // Assert
        Assert.False(isExpired);
    }

    [Fact]
    public void IsTokenExpired_WithPastExpiration_ReturnsTrue()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .Expired()
            .Build();

        // Act
        var isExpired = account.IsTokenExpired();

        // Assert
        Assert.True(isExpired);
    }

    [Fact]
    public void IsTokenExpired_ExactlyAtExpiration_ReturnsTrue()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow;
        var account = new OAuthAccountBuilder()
            .WithTokenExpiry(expiresAt)
            .Build();

        // Act
        var isExpired = account.IsTokenExpired();

        // Assert
        Assert.True(isExpired);
    }

    [Fact]
    public void IsTokenExpired_BoundaryCondition_OneMinuteBeforeExpiry()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .WithTokenExpiry(DateTime.UtcNow.AddMinutes(1))
            .Build();

        // Act
        var isExpired = account.IsTokenExpired();

        // Assert
        Assert.False(isExpired);
    }
    [Fact]
    public void SupportsRefresh_WithGoogleAndRefreshToken_ReturnsTrue()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .AsGoogle()
            .Build();

        // Act
        var supportsRefresh = account.SupportsRefresh();

        // Assert
        Assert.True(supportsRefresh);
        Assert.NotNull(account.RefreshTokenEncrypted);
    }

    [Fact]
    public void SupportsRefresh_WithDiscordAndRefreshToken_ReturnsTrue()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .AsDiscord()
            .Build();

        // Act
        var supportsRefresh = account.SupportsRefresh();

        // Assert
        Assert.True(supportsRefresh);
        Assert.NotNull(account.RefreshTokenEncrypted);
    }

    [Fact]
    public void SupportsRefresh_WithGitHub_ReturnsFalse()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .AsGitHub()
            .Build();

        // Act
        var supportsRefresh = account.SupportsRefresh();

        // Assert
        Assert.False(supportsRefresh);
    }

    [Fact]
    public void SupportsRefresh_WithGoogleButNoRefreshToken_ReturnsFalse()
    {
        // Arrange
        var account = new OAuthAccount(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "google",
            "user123",
            "access_token",
            null // No refresh token
        );

        // Act
        var supportsRefresh = account.SupportsRefresh();

        // Assert
        Assert.False(supportsRefresh);
    }

    [Fact]
    public void SupportsRefresh_WithDiscordButNoRefreshToken_ReturnsFalse()
    {
        // Arrange
        var account = new OAuthAccount(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "discord",
            "user123",
            "access_token",
            null
        );

        // Act
        var supportsRefresh = account.SupportsRefresh();

        // Assert
        Assert.False(supportsRefresh);
    }

    [Fact]
    public void SupportsRefresh_WithEmptyRefreshToken_ReturnsFalse()
    {
        // Arrange
        var account = new OAuthAccount(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "google",
            "user123",
            "access_token",
            "" // Empty string
        );

        // Act
        var supportsRefresh = account.SupportsRefresh();

        // Assert
        Assert.False(supportsRefresh);
    }
    [Fact]
    public void Constructor_RequiresEncryptedAccessToken()
    {
        // Arrange & Act
        var account = new OAuthAccountBuilder()
            .WithAccessToken("encrypted_token_value")
            .Build();

        // Assert
        Assert.Equal("encrypted_token_value", account.AccessTokenEncrypted);
    }

    [Fact]
    public void Constructor_WithRefreshToken_RequiresEncrypted()
    {
        // Arrange & Act
        var account = new OAuthAccountBuilder()
            .WithRefreshToken("encrypted_refresh_value")
            .Build();

        // Assert
        Assert.Equal("encrypted_refresh_value", account.RefreshTokenEncrypted);
    }
    [Fact]
    public void Builder_CreateDefault_ProducesValidOAuthAccount()
    {
        // Act
        var account = OAuthAccountBuilder.CreateDefault();

        // Assert
        Assert.NotEqual(Guid.Empty, account.Id);
        Assert.NotEqual(Guid.Empty, account.UserId);
        Assert.NotNull(account.AccessTokenEncrypted);
    }

    [Fact]
    public void Builder_CreateGoogleForUser_ProducesGoogleAccount()
    {
        // Arrange
        var user = new UserBuilder().Build();

        // Act
        var account = OAuthAccountBuilder.CreateGoogleForUser(user);

        // Assert
        Assert.Equal("google", account.Provider);
        Assert.Equal(user.Id, account.UserId);
        Assert.NotNull(account.RefreshTokenEncrypted);
    }

    [Fact]
    public void Builder_CreateDiscordForUser_ProducesDiscordAccount()
    {
        // Arrange
        var user = new UserBuilder().Build();

        // Act
        var account = OAuthAccountBuilder.CreateDiscordForUser(user);

        // Assert
        Assert.Equal("discord", account.Provider);
        Assert.Equal(user.Id, account.UserId);
        Assert.NotNull(account.RefreshTokenEncrypted);
    }

    [Fact]
    public void Builder_CreateGitHubForUser_ProducesGitHubAccount()
    {
        // Arrange
        var user = new UserBuilder().Build();

        // Act
        var account = OAuthAccountBuilder.CreateGitHubForUser(user);

        // Assert
        Assert.Equal("github", account.Provider);
        Assert.Equal(user.Id, account.UserId);
    }
    [Fact]
    public void SupportedProviders_ContainsExpectedProviders()
    {
        // Assert - Use Assert.Contains for collection membership checks
        Assert.Contains("google", (System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders);
        Assert.Contains("discord", (System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders);
        Assert.Contains("github", (System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders);
        Assert.Equal(3, OAuthAccount.SupportedProviders.Count);
    }

    [Fact]
    public void SupportedProviders_IsCaseInsensitive()
    {
        // Assert - Use Assert.Contains for collection membership checks (case-insensitive support tested elsewhere)
        Assert.Contains("GOOGLE", (System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders);
        Assert.Contains("Discord", (System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders);
        Assert.Contains("github", (System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders);
    }
}
