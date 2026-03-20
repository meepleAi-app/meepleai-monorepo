using System.Threading;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Integration tests for OAuthAccountRepository using shared Testcontainers with real PostgreSQL.
/// Tests OAuth provider linking, token management, and multi-provider scenarios.
/// Issue #2541: Migrated to SharedDatabaseTestBase for improved performance.
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
public class OAuthAccountRepositoryTests : SharedDatabaseTestBase<OAuthAccountRepository>
{
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public OAuthAccountRepositoryTests(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    protected override OAuthAccountRepository CreateRepository(MeepleAiDbContext dbContext)
        => new OAuthAccountRepository(dbContext, MockEventCollector.Object, NullLogger<OAuthAccountRepository>.Instance);
    [Fact]
    public async Task GetByUserIdAndProviderAsync_ExistingAccount_ReturnsAccount()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "google", "google_user_123");
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await Repository.GetByUserIdAndProviderAsync(userId, "google", TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(account.Id);
        result.Provider.Should().Be("google");
        result.ProviderUserId.Should().Be("google_user_123");
    }

    [Fact]
    public async Task GetByUserIdAndProviderAsync_CaseInsensitiveProvider_ReturnsAccount()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "github", "github_user_456");
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Search with uppercase
        var result = await Repository.GetByUserIdAndProviderAsync(userId, "GITHUB", TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(account.Id);
    }

    [Fact]
    public async Task GetByUserIdAndProviderAsync_NonExisting_ReturnsNull()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();

        // Act
        var result = await Repository.GetByUserIdAndProviderAsync(userId, "google", TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }
    [Fact]
    public async Task GetByProviderUserIdAsync_ExistingAccount_ReturnsAccount()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "discord", "discord_user_789");
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await Repository.GetByProviderUserIdAsync("discord", "discord_user_789");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(account.Id);
        result.UserId.Should().Be(userId);
        result.ProviderUserId.Should().Be("discord_user_789");
    }

    [Fact]
    public async Task GetByProviderUserIdAsync_ForOAuthLogin_FindsCorrectUser()
    {
        // Arrange - Simulate OAuth login scenario
        await ResetDatabaseAsync();
        var user1Id = await CreateTestUserAsync();
        var user2Id = await CreateTestUserAsync();

        var account1 = CreateTestOAuthAccount(user1Id, "google", "google_12345");
        var account2 = CreateTestOAuthAccount(user2Id, "google", "google_67890");

        await Repository.AddAsync(account1, TestCancellationToken);
        await Repository.AddAsync(account2, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act - OAuth provider returns google_12345
        var result = await Repository.GetByProviderUserIdAsync("google", "google_12345");

        // Assert - Should find user1
        result.Should().NotBeNull();
        result.UserId.Should().Be(user1Id);
    }

    [Fact]
    public async Task GetByProviderUserIdAsync_NonExisting_ReturnsNull()
    {
        // Arrange
        await ResetDatabaseAsync();

        // Act
        var result = await Repository.GetByProviderUserIdAsync("google", "nonexistent_id");

        // Assert
        result.Should().BeNull();
    }
    [Fact]
    public async Task GetByUserIdAsync_NoAccounts_ReturnsEmptyList()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = Guid.NewGuid();

        // Act
        var accounts = await Repository.GetByUserIdAsync(userId, TestCancellationToken);

        // Assert
        accounts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserIdAsync_MultipleProviders_ReturnsAllOrdered()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();

        var googleAccount = CreateTestOAuthAccount(userId, "google", "google_user");
        var githubAccount = CreateTestOAuthAccount(userId, "github", "github_user");
        var discordAccount = CreateTestOAuthAccount(userId, "discord", "discord_user");

        await Repository.AddAsync(googleAccount, TestCancellationToken);
        await Repository.AddAsync(githubAccount, TestCancellationToken);
        await Repository.AddAsync(discordAccount, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var accounts = await Repository.GetByUserIdAsync(userId, TestCancellationToken);

        // Assert
        accounts.Count.Should().Be(3);
        // Should be ordered by Provider alphabetically
        accounts[0].Provider.Should().Be("discord");
        accounts[1].Provider.Should().Be("github");
        accounts[2].Provider.Should().Be("google");
    }

    [Fact]
    public async Task GetByUserIdAsync_MultipleUsers_FiltersCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var user1Id = await CreateTestUserAsync();
        var user2Id = await CreateTestUserAsync();

        var user1Google = CreateTestOAuthAccount(user1Id, "google", "user1_google");
        var user1Github = CreateTestOAuthAccount(user1Id, "github", "user1_github");
        var user2Google = CreateTestOAuthAccount(user2Id, "google", "user2_google");

        await Repository.AddAsync(user1Google, TestCancellationToken);
        await Repository.AddAsync(user1Github, TestCancellationToken);
        await Repository.AddAsync(user2Google, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var user1Accounts = await Repository.GetByUserIdAsync(user1Id, TestCancellationToken);
        var user2Accounts = await Repository.GetByUserIdAsync(user2Id, TestCancellationToken);

        // Assert
        user1Accounts.Count.Should().Be(2);
        user2Accounts.Should().ContainSingle();
    }
    [Fact]
    public async Task AddAsync_NewAccount_PersistsSuccessfully()
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
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted.UserId.Should().Be(userId);
        persisted.Provider.Should().Be("google");
        persisted.ProviderUserId.Should().Be("google_unique_id");
        persisted.AccessTokenEncrypted.Should().Be("encrypted_access_token_123");
        persisted.RefreshTokenEncrypted.Should().Be("encrypted_refresh_token_456");
        persisted.TokenExpiresAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AddAsync_WithoutRefreshToken_PersistsCorrectly()
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
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted.RefreshTokenEncrypted.Should().BeNull();
    }
    [Fact]
    public async Task UpdateAsync_RefreshTokens_UpdatesCorrectly()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "google", "google_refresh_test");
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act - Simulate token refresh
        var trackedAccount = await Repository.GetByUserIdAndProviderAsync(userId, "google", TestCancellationToken);
        trackedAccount.Should().NotBeNull();
        trackedAccount.UpdateTokens(
            "new_access_token_encrypted",
            "new_refresh_token_encrypted",
            DateTime.UtcNow.AddHours(2)
        );
        DbContext.ChangeTracker.Clear(); // Clear any tracked entities before update
        await Repository.UpdateAsync(trackedAccount, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await Repository.GetByUserIdAndProviderAsync(userId, "google", TestCancellationToken);
        updated.Should().NotBeNull();
        updated.AccessTokenEncrypted.Should().Be("new_access_token_encrypted");
        updated.RefreshTokenEncrypted.Should().Be("new_refresh_token_encrypted");
        updated.TokenExpiresAt.Should().NotBeNull();
        (updated.UpdatedAt > updated.CreatedAt).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateAsync_AccessTokenOnly_UpdatesWithoutRefresh()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "discord", "discord_update_test");
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var trackedAccount = await Repository.GetByUserIdAndProviderAsync(userId, "discord", TestCancellationToken);
        trackedAccount.Should().NotBeNull();
        trackedAccount.UpdateTokens("new_access_token_only", null, DateTime.UtcNow.AddHours(1));
        DbContext.ChangeTracker.Clear(); // Clear any tracked entities before update
        await Repository.UpdateAsync(trackedAccount, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var updated = await Repository.GetByUserIdAndProviderAsync(userId, "discord", TestCancellationToken);
        updated.Should().NotBeNull();
        updated.AccessTokenEncrypted.Should().Be("new_access_token_only");
    }
    [Fact]
    public async Task DeleteAsync_ExistingAccount_RemovesFromDatabase()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "google", "delete_test");
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        await Repository.DeleteAsync(account);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var deleted = await DbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id, TestCancellationToken);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingAccount_DoesNotThrow()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "google", "nonexistent");

        // Act & Assert - Should not throw
        await Repository.DeleteAsync(account);
        await DbContext.SaveChangesAsync(TestCancellationToken);
    }
    [Fact]
    public async Task Mapping_DomainToPersistence_AllFieldsCorrect()
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
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await DbContext.OAuthAccounts.FirstOrDefaultAsync(oa => oa.Id == account.Id, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted.Id.Should().Be(account.Id);
        persisted.UserId.Should().Be(account.UserId);
        persisted.Provider.Should().Be(account.Provider);
        persisted.ProviderUserId.Should().Be(account.ProviderUserId);
        persisted.AccessTokenEncrypted.Should().Be(account.AccessTokenEncrypted);
        persisted.RefreshTokenEncrypted.Should().Be(account.RefreshTokenEncrypted);
        persisted.TokenExpiresAt.Should().Be(account.TokenExpiresAt);
    }

    [Fact]
    public async Task Mapping_PersistenceToDomain_AllFieldsCorrect()
    {
        // Arrange
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();
        var account = CreateTestOAuthAccount(userId, "discord", "roundtrip_test");
        await Repository.AddAsync(account, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await Repository.GetByUserIdAndProviderAsync(userId, "discord", TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Id.Should().Be(account.Id);
        retrieved.UserId.Should().Be(account.UserId);
        retrieved.Provider.Should().Be(account.Provider);
        retrieved.ProviderUserId.Should().Be(account.ProviderUserId);
        retrieved.AccessTokenEncrypted.Should().Be(account.AccessTokenEncrypted);
    }
    [Fact]
    public async Task MultiProvider_UserWithThreeProviders_AllPersistCorrectly()
    {
        // Arrange - User links Google, GitHub, and Discord
        await ResetDatabaseAsync();
        var userId = await CreateTestUserAsync();

        var googleAccount = CreateTestOAuthAccount(userId, "google", "multi_google");
        var githubAccount = CreateTestOAuthAccount(userId, "github", "multi_github");
        var discordAccount = CreateTestOAuthAccount(userId, "discord", "multi_discord");

        // Act
        await Repository.AddAsync(googleAccount, TestCancellationToken);
        await Repository.AddAsync(githubAccount, TestCancellationToken);
        await Repository.AddAsync(discordAccount, TestCancellationToken);
        await DbContext.SaveChangesAsync(TestCancellationToken);

        // Assert
        var allAccounts = await Repository.GetByUserIdAsync(userId, TestCancellationToken);
        allAccounts.Count.Should().Be(3);

        var google = await Repository.GetByUserIdAndProviderAsync(userId, "google", TestCancellationToken);
        var github = await Repository.GetByUserIdAndProviderAsync(userId, "github", TestCancellationToken);
        var discord = await Repository.GetByUserIdAndProviderAsync(userId, "discord", TestCancellationToken);

        google.Should().NotBeNull();
        github.Should().NotBeNull();
        discord.Should().NotBeNull();
    }
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
        await DbContext.SaveChangesAsync(TestCancellationToken);

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
}