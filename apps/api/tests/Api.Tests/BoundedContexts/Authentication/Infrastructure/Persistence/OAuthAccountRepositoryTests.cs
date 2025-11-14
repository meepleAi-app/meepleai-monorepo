using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Integration tests for OAuthAccountRepository using Testcontainers with real PostgreSQL.
/// Tests OAuth provider linking, token management, and multi-provider scenarios.
/// </summary>
public class OAuthAccountRepositoryTests : IntegrationTestBase<OAuthAccountRepository>
{
    protected override string DatabaseName => "meepleai_oauth_test";

    protected override OAuthAccountRepository CreateRepository(MeepleAiDbContext dbContext)
        => new OAuthAccountRepository(dbContext, NullLogger<OAuthAccountRepository>.Instance);

    #region GetByUserIdAndProviderAsync Tests

    [Fact]
    public async Task Test01_GetByUserIdAndProviderAsync_ExistingAccount_ReturnsAccount()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "google", "google_user_123");
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await Repository.GetByUserIdAndProviderAsync(userId, "google");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(account.Id, result.Id);
        Assert.Equal("google", result.Provider);
        Assert.Equal("google_user_123", result.ProviderUserId);
    }

    [Fact]
    public async Task Test02_GetByUserIdAndProviderAsync_CaseInsensitiveProvider_ReturnsAccount()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "github", "github_user_456");
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Act - Search with uppercase
        var result = await Repository.GetByUserIdAndProviderAsync(userId, "GITHUB");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(account.Id, result.Id);
    }

    [Fact]
    public async Task Test03_GetByUserIdAndProviderAsync_NonExisting_ReturnsNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();

        // Act
        var result = await Repository.GetByUserIdAndProviderAsync(userId, "google");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByProviderUserIdAsync Tests

    [Fact]
    public async Task Test04_GetByProviderUserIdAsync_ExistingAccount_ReturnsAccount()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "discord", "discord_user_789");
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Act
        var result = await Repository.GetByProviderUserIdAsync("discord", "discord_user_789");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(account.Id, result.Id);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("discord_user_789", result.ProviderUserId);
    }

    [Fact]
    public async Task Test05_GetByProviderUserIdAsync_ForOAuthLogin_FindsCorrectUser()
    {
        // Arrange - Simulate OAuth login scenario
        await ResetDatabaseAsync();
        var user1Id = await CreateTestUserAsync();
        var user2Id = await CreateTestUserAsync();

        var account1 = CreateTestOAuthAccount(user1Id, "google", "google_12345");
        var account2 = CreateTestOAuthAccount(user2Id, "google", "google_67890");

        await Repository.AddAsync(account1);
        await Repository.AddAsync(account2);
        await DbContext.SaveChangesAsync();

        // Act - OAuth provider returns google_12345
        var result = await Repository.GetByProviderUserIdAsync("google", "google_12345");

        // Assert - Should find user1
        Assert.NotNull(result);
        Assert.Equal(user1Id, result.UserId);
    }

    [Fact]
    public async Task Test06_GetByProviderUserIdAsync_NonExisting_ReturnsNull()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await Repository.GetByProviderUserIdAsync("google", "nonexistent_id");

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task Test07_GetByUserIdAsync_NoAccounts_ReturnsEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();

        // Act
        var accounts = await Repository.GetByUserIdAsync(userId);

        // Assert
        Assert.Empty(accounts);
    }

    [Fact]
    public async Task Test08_GetByUserIdAsync_MultipleProviders_ReturnsAllOrdered()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();

        var googleAccount = CreateTestOAuthAccount(userId, "google", "google_user");
        var githubAccount = CreateTestOAuthAccount(userId, "github", "github_user");
        var discordAccount = CreateTestOAuthAccount(userId, "discord", "discord_user");

        await Repository.AddAsync(googleAccount);
        await Repository.AddAsync(githubAccount);
        await Repository.AddAsync(discordAccount);
        await DbContext.SaveChangesAsync();

        // Act
        var accounts = await Repository.GetByUserIdAsync(userId);

        // Assert
        Assert.Equal(3, accounts.Count);
        // Should be ordered by Provider alphabetically
        Assert.Equal("discord", accounts[0].Provider);
        Assert.Equal("github", accounts[1].Provider);
        Assert.Equal("google", accounts[2].Provider);
    }

    [Fact]
    public async Task Test09_GetByUserIdAsync_MultipleUsers_FiltersCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1Id = await CreateTestUserAsync();
        var user2Id = await CreateTestUserAsync();

        var user1Google = CreateTestOAuthAccount(user1Id, "google", "user1_google");
        var user1Github = CreateTestOAuthAccount(user1Id, "github", "user1_github");
        var user2Google = CreateTestOAuthAccount(user2Id, "google", "user2_google");

        await Repository.AddAsync(user1Google);
        await Repository.AddAsync(user1Github);
        await Repository.AddAsync(user2Google);
        await DbContext.SaveChangesAsync();

        // Act
        var user1Accounts = await Repository.GetByUserIdAsync(user1Id);
        var user2Accounts = await Repository.GetByUserIdAsync(user2Id);

        // Assert
        Assert.Equal(2, user1Accounts.Count);
        Assert.Single(user2Accounts);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task Test10_AddAsync_NewAccount_PersistsSuccessfully()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(
            userId,
            "google",
            "google_unique_id",
            "encrypted_access_token_123",
            "encrypted_refresh_token_456",
            DateTime.UtcNow.AddHours(1)
        );

        // Act
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id);
        Assert.NotNull(persisted);
        Assert.Equal(userId, persisted.UserId);
        Assert.Equal("google", persisted.Provider);
        Assert.Equal("google_unique_id", persisted.ProviderUserId);
        Assert.Equal("encrypted_access_token_123", persisted.AccessTokenEncrypted);
        Assert.Equal("encrypted_refresh_token_456", persisted.RefreshTokenEncrypted);
        Assert.NotNull(persisted.TokenExpiresAt);
    }

    [Fact]
    public async Task Test11_AddAsync_WithoutRefreshToken_PersistsCorrectly()
    {
        // Arrange - Some providers don't provide refresh tokens
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(
            userId,
            "github",
            "github_user",
            "access_token_only",
            refreshToken: null
        );

        // Act
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted.RefreshTokenEncrypted);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Test12_UpdateAsync_RefreshTokens_UpdatesCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "google", "google_refresh_test");
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Act - Simulate token refresh
        var trackedAccount = await Repository.GetByUserIdAndProviderAsync(userId, "google");
        Assert.NotNull(trackedAccount);
        trackedAccount.UpdateTokens(
            "new_access_token_encrypted",
            "new_refresh_token_encrypted",
            DateTime.UtcNow.AddHours(2)
        );
        DbContext.ChangeTracker.Clear(); // Clear any tracked entities before update
        await Repository.UpdateAsync(trackedAccount);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await Repository.GetByUserIdAndProviderAsync(userId, "google");
        Assert.NotNull(updated);
        Assert.Equal("new_access_token_encrypted", updated.AccessTokenEncrypted);
        Assert.Equal("new_refresh_token_encrypted", updated.RefreshTokenEncrypted);
        Assert.NotNull(updated.TokenExpiresAt);
        Assert.True(updated.UpdatedAt > updated.CreatedAt);
    }

    [Fact]
    public async Task Test13_UpdateAsync_AccessTokenOnly_UpdatesWithoutRefresh()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "discord", "discord_update_test");
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Act
        var trackedAccount = await Repository.GetByUserIdAndProviderAsync(userId, "discord");
        Assert.NotNull(trackedAccount);
        trackedAccount.UpdateTokens("new_access_token_only", null, DateTime.UtcNow.AddHours(1));
        DbContext.ChangeTracker.Clear(); // Clear any tracked entities before update
        await Repository.UpdateAsync(trackedAccount);
        await DbContext.SaveChangesAsync();

        // Assert
        var updated = await Repository.GetByUserIdAndProviderAsync(userId, "discord");
        Assert.NotNull(updated);
        Assert.Equal("new_access_token_only", updated.AccessTokenEncrypted);
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task Test14_DeleteAsync_ExistingAccount_RemovesFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "google", "delete_test");
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Act
        await Repository.DeleteAsync(account);
        await DbContext.SaveChangesAsync();

        // Assert
        var deleted = await DbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task Test15_DeleteAsync_NonExistingAccount_DoesNotThrow()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "google", "nonexistent");

        // Act & Assert - Should not throw
        await Repository.DeleteAsync(account);
        await DbContext.SaveChangesAsync();
    }

    #endregion

    #region Mapping Tests

    [Fact]
    public async Task Test16_Mapping_DomainToPersistence_AllFieldsCorrect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var tokenExpiry = DateTime.UtcNow.AddHours(1);
        var account = CreateTestOAuthAccount(
            userId,
            "github",
            "github_mapping_test",
            "access_token_mapped",
            "refresh_token_mapped",
            tokenExpiry
        );

        // Act
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Assert
        var persisted = await DbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id);
        Assert.NotNull(persisted);
        Assert.Equal(account.Id, persisted.Id);
        Assert.Equal(account.UserId, persisted.UserId);
        Assert.Equal(account.Provider, persisted.Provider);
        Assert.Equal(account.ProviderUserId, persisted.ProviderUserId);
        Assert.Equal(account.AccessTokenEncrypted, persisted.AccessTokenEncrypted);
        Assert.Equal(account.RefreshTokenEncrypted, persisted.RefreshTokenEncrypted);
        Assert.Equal(account.TokenExpiresAt, persisted.TokenExpiresAt);
    }

    [Fact]
    public async Task Test17_Mapping_PersistenceToDomain_AllFieldsCorrect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "discord", "roundtrip_test");
        await Repository.AddAsync(account);
        await DbContext.SaveChangesAsync();

        // Act
        var retrieved = await Repository.GetByUserIdAndProviderAsync(userId, "discord");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(account.Id, retrieved.Id);
        Assert.Equal(account.UserId, retrieved.UserId);
        Assert.Equal(account.Provider, retrieved.Provider);
        Assert.Equal(account.ProviderUserId, retrieved.ProviderUserId);
        Assert.Equal(account.AccessTokenEncrypted, retrieved.AccessTokenEncrypted);
    }

    #endregion

    #region Multi-Provider Scenarios

    [Fact]
    public async Task Test18_MultiProvider_UserWithThreeProviders_AllPersistCorrectly()
    {
        // Arrange - User links Google, GitHub, and Discord
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();

        var googleAccount = CreateTestOAuthAccount(userId, "google", "multi_google");
        var githubAccount = CreateTestOAuthAccount(userId, "github", "multi_github");
        var discordAccount = CreateTestOAuthAccount(userId, "discord", "multi_discord");

        // Act
        await Repository.AddAsync(googleAccount);
        await Repository.AddAsync(githubAccount);
        await Repository.AddAsync(discordAccount);
        await DbContext.SaveChangesAsync();

        // Assert
        var allAccounts = await Repository.GetByUserIdAsync(userId);
        Assert.Equal(3, allAccounts.Count);

        var google = await Repository.GetByUserIdAndProviderAsync(userId, "google");
        var github = await Repository.GetByUserIdAndProviderAsync(userId, "github");
        var discord = await Repository.GetByUserIdAndProviderAsync(userId, "discord");

        Assert.NotNull(google);
        Assert.NotNull(github);
        Assert.NotNull(discord);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test user in the database to satisfy FK constraints for OAuth accounts.
    /// </summary>
    private async Task<Guid> CreateTestUserAsync()
    {
        var userId = Guid.NewGuid();
        var userEntity = new Api.Infrastructure.Entities.UserEntity
        {
            Id = userId,
            Email = $"test_{userId:N}@test.com",
            DisplayName = "Test User",
            PasswordHash = "dummy_password_hash",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = false
        };

        DbContext.Users.Add(userEntity);
        await DbContext.SaveChangesAsync();

        // Clear change tracker to prevent navigation property issues
        DbContext.ChangeTracker.Clear();

        return userId;
    }

    private static OAuthAccount CreateTestOAuthAccount(
        Guid userId,
        string provider,
        string providerUserId,
        string? accessToken = null,
        string? refreshToken = null,
        DateTime? tokenExpiresAt = null)
    {
        return new OAuthAccount(
            id: Guid.NewGuid(),
            userId: userId,
            provider: provider,
            providerUserId: providerUserId,
            accessTokenEncrypted: accessToken ?? $"encrypted_access_{Guid.NewGuid()}",
            refreshTokenEncrypted: refreshToken,
            tokenExpiresAt: tokenExpiresAt
        );
    }

    #endregion
}