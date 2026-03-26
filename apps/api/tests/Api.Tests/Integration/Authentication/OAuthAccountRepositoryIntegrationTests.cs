using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for OAuthAccountRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, OAuth account lifecycle, provider management, and token updates.
/// Issue #2307: Week 3 - Repository integration testing
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2307")]
public sealed class OAuthAccountRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IOAuthAccountRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestOAuthId1 = new("40000000-0000-0000-0000-000000000001");
    private static readonly Guid TestOAuthId2 = new("40000000-0000-0000-0000-000000000002");
    private static readonly Guid TestOAuthId3 = new("40000000-0000-0000-0000-000000000003");
    private static readonly Guid TestOAuthId4 = new("40000000-0000-0000-0000-000000000004");
    private static readonly Guid TestUserId1 = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId2 = new("10000000-0000-0000-0000-000000000002");

    public OAuthAccountRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_oauthrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);

        services.AddScoped<IOAuthAccountRepository, OAuthAccountRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IOAuthAccountRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    #region Helper Methods

    private static User CreateTestUser(
        Guid id,
        string email,
        string displayName = "Test User",
        Role? role = null,
        UserTier? tier = null)
    {
        var user = new User(
            id: id,
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: role ?? Role.User,
            tier: tier ?? UserTier.Free
        );
        return user;
    }

    private static OAuthAccount CreateTestOAuthAccount(
        Guid id,
        Guid userId,
        string provider = "google",
        string? providerUserId = null,
        string? accessToken = null,
        string? refreshToken = null,
        DateTime? tokenExpiresAt = null)
    {
        return new OAuthAccount(
            id: id,
            userId: userId,
            provider: provider,
            providerUserId: providerUserId ?? $"{provider}_user_{Guid.NewGuid():N}",
            accessTokenEncrypted: accessToken ?? $"encrypted_access_{Guid.NewGuid():N}",
            refreshTokenEncrypted: refreshToken,
            tokenExpiresAt: tokenExpiresAt
        );
    }

    private async Task EnsureUserExistsAsync(Guid userId, string? email = null)
    {
        using var scope = _serviceProvider!.CreateScope();
        var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var existingUser = await userRepo.GetByIdAsync(userId, TestCancellationToken);
        if (existingUser == null)
        {
            var user = CreateTestUser(userId, email ?? $"user{userId:N}@test.com");
            await userRepo.AddAsync(user, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
        }
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        _dbContext.OAuthAccounts.RemoveRange(_dbContext.OAuthAccounts);
        _dbContext.Users.RemoveRange(_dbContext.Users);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    /// <summary>
    /// Execute action in isolated scope with fresh DbContext to avoid tracking conflicts
    /// </summary>
    private async Task<T> ExecuteInScopeAsync<T>(Func<IOAuthAccountRepository, IUnitOfWork, Task<T>> action)
    {
        using var scope = _serviceProvider!.CreateScope();
        var scopedRepo = scope.ServiceProvider.GetRequiredService<IOAuthAccountRepository>();
        var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await action(scopedRepo, scopedUow);
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ExistingAccount_ShouldReturnOAuthAccount()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByIdAsync(TestOAuthId1, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestOAuthId1);
        result.UserId.Should().Be(TestUserId1);
        result.Provider.Should().Be("google");
    }

    [Fact]
    public async Task GetByIdAsync_NonExistingAccount_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository!.GetByIdAsync(nonExistingId, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByUserIdAndProviderAsync Tests

    [Fact]
    public async Task GetByUserIdAndProviderAsync_ExistingAccount_ShouldReturnOAuthAccount()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByUserIdAndProviderAsync(TestUserId1, "google", TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.UserId.Should().Be(TestUserId1);
        result.Provider.Should().Be("google");
    }

    [Fact]
    public async Task GetByUserIdAndProviderAsync_NonExistingAccount_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetByUserIdAndProviderAsync(Guid.NewGuid(), "github", TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserIdAndProviderAsync_CaseInsensitiveProvider_ShouldReturnAccount()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByUserIdAndProviderAsync(TestUserId1, "GOOGLE", TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Provider.Should().Be("google");
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task GetByUserIdAsync_MultipleProviders_ShouldReturnAllOrderedByProvider()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var googleAccount = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        var discordAccount = CreateTestOAuthAccount(TestOAuthId2, TestUserId1, "discord");
        var githubAccount = CreateTestOAuthAccount(TestOAuthId3, TestUserId1, "github");

        await _repository!.AddAsync(googleAccount, TestCancellationToken);
        await _repository.AddAsync(discordAccount, TestCancellationToken);
        await _repository.AddAsync(githubAccount, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInAscendingOrder(a => a.Provider);
        result.Should().Contain(a => a.Provider == "google");
        result.Should().Contain(a => a.Provider == "discord");
        result.Should().Contain(a => a.Provider == "github");
    }

    [Fact]
    public async Task GetByUserIdAsync_NoAccounts_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetByUserIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetByProviderUserIdAsync Tests

    [Fact]
    public async Task GetByProviderUserIdAsync_ExistingAccount_ShouldReturnOAuthAccount()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var providerUserId = "google_user_12345";
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google", providerUserId);
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByProviderUserIdAsync("google", providerUserId, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Provider.Should().Be("google");
        result.ProviderUserId.Should().Be(providerUserId);
    }

    [Fact]
    public async Task GetByProviderUserIdAsync_NonExistingAccount_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetByProviderUserIdAsync("google", "nonexistent_user", TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByProviderUserIdAsync_CaseInsensitiveProvider_ShouldReturnAccount()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var providerUserId = "discord_user_67890";
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "discord", providerUserId);
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByProviderUserIdAsync("DISCORD", providerUserId, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Provider.Should().Be("discord");
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_MultipleAccounts_ShouldReturnAll()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await EnsureUserExistsAsync(TestUserId2);
        var account1 = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        var account2 = CreateTestOAuthAccount(TestOAuthId2, TestUserId2, "github");

        await _repository!.AddAsync(account1, TestCancellationToken);
        await _repository.AddAsync(account2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(a => a.UserId == TestUserId1);
        result.Should().Contain(a => a.UserId == TestUserId2);
    }

    [Fact]
    public async Task GetAllAsync_EmptyDatabase_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task AddAsync_NewOAuthAccount_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google", "google_user_123");

        // Act
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestOAuthId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(TestUserId1);
        persisted.Provider.Should().Be("google");
        persisted.ProviderUserId.Should().Be("google_user_123");
    }

    [Fact]
    public async Task AddAsync_AccountWithTokenExpiration_ShouldPersistExpiration()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, tokenExpiresAt: expiresAt);

        // Act
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestOAuthId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.TokenExpiresAt.Should().NotBeNull();
        persisted.TokenExpiresAt!.Value.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AddAsync_NullOAuthAccount_ShouldThrowArgumentNullException()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.AddAsync(null!, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_UpdateTokens_ShouldPersistChanges()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        var newAccessToken = $"new_access_{Guid.NewGuid():N}";
        var newRefreshToken = $"new_refresh_{Guid.NewGuid():N}";
        var newExpiresAt = DateTime.UtcNow.AddHours(2);

        // Act - Update in isolated scope
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedAccount = await repo.GetByIdAsync(TestOAuthId1, TestCancellationToken);
            loadedAccount!.UpdateTokens(newAccessToken, newRefreshToken, newExpiresAt);
            await repo.UpdateAsync(loadedAccount, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated = await _repository.GetByIdAsync(TestOAuthId1, TestCancellationToken);
        updated!.AccessTokenEncrypted.Should().Be(newAccessToken);
        updated.RefreshTokenEncrypted.Should().Be(newRefreshToken);
        updated.TokenExpiresAt.Should().NotBeNull();
        updated.TokenExpiresAt!.Value.Should().BeCloseTo(newExpiresAt, TimeSpan.FromSeconds(1));
        updated.UpdatedAt.Should().BeAfter(updated.CreatedAt);
    }

    [Fact]
    public async Task UpdateAsync_NullOAuthAccount_ShouldThrowArgumentNullException()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.UpdateAsync(null!, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ExistingAccount_ShouldRemoveFromDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        await _repository.DeleteAsync(account, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert
        var deleted = await _repository.GetByIdAsync(TestOAuthId1, TestCancellationToken);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_NonExistingAccount_ShouldNotThrow()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(Guid.NewGuid(), TestUserId1, "google");

        // Act
        var act = async () =>
        {
            await _repository!.DeleteAsync(account, TestCancellationToken);
            await _unitOfWork!.SaveChangesAsync(TestCancellationToken);
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DeleteAsync_NullOAuthAccount_ShouldThrowArgumentNullException()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.DeleteAsync(null!, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region ExistsAsync Tests

    [Fact]
    public async Task ExistsAsync_ExistingAccount_ShouldReturnTrue()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.ExistsAsync(TestOAuthId1, TestCancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_NonExistingAccount_ShouldReturnFalse()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingId = Guid.NewGuid();

        // Act
        var result = await _repository!.ExistsAsync(nonExistingId, TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ExistsByUserIdAndProviderAsync Tests

    [Fact]
    public async Task ExistsByUserIdAndProviderAsync_ExistingAccount_ShouldReturnTrue()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.ExistsByUserIdAndProviderAsync(TestUserId1, "google", TestCancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsByUserIdAndProviderAsync_NonExistingAccount_ShouldReturnFalse()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.ExistsByUserIdAndProviderAsync(Guid.NewGuid(), "github", TestCancellationToken);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsByUserIdAndProviderAsync_CaseInsensitiveProvider_ShouldReturnTrue()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "discord");
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.ExistsByUserIdAndProviderAsync(TestUserId1, "DISCORD", TestCancellationToken);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task ComplexScenario_OAuthAccountLifecycle_ShouldMaintainConsistency()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);

        // 1. Create OAuth account
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google", "google_user_123");
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // 2. Verify account exists
        var exists = await _repository.ExistsAsync(TestOAuthId1, TestCancellationToken);
        exists.Should().BeTrue();

        // 3. Update tokens
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedAccount = await repo.GetByIdAsync(TestOAuthId1, TestCancellationToken);
            loadedAccount!.UpdateTokens($"new_access_{Guid.NewGuid():N}", $"new_refresh_{Guid.NewGuid():N}");
            await repo.UpdateAsync(loadedAccount, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // 4. Delete account
        var accountToDelete = await _repository.GetByIdAsync(TestOAuthId1, TestCancellationToken);
        await _repository.DeleteAsync(accountToDelete!, TestCancellationToken);
        await _unitOfWork.SaveChangesAsync(TestCancellationToken);

        // Assert final state
        var deleted = await _repository.GetByIdAsync(TestOAuthId1, TestCancellationToken);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task ComplexScenario_MultipleProvidersPerUser_ShouldIsolateCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var googleAccount = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        var githubAccount = CreateTestOAuthAccount(TestOAuthId2, TestUserId1, "github");
        var discordAccount = CreateTestOAuthAccount(TestOAuthId3, TestUserId1, "discord");

        await _repository!.AddAsync(googleAccount, TestCancellationToken);
        await _repository.AddAsync(githubAccount, TestCancellationToken);
        await _repository.AddAsync(discordAccount, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var allAccounts = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);
        var googleOnly = await _repository.GetByUserIdAndProviderAsync(TestUserId1, "google", TestCancellationToken);

        // Assert
        allAccounts.Should().HaveCount(3);
        allAccounts.Should().OnlyContain(a => a.UserId == TestUserId1);

        googleOnly.Should().NotBeNull();
        googleOnly!.Provider.Should().Be("google");
    }

    [Fact]
    public async Task ComplexScenario_MultipleUsersAndProviders_ShouldIsolateCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await EnsureUserExistsAsync(TestUserId2);
        var user1Google = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google");
        var user1Discord = CreateTestOAuthAccount(TestOAuthId2, TestUserId1, "discord");
        var user2Google = CreateTestOAuthAccount(TestOAuthId3, TestUserId2, "google");
        var user2Github = CreateTestOAuthAccount(TestOAuthId4, TestUserId2, "github");

        await _repository!.AddAsync(user1Google, TestCancellationToken);
        await _repository.AddAsync(user1Discord, TestCancellationToken);
        await _repository.AddAsync(user2Google, TestCancellationToken);
        await _repository.AddAsync(user2Github, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var user1Accounts = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);
        var user2Accounts = await _repository.GetByUserIdAsync(TestUserId2, TestCancellationToken);

        // Assert
        user1Accounts.Should().HaveCount(2);
        user1Accounts.Should().OnlyContain(a => a.UserId == TestUserId1);

        user2Accounts.Should().HaveCount(2);
        user2Accounts.Should().OnlyContain(a => a.UserId == TestUserId2);
    }

    [Fact]
    public async Task ComplexScenario_TokenExpiration_ShouldBeDetectable()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var expiredAccount = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google", tokenExpiresAt: DateTime.UtcNow.AddHours(-1));
        var activeAccount = CreateTestOAuthAccount(TestOAuthId2, TestUserId1, "github", tokenExpiresAt: DateTime.UtcNow.AddHours(1));

        await _repository!.AddAsync(expiredAccount, TestCancellationToken);
        await _repository.AddAsync(activeAccount, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var expired = await _repository.GetByIdAsync(TestOAuthId1, TestCancellationToken);
        var active = await _repository.GetByIdAsync(TestOAuthId2, TestCancellationToken);

        // Assert
        expired!.IsTokenExpired().Should().BeTrue();
        active!.IsTokenExpired().Should().BeFalse();
    }

    [Fact]
    public async Task ComplexScenario_RefreshSupport_ShouldBeDetectable()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var withRefresh = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google", refreshToken: "encrypted_refresh_token");
        var withoutRefresh = CreateTestOAuthAccount(TestOAuthId2, TestUserId1, "github", refreshToken: null);

        await _repository!.AddAsync(withRefresh, TestCancellationToken);
        await _repository.AddAsync(withoutRefresh, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var accountWithRefresh = await _repository.GetByIdAsync(TestOAuthId1, TestCancellationToken);
        var accountWithoutRefresh = await _repository.GetByIdAsync(TestOAuthId2, TestCancellationToken);

        // Assert
        accountWithRefresh!.SupportsRefresh().Should().BeTrue();
        accountWithoutRefresh!.SupportsRefresh().Should().BeFalse();
    }

    [Fact]
    public async Task ComplexScenario_ProviderUserIdLookup_ShouldWorkAcrossDifferentUsers()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var providerUserId = "google_user_unique_123";
        var account = CreateTestOAuthAccount(TestOAuthId1, TestUserId1, "google", providerUserId);
        await _repository!.AddAsync(account, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var found = await _repository.GetByProviderUserIdAsync("google", providerUserId, TestCancellationToken);

        // Assert
        found.Should().NotBeNull();
        found!.UserId.Should().Be(TestUserId1);
        found.ProviderUserId.Should().Be(providerUserId);
    }

    #endregion
}
