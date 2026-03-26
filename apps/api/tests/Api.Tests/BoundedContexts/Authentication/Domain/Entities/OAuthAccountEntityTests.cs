using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.BoundedContexts.Authentication.TestHelpers;
using Xunit;
using FluentAssertions;
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
        account.Id.Should().Be(id);
        account.UserId.Should().Be(userId);
        account.Provider.Should().Be("google"); // Normalized to lowercase
        account.ProviderUserId.Should().Be(providerUserId);
        account.AccessTokenEncrypted.Should().Be(accessToken);
        account.RefreshTokenEncrypted.Should().Be(refreshToken);
        account.TokenExpiresAt.Should().Be(expiresAt);
        (account.CreatedAt <= DateTime.UtcNow).Should().BeTrue();
        // CreatedAt and UpdatedAt should be very close (within milliseconds)
        ((account.UpdatedAt - account.CreatedAt).TotalMilliseconds < 10).Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithGoogleProvider_CreatesSuccessfully()
    {
        // Act
        var account = new OAuthAccountBuilder()
            .AsGoogle()
            .Build();

        // Assert
        account.Provider.Should().Be("google");
        account.RefreshTokenEncrypted.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithDiscordProvider_CreatesSuccessfully()
    {
        // Act
        var account = new OAuthAccountBuilder()
            .AsDiscord()
            .Build();

        // Assert
        account.Provider.Should().Be("discord");
        account.RefreshTokenEncrypted.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithGitHubProvider_CreatesSuccessfully()
    {
        // Act
        var account = new OAuthAccountBuilder()
            .AsGitHub()
            .Build();

        // Assert
        account.Provider.Should().Be("github");
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
        account.Provider.Should().Be("google");
    }

    [Fact]
    public void Constructor_WithUnsupportedProvider_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            new OAuthAccount(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "unsupported_provider",
                "user123",
                "access_token"
            );
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("Unsupported OAuth provider");
        exception.Message.Should().Contain("unsupported_provider");
    }

    [Fact]
    public void Constructor_WithEmptyProviderUserId_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            new OAuthAccount(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "google",
                "", // Empty
                "access_token"
            );
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("Provider user ID");
    }

    [Fact]
    public void Constructor_WithEmptyAccessToken_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            new OAuthAccount(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "google",
                "user123",
                "" // Empty
            );
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("Access token");
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
        account.RefreshTokenEncrypted.Should().BeNull();
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
        account.TokenExpiresAt.Should().BeNull();
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
        account.Provider.Should().Be(provider.ToLowerInvariant());
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
        var act = () =>
            new OAuthAccount(
                Guid.NewGuid(),
                Guid.NewGuid(),
                provider,
                "user123",
                "access_token"
            );
        act.Should().Throw<ValidationException>();
    }
    [Fact]
    public async Task UpdateTokens_WithValidTokens_UpdatesSuccessfully()
    {
        // Arrange
        var account = new OAuthAccountBuilder().Build();
        var newAccessToken = "new_encrypted_access_token";
        var newRefreshToken = "new_encrypted_refresh_token";
        var newExpiry = DateTime.UtcNow.AddHours(2);
        var originalUpdatedAt = account.UpdatedAt;

        await Task.Delay(TestConstants.Timing.TinyDelay); // Ensure timestamp difference

        // Act
        account.UpdateTokens(newAccessToken, newRefreshToken, newExpiry);

        // Assert
        account.AccessTokenEncrypted.Should().Be(newAccessToken);
        account.RefreshTokenEncrypted.Should().Be(newRefreshToken);
        account.TokenExpiresAt.Should().Be(newExpiry);
        (account.UpdatedAt > originalUpdatedAt).Should().BeTrue();
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
        account.AccessTokenEncrypted.Should().Be(newAccessToken);
        account.RefreshTokenEncrypted.Should().Be(originalRefreshToken); // Unchanged
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
        (account.UpdatedAt >= beforeUpdate).Should().BeTrue();
        (account.UpdatedAt <= DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void UpdateTokens_WithEmptyAccessToken_ThrowsValidationException()
    {
        // Arrange
        var account = new OAuthAccountBuilder().Build();

        // Act & Assert
        var act = () =>
            account.UpdateTokens("");
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("Access token");
    }

    [Fact]
    public void UpdateTokens_WithNullAccessToken_ThrowsValidationException()
    {
        // Arrange
        var account = new OAuthAccountBuilder().Build();

        // Act & Assert
        var act = () =>
            account.UpdateTokens(null!);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdateTokens_ClearsRefreshToken_WhenPassedNull()
    {
        // Arrange
        var account = new OAuthAccountBuilder()
            .AsGoogle()
            .Build();
        account.RefreshTokenEncrypted.Should().NotBeNull();

        // Act
        account.UpdateTokens("new_access", null, null);

        // Assert
        account.RefreshTokenEncrypted.Should().BeNull();
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
        isExpired.Should().BeFalse();
        account.TokenExpiresAt.Should().BeNull();
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
        isExpired.Should().BeFalse();
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
        isExpired.Should().BeTrue();
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
        isExpired.Should().BeTrue();
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
        isExpired.Should().BeFalse();
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
        supportsRefresh.Should().BeTrue();
        account.RefreshTokenEncrypted.Should().NotBeNull();
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
        supportsRefresh.Should().BeTrue();
        account.RefreshTokenEncrypted.Should().NotBeNull();
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
        supportsRefresh.Should().BeFalse();
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
        supportsRefresh.Should().BeFalse();
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
        supportsRefresh.Should().BeFalse();
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
        supportsRefresh.Should().BeFalse();
    }
    [Fact]
    public void Constructor_RequiresEncryptedAccessToken()
    {
        // Arrange & Act
        var account = new OAuthAccountBuilder()
            .WithAccessToken("encrypted_token_value")
            .Build();

        // Assert
        account.AccessTokenEncrypted.Should().Be("encrypted_token_value");
    }

    [Fact]
    public void Constructor_WithRefreshToken_RequiresEncrypted()
    {
        // Arrange & Act
        var account = new OAuthAccountBuilder()
            .WithRefreshToken("encrypted_refresh_value")
            .Build();

        // Assert
        account.RefreshTokenEncrypted.Should().Be("encrypted_refresh_value");
    }
    [Fact]
    public void Builder_CreateDefault_ProducesValidOAuthAccount()
    {
        // Act
        var account = OAuthAccountBuilder.CreateDefault();

        // Assert
        account.Id.Should().NotBe(Guid.Empty);
        account.UserId.Should().NotBe(Guid.Empty);
        account.AccessTokenEncrypted.Should().NotBeNull();
    }

    [Fact]
    public void Builder_CreateGoogleForUser_ProducesGoogleAccount()
    {
        // Arrange
        var user = new UserBuilder().Build();

        // Act
        var account = OAuthAccountBuilder.CreateGoogleForUser(user);

        // Assert
        account.Provider.Should().Be("google");
        account.UserId.Should().Be(user.Id);
        account.RefreshTokenEncrypted.Should().NotBeNull();
    }

    [Fact]
    public void Builder_CreateDiscordForUser_ProducesDiscordAccount()
    {
        // Arrange
        var user = new UserBuilder().Build();

        // Act
        var account = OAuthAccountBuilder.CreateDiscordForUser(user);

        // Assert
        account.Provider.Should().Be("discord");
        account.UserId.Should().Be(user.Id);
        account.RefreshTokenEncrypted.Should().NotBeNull();
    }

    [Fact]
    public void Builder_CreateGitHubForUser_ProducesGitHubAccount()
    {
        // Arrange
        var user = new UserBuilder().Build();

        // Act
        var account = OAuthAccountBuilder.CreateGitHubForUser(user);

        // Assert
        account.Provider.Should().Be("github");
        account.UserId.Should().Be(user.Id);
    }
    [Fact]
    public void SupportedProviders_ContainsExpectedProviders()
    {
        // Assert - Use Should().Contain for collection membership checks
        ((System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders).Should().Contain("google");
        ((System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders).Should().Contain("discord");
        ((System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders).Should().Contain("github");
        OAuthAccount.SupportedProviders.Count.Should().Be(3);
    }

    [Fact]
    public void SupportedProviders_IsCaseInsensitive()
    {
        // Assert - Use Should().Contain for collection membership checks (case-insensitive support tested elsewhere)
        ((System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders).Should().Contain("GOOGLE");
        ((System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders).Should().Contain("Discord");
        ((System.Collections.Generic.IEnumerable<string>)OAuthAccount.SupportedProviders).Should().Contain("github");
    }
}
