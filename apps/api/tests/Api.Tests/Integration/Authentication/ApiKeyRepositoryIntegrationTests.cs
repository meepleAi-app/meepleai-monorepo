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
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for ApiKeyRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, API key lifecycle, verification, and revocation.
/// Issue #2307: Week 3 - Repository integration testing
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2307")]
public sealed class ApiKeyRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IApiKeyRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestApiKeyId1 = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TestApiKeyId2 = new("30000000-0000-0000-0000-000000000002");
    private static readonly Guid TestApiKeyId3 = new("30000000-0000-0000-0000-000000000003");
    private static readonly Guid TestUserId1 = new("10000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId2 = new("10000000-0000-0000-0000-000000000002");

    public ApiKeyRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_apikeyrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // Mock IApiKeyUsageLogRepository (required by ApiKeyUsedEventHandler)
        var mockUsageLogRepo = new Mock<IApiKeyUsageLogRepository>();
        services.AddSingleton(mockUsageLogRepo.Object);

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IApiKeyRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.EnsureCreatedAsync(TestCancellationToken);
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

    private static (ApiKey apiKey, string plaintextKey) CreateTestApiKey(
        Guid apiKeyId,
        Guid userId,
        string keyName = "Test Key",
        string scopes = "read,write",
        DateTime? expiresAt = null,
        string? metadata = null)
    {
        return ApiKey.Create(
            id: apiKeyId,
            userId: userId,
            keyName: keyName,
            scopes: scopes,
            expiresAt: expiresAt,
            metadata: metadata
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

        _dbContext.ApiKeys.RemoveRange(_dbContext.ApiKeys);
        _dbContext.Users.RemoveRange(_dbContext.Users);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    /// <summary>
    /// Execute action in isolated scope with fresh DbContext to avoid tracking conflicts
    /// </summary>
    private async Task<T> ExecuteInScopeAsync<T>(Func<IApiKeyRepository, IUnitOfWork, Task<T>> action)
    {
        using var scope = _serviceProvider!.CreateScope();
        var scopedRepo = scope.ServiceProvider.GetRequiredService<IApiKeyRepository>();
        var scopedUow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        return await action(scopedRepo, scopedUow);
    }

    #endregion

    #region GetByKeyPrefixAsync Tests

    [Fact]
    public async Task GetByKeyPrefixAsync_ExistingKey_ShouldReturnApiKey()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (apiKey, plaintextKey) = CreateTestApiKey(TestApiKeyId1, TestUserId1);
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(TestApiKeyId1);
        result.KeyPrefix.Should().Be(apiKey.KeyPrefix);
    }

    [Fact]
    public async Task GetByKeyPrefixAsync_NonExistingPrefix_ShouldReturnNull()
    {
        // Arrange
        await CleanDatabaseAsync();
        var nonExistingPrefix = "NOEXIST1";

        // Act
        var result = await _repository!.GetByKeyPrefixAsync(nonExistingPrefix, TestCancellationToken);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task GetByUserIdAsync_MultipleKeys_ShouldReturnAllOrderedByCreatedAtDesc()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (key1, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1, "Key 1");
        await Task.Delay(TimeSpan.FromMilliseconds(10), TestCancellationToken); // Ensure different CreatedAt
        var (key2, _) = CreateTestApiKey(TestApiKeyId2, TestUserId1, "Key 2");
        await Task.Delay(TimeSpan.FromMilliseconds(10), TestCancellationToken);
        var (key3, _) = CreateTestApiKey(TestApiKeyId3, TestUserId1, "Key 3");

        await _repository!.AddAsync(key1, TestCancellationToken);
        await _repository.AddAsync(key2, TestCancellationToken);
        await _repository.AddAsync(key3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().BeInDescendingOrder(k => k.CreatedAt);
    }

    [Fact]
    public async Task GetByUserIdAsync_NoKeys_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetByUserIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetActiveKeysByUserIdAsync Tests

    [Fact]
    public async Task GetActiveKeysByUserIdAsync_OnlyActiveKeys_ShouldReturnAll()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (key1, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1, "Active Key 1");
        var (key2, _) = CreateTestApiKey(TestApiKeyId2, TestUserId1, "Active Key 2");

        await _repository!.AddAsync(key1, TestCancellationToken);
        await _repository.AddAsync(key2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetActiveKeysByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().OnlyContain(k => k.IsValidKey());
    }

    [Fact]
    public async Task GetActiveKeysByUserIdAsync_MixedKeys_ShouldReturnOnlyActive()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (activeKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1, "Active Key");
        var (expiredKey, _) = CreateTestApiKey(TestApiKeyId2, TestUserId1, "Expired Key", expiresAt: DateTime.UtcNow.AddDays(-1));

        await _repository!.AddAsync(activeKey, TestCancellationToken);
        await _repository.AddAsync(expiredKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Revoke one key
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var key = await repo.GetByKeyPrefixAsync(activeKey.KeyPrefix, TestCancellationToken);
            key!.Revoke(TestUserId1, "Test revocation");
            await repo.UpdateAsync(key, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Act
        var result = await _repository!.GetActiveKeysByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().BeEmpty(); // Both keys are now inactive
    }

    [Fact]
    public async Task GetActiveKeysByUserIdAsync_NoActiveKeys_ShouldReturnEmptyList()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var result = await _repository!.GetActiveKeysByUserIdAsync(Guid.NewGuid(), TestCancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region GetAllAsync Tests

    [Fact]
    public async Task GetAllAsync_MultipleKeys_ShouldReturnAll()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await EnsureUserExistsAsync(TestUserId2);
        var (key1, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1);
        var (key2, _) = CreateTestApiKey(TestApiKeyId2, TestUserId2);

        await _repository!.AddAsync(key1, TestCancellationToken);
        await _repository.AddAsync(key2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(k => k.UserId == TestUserId1);
        result.Should().Contain(k => k.UserId == TestUserId2);
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
    public async Task AddAsync_NewApiKey_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (apiKey, plaintextKey) = CreateTestApiKey(TestApiKeyId1, TestUserId1, "Production Key", "read,write,delete");

        // Act
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(TestUserId1);
        persisted.KeyName.Should().Be("Production Key");
        persisted.Scopes.Should().Be("read,write,delete");
    }

    [Fact]
    public async Task AddAsync_ApiKeyWithExpiration_ShouldPersistExpiration()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var expiresAt = DateTime.UtcNow.AddDays(90);
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1, expiresAt: expiresAt);

        // Act
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.ExpiresAt.Should().NotBeNull();
        persisted.ExpiresAt!.Value.Should().BeCloseTo(expiresAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AddAsync_NullApiKey_ShouldThrowArgumentNullException()
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
    public async Task UpdateAsync_MarkAsUsed_ShouldUpdateLastUsedAt()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1);
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Update in isolated scope
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedKey = await repo.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
            loadedKey!.MarkAsUsed();
            await repo.UpdateAsync(loadedKey, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
        updated!.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_RevokeApiKey_ShouldPersistRevocation()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1);
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Revoke in isolated scope
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedKey = await repo.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
            loadedKey!.Revoke(TestUserId1, "Security breach");
            await repo.UpdateAsync(loadedKey, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
        updated!.RevokedAt.Should().NotBeNull();
        updated.RevokedBy.Should().Be(TestUserId1);
        updated.IsActive.Should().BeFalse();
        updated.IsValidKey().Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_RecordUsage_ShouldIncrementUsageCount()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1);
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Record usage in isolated scope
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedKey = await repo.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
            loadedKey!.RecordUsage("/api/games", "192.168.1.1", "TestAgent/1.0");
            await repo.UpdateAsync(loadedKey, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        var updated = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
        updated!.UsageCount.Should().Be(1);
        updated.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_NullApiKey_ShouldThrowArgumentNullException()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Act
        var act = async () => await _repository!.UpdateAsync(null!, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Complex Scenario Tests

    [Fact]
    public async Task ComplexScenario_ApiKeyLifecycle_ShouldMaintainConsistency()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);

        // 1. Create API key
        var (apiKey, plaintextKey) = CreateTestApiKey(TestApiKeyId1, TestUserId1, "Test Key", "read,write");
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // 2. Record usage
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedKey = await repo.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
            loadedKey!.RecordUsage("/api/games", "192.168.1.1");
            await repo.UpdateAsync(loadedKey, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // 3. Verify key is active
        var activeKeys = await _repository.GetActiveKeysByUserIdAsync(TestUserId1, TestCancellationToken);
        activeKeys.Should().HaveCount(1);

        // 4. Revoke key
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedKey = await repo.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
            loadedKey!.Revoke(TestUserId1, "No longer needed");
            await repo.UpdateAsync(loadedKey, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // 5. Verify key is no longer active
        var activeAfterRevoke = await _repository.GetActiveKeysByUserIdAsync(TestUserId1, TestCancellationToken);
        activeAfterRevoke.Should().BeEmpty();

        // Assert final state
        var final = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
        final.Should().NotBeNull();
        final!.IsValidKey().Should().BeFalse();
        final.UsageCount.Should().Be(1);
        final.LastUsedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ComplexScenario_MultipleUsersAndKeys_ShouldIsolateCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        await EnsureUserExistsAsync(TestUserId2);
        var (user1Key1, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1, "User1 Key1");
        var (user1Key2, _) = CreateTestApiKey(TestApiKeyId2, TestUserId1, "User1 Key2");
        var (user2Key1, _) = CreateTestApiKey(TestApiKeyId3, TestUserId2, "User2 Key1");

        await _repository!.AddAsync(user1Key1, TestCancellationToken);
        await _repository.AddAsync(user1Key2, TestCancellationToken);
        await _repository.AddAsync(user2Key1, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act - Get keys for each user
        var user1Keys = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);
        var user2Keys = await _repository.GetByUserIdAsync(TestUserId2, TestCancellationToken);

        // Assert
        user1Keys.Should().HaveCount(2);
        user1Keys.Should().OnlyContain(k => k.UserId == TestUserId1);

        user2Keys.Should().HaveCount(1);
        user2Keys[0].UserId.Should().Be(TestUserId2);
    }

    [Fact]
    public async Task ComplexScenario_ExpiredKey_ShouldNotAppearInActive()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (expiredKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1, "Expired", expiresAt: DateTime.UtcNow.AddDays(-1));
        var (activeKey, _) = CreateTestApiKey(TestApiKeyId2, TestUserId1, "Active", expiresAt: DateTime.UtcNow.AddDays(30));

        await _repository!.AddAsync(expiredKey, TestCancellationToken);
        await _repository.AddAsync(activeKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var activeKeys = await _repository.GetActiveKeysByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        activeKeys.Should().HaveCount(1);
        activeKeys[0].Id.Should().Be(TestApiKeyId2);
    }

    [Fact]
    public async Task ComplexScenario_KeyVerification_ShouldWorkCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (apiKey, plaintextKey) = CreateTestApiKey(TestApiKeyId1, TestUserId1);
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.VerifyKey(plaintextKey).Should().BeTrue();
        retrieved.VerifyKey("wrong_key").Should().BeFalse();
    }

    [Fact]
    public async Task ComplexScenario_ScopeChecking_ShouldWorkCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1, scopes: "read,write");
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var retrieved = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);

        // Assert
        retrieved.Should().NotBeNull();
        retrieved!.HasScope("read").Should().BeTrue();
        retrieved.HasScope("write").Should().BeTrue();
        retrieved.HasScope("delete").Should().BeFalse();
    }

    [Fact]
    public async Task ComplexScenario_RevokeDoubleAttempt_ShouldThrowDomainException()
    {
        // Arrange
        await CleanDatabaseAsync();
        await EnsureUserExistsAsync(TestUserId1);
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1);
        await _repository!.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Revoke once
        await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedKey = await repo.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
            loadedKey!.Revoke(TestUserId1);
            await repo.UpdateAsync(loadedKey, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Act - Attempt second revocation
        var act = async () => await ExecuteInScopeAsync(async (repo, uow) =>
        {
            var loadedKey = await repo.GetByKeyPrefixAsync(apiKey.KeyPrefix, TestCancellationToken);
            loadedKey!.Revoke(TestUserId1); // Should throw
            await repo.UpdateAsync(loadedKey, TestCancellationToken);
            await uow.SaveChangesAsync(TestCancellationToken);
            return true;
        });

        // Assert
        await act.Should().ThrowAsync<DomainException>()
            .WithMessage("*already revoked*");
    }

    #endregion
}
