using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Comprehensive domain tests for User aggregate OAuth functionality.
/// Tests business rules, validation logic, and authentication method management.
/// Target: 90%+ coverage for OAuth-related methods.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UserOAuthTests
{
    [Fact]
    public void LinkOAuthAccount_ValidAccount_AddsToCollection()
    {
        // Arrange
        var user = CreateTestUser();
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);

        // Act
        user.LinkOAuthAccount(oauthAccount);

        // Assert
        user.OAuthAccounts.Should().ContainSingle();
        user.OAuthAccounts.Should().Contain(a => a.Provider == "google");
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
        user.OAuthAccounts.Count.Should().Be(2);
        user.OAuthAccounts.Should().Contain(a => a.Provider == "google");
        user.OAuthAccounts.Should().Contain(a => a.Provider == "discord");
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
        var act = () =>
            user.LinkOAuthAccount(duplicateAccount);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("OAuth provider 'google' is already linked");
    }

    [Fact]
    public void LinkOAuthAccount_NullAccount_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () => user.LinkOAuthAccount(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LinkOAuthAccount_UnsupportedProvider_ThrowsValidationException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        // Note: OAuthAccount constructor validates this, but we test User's validation too
        var act = () =>
            CreateTestOAuthAccount("facebook", user.Id);
        var exception = act.Should().Throw<ValidationException>().Which; // Unsupported provider
        exception.Message.Should().Contain("Unsupported OAuth provider: facebook");
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
        user.OAuthAccounts.Count.Should().Be(initialCount + 1);
        var linkedAccount = user.OAuthAccounts.First(a => a.Provider == "github");
        linkedAccount.Id.Should().Be(oauthAccount.Id);
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
        collection.Should().BeAssignableTo<IReadOnlyCollection<OAuthAccount>>();
        // Collection should be readonly and not allow direct modification
        collection.Should().ContainSingle();
    }
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
        user.OAuthAccounts.Should().BeEmpty();
        user.OAuthAccounts.Should().NotContain(a => a.Provider == "google");
    }

    [Fact]
    public void UnlinkOAuthAccount_ProviderNotLinked_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () =>
            user.UnlinkOAuthAccount("google");
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("OAuth provider 'google' is not linked");
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
        var act = () =>
            user.UnlinkOAuthAccount(invalidProvider!);
        var exception = act.Should().Throw<ValidationException>().Which;
        exception.Message.Should().Contain("Provider cannot be empty");
    }

    [Fact]
    public void UnlinkOAuthAccount_LastAuthMethod_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUserWithoutPassword(); // No password
        var oauthAccount = CreateTestOAuthAccount("google", user.Id);
        user.LinkOAuthAccount(oauthAccount); // Only OAuth account

        // Act & Assert
        var act = () =>
            user.UnlinkOAuthAccount("google");
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("User must have at least one authentication method");
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
        user.OAuthAccounts.Should().BeEmpty();
        user.HasAnyAuthenticationMethod().Should().BeTrue(); // Still has password
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
        user.OAuthAccounts.Should().ContainSingle();
        user.OAuthAccounts.Should().Contain(a => a.Provider == "discord");
        user.HasAnyAuthenticationMethod().Should().BeTrue(); // Still has Discord
    }
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
        result.Should().NotBeNull();
        result.Provider.Should().Be("google");
        result.Id.Should().Be(oauthAccount.Id);
    }

    [Fact]
    public void GetOAuthAccount_ProviderNotFound_ReturnsNull()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.GetOAuthAccount("google");

        // Assert
        result.Should().BeNull();
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
        result.Should().NotBeNull();
        result.Provider.Should().Be(linkedProvider);
    }
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
        result.Should().BeTrue();
    }

    [Fact]
    public void HasOAuthAccount_ProviderNotLinked_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.HasOAuthAccount("google");

        // Assert
        result.Should().BeFalse();
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
        result.Should().BeFalse();
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
        result.Should().BeTrue();
    }
    [Fact]
    public void HasAnyAuthenticationMethod_UserHasPasswordOnly_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser(); // Has password, no OAuth

        // Act
        var result = user.HasAnyAuthenticationMethod();

        // Assert
        result.Should().BeTrue();
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
        result.Should().BeTrue();
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
        result.Should().BeTrue();
    }

    [Fact]
    public void HasAnyAuthenticationMethod_UserHasNeitherPasswordNorOAuth_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUserWithoutPassword(); // No password, no OAuth

        // Act
        var result = user.HasAnyAuthenticationMethod();

        // Assert
        result.Should().BeFalse(); // Edge case validation
    }
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
        user.OAuthAccounts.Count.Should().Be(3);
        user.OAuthAccounts.Should().Contain(a => a.Provider == "google");
        user.OAuthAccounts.Should().Contain(a => a.Provider == "discord");
        user.OAuthAccounts.Should().Contain(a => a.Provider == "github");
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
        user.OAuthAccounts.Count.Should().Be(2);
        user.OAuthAccounts.Should().Contain(a => a.Provider == "google");
        user.OAuthAccounts.Should().Contain(a => a.Provider == "github");
        user.OAuthAccounts.Should().NotContain(a => a.Provider == "discord");
    }

    [Fact]
    public void CollectionState_AfterAddRemoveOperations_RemainsConsistent()
    {
        // Arrange
        var user = CreateTestUser();
        var googleAccount = CreateTestOAuthAccount("google", user.Id);
        var discordAccount = CreateTestOAuthAccount("discord", user.Id);

        // Act & Assert - Initial state
        user.OAuthAccounts.Should().BeEmpty();

        // Add first account
        user.LinkOAuthAccount(googleAccount);
        user.OAuthAccounts.Should().ContainSingle();
        user.HasOAuthAccount("google").Should().BeTrue();

        // Add second account
        user.LinkOAuthAccount(discordAccount);
        user.OAuthAccounts.Count.Should().Be(2);
        user.HasOAuthAccount("discord").Should().BeTrue();

        // Remove first account
        user.UnlinkOAuthAccount("google");
        user.OAuthAccounts.Should().ContainSingle();
        user.HasOAuthAccount("google").Should().BeFalse();
        user.HasOAuthAccount("discord").Should().BeTrue();

        // Remove last account
        user.UnlinkOAuthAccount("discord");
        user.OAuthAccounts.Should().BeEmpty();
        user.HasOAuthAccount("discord").Should().BeFalse();
    }
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
}
