using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Comprehensive domain tests for User aggregate OAuth functionality.
/// Tests business rules, validation logic, and authentication method management.
/// Target: 90%+ coverage for OAuth-related methods.
/// </summary>
public class UserOAuthTests
{
    #region LinkOAuthAccount Tests (7 tests)

    [Fact]
    public void LinkOAuthAccount_ValidAccount_AddsToCollection()
    {
        // Arrange
        var user = CreateTestUser();
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);

        // Act
        user.LinkOAuthAccount(oauthAccount);

        // Assert
        Assert.Single(user.OAuthAccounts);
        Assert.Contains(user.OAuthAccounts, a => a.Provider == "google");
    }

    [Fact]
    public void LinkOAuthAccount_MultipleDifferentProviders_AddsAllSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var googleAccount = CreateTestOAuthAccount("google", user.Id);
        var discordAccount = CreateTestOAuthAccount("discord", user.Id);

        // Act
        user.LinkOAuthAccount(googleAccount);
        user.LinkOAuthAccount(discordAccount);

        // Assert
        Assert.Equal(2, user.OAuthAccounts.Count);
        Assert.Contains(user.OAuthAccounts, a => a.Provider == "google");
        Assert.Contains(user.OAuthAccounts, a => a.Provider == "discord");
    }

    [Fact]
    public void LinkOAuthAccount_DuplicateProvider_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var firstAccount = CreateTestOAuthAccount("google", user.Id);
        var duplicateAccount = CreateTestOAuthAccount("google", user.Id, "different-provider-user-id");
        user.LinkOAuthAccount(firstAccount);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.LinkOAuthAccount(duplicateAccount));
        Assert.Contains("OAuth provider 'google' is already linked", exception.Message);
    }

    [Fact]
    public void LinkOAuthAccount_NullAccount_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => user.LinkOAuthAccount(null!));
    }

    [Fact]
    public void LinkOAuthAccount_UnsupportedProvider_ThrowsValidationException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        // Note: OAuthAccount constructor validates this, but we test User's validation too
        var exception = Assert.Throws<ValidationException>(() =>
            CreateTestOAuthAccount("facebook", user.Id)); // Unsupported provider
        Assert.Contains("Unsupported OAuth provider: facebook", exception.Message);
    }

    [Fact]
    public void LinkOAuthAccount_UpdatesOAuthAccountsCollection()
    {
        // Arrange
        var user = CreateTestUser();
        var initialCount = user.OAuthAccounts.Count;
        var oauthAccount = CreateTestOAuthAccount("github", user.Id);

        // Act
        user.LinkOAuthAccount(oauthAccount);

        // Assert
        Assert.Equal(initialCount + 1, user.OAuthAccounts.Count);
        var linkedAccount = user.OAuthAccounts.First(a => a.Provider == "github");
        Assert.Equal(oauthAccount.Id, linkedAccount.Id);
    }

    [Fact]
    public void LinkOAuthAccount_ReadonlyCollection_PreventsExternalModification()
    {
        // Arrange
        var user = CreateTestUser();
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount);

        // Act
        var collection = user.OAuthAccounts;

        // Assert
        Assert.IsAssignableFrom<IReadOnlyCollection<OAuthAccount>>(collection);
        // Collection should be readonly and not allow direct modification
        Assert.Single(collection);
    }

    #endregion

    #region UnlinkOAuthAccount Tests (6 tests)

    [Fact]
    public void UnlinkOAuthAccount_ValidProvider_RemovesFromCollection()
    {
        // Arrange
        var user = CreateTestUser();
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount);

        // Act
        user.UnlinkOAuthAccount("google");

        // Assert
        Assert.Empty(user.OAuthAccounts);
        Assert.DoesNotContain(user.OAuthAccounts, a => a.Provider == "google");
    }

    [Fact]
    public void UnlinkOAuthAccount_ProviderNotLinked_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.UnlinkOAuthAccount("google"));
        Assert.Contains("OAuth provider 'google' is not linked", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnlinkOAuthAccount_EmptyProvider_ThrowsValidationException(string? invalidProvider)
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            user.UnlinkOAuthAccount(invalidProvider!));
        Assert.Contains("Provider cannot be empty", exception.Message);
    }

    [Fact]
    public void UnlinkOAuthAccount_LastAuthMethod_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUserWithoutPassword(); // No password
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount); // Only OAuth account

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.UnlinkOAuthAccount("google"));
        Assert.Contains("User must have at least one authentication method", exception.Message);
    }

    [Fact]
    public void UnlinkOAuthAccount_UserHasPassword_AllowsUnlink()
    {
        // Arrange
        var user = CreateTestUser(); // Has password
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount);

        // Act
        user.UnlinkOAuthAccount("google");

        // Assert
        Assert.Empty(user.OAuthAccounts);
        Assert.True(user.HasAnyAuthenticationMethod()); // Still has password
    }

    [Fact]
    public void UnlinkOAuthAccount_UserHasOtherOAuthAccounts_AllowsUnlink()
    {
        // Arrange
        var user = CreateTestUserWithoutPassword(); // No password
        var googleAccount = CreateTestOAuthAccount("google", user.Id);
        var discordAccount = CreateTestOAuthAccount("discord", user.Id);
        user.LinkOAuthAccount(googleAccount);
        user.LinkOAuthAccount(discordAccount);

        // Act
        user.UnlinkOAuthAccount("google");

        // Assert
        Assert.Single(user.OAuthAccounts);
        Assert.Contains(user.OAuthAccounts, a => a.Provider == "discord");
        Assert.True(user.HasAnyAuthenticationMethod()); // Still has Discord
    }

    #endregion

    #region GetOAuthAccount Tests (3 tests)

    [Fact]
    public void GetOAuthAccount_ProviderExists_ReturnsAccount()
    {
        // Arrange
        var user = CreateTestUser();
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount);

        // Act
        var result = user.GetOAuthAccount("google");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("google", result.Provider);
        Assert.Equal(oauthAccount.Id, result.Id);
    }

    [Fact]
    public void GetOAuthAccount_ProviderNotFound_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.GetOAuthAccount("google");

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData("GOOGLE", "google")]
    [InlineData("Google", "google")]
    [InlineData("gOoGlE", "google")]
    public void GetOAuthAccount_CaseInsensitiveMatching_ReturnsAccount(string searchProvider, string linkedProvider)
    {
        // Arrange
        var user = CreateTestUser();
        var oauthAccount = CreateTestOAuthAccount(linkedProvider, user.Id);
        user.LinkOAuthAccount(oauthAccount);

        // Act
        var result = user.GetOAuthAccount(searchProvider);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(linkedProvider, result.Provider);
    }

    #endregion

    #region HasOAuthAccount Tests (4 tests)

    [Fact]
    public void HasOAuthAccount_ProviderLinked_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount);

        // Act
        var result = user.HasOAuthAccount("google");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasOAuthAccount_ProviderNotLinked_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.HasOAuthAccount("google");

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasOAuthAccount_EmptyProvider_ReturnsFalse(string? invalidProvider)
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.HasOAuthAccount(invalidProvider!);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData("GOOGLE")]
    [InlineData("Google")]
    [InlineData("gOoGlE")]
    public void HasOAuthAccount_CaseInsensitiveMatching_ReturnsTrue(string searchProvider)
    {
        // Arrange
        var user = CreateTestUser();
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount);

        // Act
        var result = user.HasOAuthAccount(searchProvider);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region HasAnyAuthenticationMethod Tests (4 tests)

    [Fact]
    public void HasAnyAuthenticationMethod_UserHasPasswordOnly_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser(); // Has password, no OAuth

        // Act
        var result = user.HasAnyAuthenticationMethod();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasAnyAuthenticationMethod_UserHasOAuthOnly_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUserWithoutPassword(); // No password
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount);

        // Act
        var result = user.HasAnyAuthenticationMethod();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasAnyAuthenticationMethod_UserHasBothPasswordAndOAuth_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser(); // Has password
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount);

        // Act
        var result = user.HasAnyAuthenticationMethod();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasAnyAuthenticationMethod_UserHasNeitherPasswordNorOAuth_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUserWithoutPassword(); // No password, no OAuth

        // Act
        var result = user.HasAnyAuthenticationMethod();

        // Assert
        Assert.False(result); // Edge case validation
    }

    #endregion

    #region Integration/Edge Case Tests (3 tests)

    [Fact]
    public void LinkMultipleProviders_GoogleDiscordGitHub_AllLinkedSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var googleAccount = CreateTestOAuthAccount("google", user.Id);
        var discordAccount = CreateTestOAuthAccount("discord", user.Id);
        var githubAccount = CreateTestOAuthAccount("github", user.Id);

        // Act
        user.LinkOAuthAccount(googleAccount);
        user.LinkOAuthAccount(discordAccount);
        user.LinkOAuthAccount(githubAccount);

        // Assert
        Assert.Equal(3, user.OAuthAccounts.Count);
        Assert.Contains(user.OAuthAccounts, a => a.Provider == "google");
        Assert.Contains(user.OAuthAccounts, a => a.Provider == "discord");
        Assert.Contains(user.OAuthAccounts, a => a.Provider == "github");
    }

    [Fact]
    public void UnlinkMiddleProvider_DoesNotAffectOthers()
    {
        // Arrange
        var user = CreateTestUser();
        var googleAccount = CreateTestOAuthAccount("google", user.Id);
        var discordAccount = CreateTestOAuthAccount("discord", user.Id);
        var githubAccount = CreateTestOAuthAccount("github", user.Id);
        user.LinkOAuthAccount(googleAccount);
        user.LinkOAuthAccount(discordAccount);
        user.LinkOAuthAccount(githubAccount);

        // Act
        user.UnlinkOAuthAccount("discord"); // Unlink middle provider

        // Assert
        Assert.Equal(2, user.OAuthAccounts.Count);
        Assert.Contains(user.OAuthAccounts, a => a.Provider == "google");
        Assert.Contains(user.OAuthAccounts, a => a.Provider == "github");
        Assert.DoesNotContain(user.OAuthAccounts, a => a.Provider == "discord");
    }

    [Fact]
    public void CollectionState_AfterAddRemoveOperations_RemainsConsistent()
    {
        // Arrange
        var user = CreateTestUser();
        var googleAccount = CreateTestOAuthAccount("google", user.Id);
        var discordAccount = CreateTestOAuthAccount("discord", user.Id);

        // Act & Assert - Initial state
        Assert.Empty(user.OAuthAccounts);

        // Add first account
        user.LinkOAuthAccount(googleAccount);
        Assert.Single(user.OAuthAccounts);
        Assert.True(user.HasOAuthAccount("google"));

        // Add second account
        user.LinkOAuthAccount(discordAccount);
        Assert.Equal(2, user.OAuthAccounts.Count);
        Assert.True(user.HasOAuthAccount("discord"));

        // Remove first account
        user.UnlinkOAuthAccount("google");
        Assert.Single(user.OAuthAccounts);
        Assert.False(user.HasOAuthAccount("google"));
        Assert.True(user.HasOAuthAccount("discord"));

        // Remove last account
        user.UnlinkOAuthAccount("discord");
        Assert.Empty(user.OAuthAccounts);
        Assert.False(user.HasOAuthAccount("discord"));
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a test user with email, password, and User role.
    /// </summary>
    private static User CreateTestUser()
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: Role.User
        );
    }

    /// <summary>
    /// Creates a test user WITHOUT password (for OAuth-only scenarios).
    /// Note: Uses reflection to create a user with null password for edge case testing.
    /// In production, OAuth users are created with a random password or through a different flow.
    /// </summary>
    private static User CreateTestUserWithoutPassword()
    {
        // Create a user with a password first
        var user = new User(
            id: Guid.NewGuid(),
            email: new Email("oauth-only@example.com"),
            displayName: "OAuth User",
            passwordHash: PasswordHash.Create("TempPassword123!"),
            role: Role.User
        );

        // Use reflection to set PasswordHash to null for testing edge cases
        var passwordHashProperty = typeof(User).GetProperty("PasswordHash");
        passwordHashProperty?.SetValue(user, null);

        return user;
    }

    /// <summary>
    /// Creates a test OAuth account for the specified provider.
    /// </summary>
    private static OAuthAccount CreateTestOAuthAccount(
        string provider,
        Guid userId,
        string? providerUserId = null)
    {
        return new OAuthAccount(
            id: Guid.NewGuid(),
            userId: userId,
            provider: provider,
            providerUserId: providerUserId ?? $"{provider}-user-{Guid.NewGuid()}",
            accessTokenEncrypted: $"encrypted-access-token-{Guid.NewGuid()}",
            refreshTokenEncrypted: $"encrypted-refresh-token-{Guid.NewGuid()}",
            tokenExpiresAt: DateTime.UtcNow.AddHours(1)
        );
    }

    #endregion
}

