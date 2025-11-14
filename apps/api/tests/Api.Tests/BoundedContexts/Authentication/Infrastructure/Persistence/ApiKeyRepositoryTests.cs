using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Integration tests for ApiKeyRepository using Testcontainers with real PostgreSQL.
/// Tests API key management, scoping, expiration, and revocation logic.
/// </summary>
[Collection("Integration")]
public class ApiKeyRepositoryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private ApiKeyRepository? _repository;

    public async ValueTask InitializeAsync()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("meepleai_apikey_test")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .Build();

        await _postgresContainer.StartAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        await _dbContext.Database.MigrateAsync();

        _repository = new ApiKeyRepository(_dbContext);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    #region GetByKeyPrefixAsync Tests

    [Fact]
    public async Task Test01_GetByKeyPrefixAsync_ExistingKey_ReturnsApiKey()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(
            id: Guid.NewGuid(),
            userId: userId,
            keyName: "Test Key",
            scopes: "read,write"
        );

        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Act
        var result = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(apiKey.Id, result.Id);
        Assert.Equal(apiKey.KeyPrefix, result.KeyPrefix);
    }

    [Fact]
    public async Task Test02_GetByKeyPrefixAsync_NonExistingPrefix_ReturnsNull()
    {
        // Arrange
        var nonExistentPrefix = "NONEXIST";

        // Act
        var result = await _repository!.GetByKeyPrefixAsync(nonExistentPrefix);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Fact]
    public async Task Test03_GetByUserIdAsync_NoKeys_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var keys = await _repository!.GetByUserIdAsync(userId);

        // Assert
        Assert.Empty(keys);
    }

    [Fact]
    public async Task Test04_GetByUserIdAsync_MultipleKeys_ReturnsAllOrdered()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var (key1, _) = ApiKey.Create(Guid.NewGuid(), userId, "Key 1", "read");
        await Task.Delay(10); // Ensure different timestamps
        var (key2, _) = ApiKey.Create(Guid.NewGuid(), userId, "Key 2", "write");
        await Task.Delay(10);
        var (key3, _) = ApiKey.Create(Guid.NewGuid(), userId, "Key 3", "admin");

        await _repository!.AddAsync(key1);
        await _repository.AddAsync(key2);
        await _repository.AddAsync(key3);
        await _dbContext!.SaveChangesAsync();

        // Act
        var keys = await _repository.GetByUserIdAsync(userId);

        // Assert
        Assert.Equal(3, keys.Count);
        // Should be ordered by CreatedAt descending
        Assert.True(keys[0].CreatedAt >= keys[1].CreatedAt);
        Assert.True(keys[1].CreatedAt >= keys[2].CreatedAt);
    }

    [Fact]
    public async Task Test05_GetByUserIdAsync_MultipleUsers_FiltersCorrectly()
    {
        // Arrange
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();

        var (key1, _) = ApiKey.Create(Guid.NewGuid(), user1Id, "User1 Key1", "read");
        var (key2, _) = ApiKey.Create(Guid.NewGuid(), user1Id, "User1 Key2", "write");
        var (key3, _) = ApiKey.Create(Guid.NewGuid(), user2Id, "User2 Key1", "admin");

        await _repository!.AddAsync(key1);
        await _repository.AddAsync(key2);
        await _repository.AddAsync(key3);
        await _dbContext!.SaveChangesAsync();

        // Act
        var user1Keys = await _repository.GetByUserIdAsync(user1Id);
        var user2Keys = await _repository.GetByUserIdAsync(user2Id);

        // Assert
        Assert.Equal(2, user1Keys.Count);
        Assert.Single(user2Keys);
    }

    #endregion

    #region GetActiveKeysByUserIdAsync Tests

    [Fact]
    public async Task Test06_GetActiveKeysByUserIdAsync_OnlyActiveKeys_ReturnsAll()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var (key1, _) = ApiKey.Create(Guid.NewGuid(), userId, "Active Key 1", "read");
        var (key2, _) = ApiKey.Create(Guid.NewGuid(), userId, "Active Key 2", "write");

        await _repository!.AddAsync(key1);
        await _repository.AddAsync(key2);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeKeys = await _repository.GetActiveKeysByUserIdAsync(userId);

        // Assert
        Assert.Equal(2, activeKeys.Count);
    }

    [Fact]
    public async Task Test07_GetActiveKeysByUserIdAsync_RevokedKeys_Excluded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var revokerId = Guid.NewGuid();

        var (activeKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Active Key", "read");
        var (revokedKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Revoked Key", "write");
        revokedKey.Revoke(revokerId);

        await _repository!.AddAsync(activeKey);
        await _repository.AddAsync(revokedKey);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeKeys = await _repository.GetActiveKeysByUserIdAsync(userId);

        // Assert
        Assert.Single(activeKeys);
        Assert.Equal(activeKey.Id, activeKeys[0].Id);
    }

    [Fact]
    public async Task Test08_GetActiveKeysByUserIdAsync_ExpiredKeys_Excluded()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var (activeKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Active Key", "read", expiresAt: DateTime.UtcNow.AddDays(30));
        var (expiredKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Expired Key", "write", expiresAt: DateTime.UtcNow.AddDays(-1));

        await _repository!.AddAsync(activeKey);
        await _repository.AddAsync(expiredKey);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeKeys = await _repository.GetActiveKeysByUserIdAsync(userId);

        // Assert
        Assert.Single(activeKeys);
        Assert.Equal(activeKey.Id, activeKeys[0].Id);
    }

    [Fact]
    public async Task Test09_GetActiveKeysByUserIdAsync_InactiveKeys_Excluded()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var revokerId = Guid.NewGuid();

        var (activeKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Active Key", "read");
        var (inactiveKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Inactive Key", "write");
        inactiveKey.Revoke(revokerId); // Makes IsActive = false

        await _repository!.AddAsync(activeKey);
        await _repository.AddAsync(inactiveKey);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeKeys = await _repository.GetActiveKeysByUserIdAsync(userId);

        // Assert
        Assert.Single(activeKeys);
        Assert.Equal(activeKey.Id, activeKeys[0].Id);
    }

    [Fact]
    public async Task Test10_GetActiveKeysByUserIdAsync_MixedKeys_FiltersCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var revokerId = Guid.NewGuid();

        var (active1, _) = ApiKey.Create(Guid.NewGuid(), userId, "Active 1", "read");
        var (active2, _) = ApiKey.Create(Guid.NewGuid(), userId, "Active 2", "write");
        var (expired, _) = ApiKey.Create(Guid.NewGuid(), userId, "Expired", "admin", expiresAt: DateTime.UtcNow.AddDays(-1));
        var (revoked, _) = ApiKey.Create(Guid.NewGuid(), userId, "Revoked", "read");
        revoked.Revoke(revokerId);

        await _repository!.AddAsync(active1);
        await _repository.AddAsync(active2);
        await _repository.AddAsync(expired);
        await _repository.AddAsync(revoked);
        await _dbContext!.SaveChangesAsync();

        // Act
        var activeKeys = await _repository.GetActiveKeysByUserIdAsync(userId);

        // Assert
        Assert.Equal(2, activeKeys.Count);
        Assert.Contains(activeKeys, k => k.Id == active1.Id);
        Assert.Contains(activeKeys, k => k.Id == active2.Id);
    }

    #endregion

    #region AddAsync Tests

    [Fact]
    public async Task Test11_AddAsync_NewApiKey_PersistsSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, plaintextKey) = ApiKey.Create(
            id: Guid.NewGuid(),
            userId: userId,
            keyName: "Production Key",
            scopes: "read,write,admin",
            expiresAt: DateTime.UtcNow.AddYears(1),
            metadata: "{\"env\":\"production\"}"
        );

        // Act
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.NotNull(persisted);
        Assert.Equal(userId, persisted.UserId);
        Assert.Equal("Production Key", persisted.KeyName);
        Assert.Equal("read,write,admin", persisted.Scopes);
        Assert.NotNull(persisted.ExpiresAt);
        Assert.Equal("{\"env\":\"production\"}", persisted.Metadata);
        Assert.True(persisted.IsActive);
        Assert.NotNull(plaintextKey); // Should receive plaintext key once
    }

    [Fact]
    public async Task Test12_AddAsync_ApiKeyWithoutExpiration_PersistsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(
            id: Guid.NewGuid(),
            userId: userId,
            keyName: "No Expiration Key",
            scopes: "read"
        );

        // Act
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted.ExpiresAt);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task Test13_UpdateAsync_LastUsedAt_UpdatesCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Test Key", "read");
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Act
        apiKey.MarkAsUsed();
        await _repository.UpdateAsync(apiKey);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.LastUsedAt);
    }

    [Fact]
    public async Task Test14_UpdateAsync_RevokeApiKey_PersistsRevocation()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var revokerId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Test Key", "read");
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Act
        apiKey.Revoke(revokerId);
        await _repository.UpdateAsync(apiKey);
        await _dbContext.SaveChangesAsync();

        // Assert
        var updated = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.RevokedAt);
        Assert.Equal(revokerId, updated.RevokedBy);
        Assert.False(updated.IsActive);
    }

    #endregion

    #region Mapping Tests

    [Fact]
    public async Task Test15_Mapping_DomainToPersistence_AllFieldsCorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(
            id: Guid.NewGuid(),
            userId: userId,
            keyName: "Mapping Test Key",
            scopes: "read,write",
            expiresAt: DateTime.UtcNow.AddMonths(6),
            metadata: "{\"client\":\"mobile\"}"
        );

        // Act
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.NotNull(persisted);
        Assert.Equal(apiKey.Id, persisted.Id);
        Assert.Equal(apiKey.UserId, persisted.UserId);
        Assert.Equal(apiKey.KeyName, persisted.KeyName);
        Assert.Equal(apiKey.KeyHash, persisted.KeyHash);
        Assert.Equal(apiKey.KeyPrefix, persisted.KeyPrefix);
        Assert.Equal(apiKey.Scopes, persisted.Scopes);
        Assert.Equal(apiKey.Metadata, persisted.Metadata);
        Assert.Equal(apiKey.IsActive, persisted.IsActive);
    }

    [Fact]
    public async Task Test16_Mapping_PersistenceToDomain_AllFieldsCorrect()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Round Trip Test", "admin");
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Act
        var retrieved = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(apiKey.Id, retrieved.Id);
        Assert.Equal(apiKey.UserId, retrieved.UserId);
        Assert.Equal(apiKey.KeyName, retrieved.KeyName);
        Assert.Equal(apiKey.KeyPrefix, retrieved.KeyPrefix);
        Assert.Equal(apiKey.Scopes, retrieved.Scopes);
        Assert.Equal(apiKey.IsActive, retrieved.IsActive);
    }

    #endregion

    #region Scope Serialization Tests

    [Fact]
    public async Task Test17_ScopeSerialization_SingleScope_PersistsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Single Scope Key", "read");

        // Act
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.NotNull(persisted);
        Assert.Equal("read", persisted.Scopes);
    }

    [Fact]
    public async Task Test18_ScopeSerialization_MultipleScopes_PersistsCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Multi Scope Key", "read,write,admin");

        // Act
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.NotNull(persisted);
        Assert.Equal("read,write,admin", persisted.Scopes);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task Test19_ConcurrentPrefixLookups_NoConflicts()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Concurrent Key", "read");
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Act - Multiple concurrent prefix lookups
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.All(results, result =>
        {
            Assert.NotNull(result);
            Assert.Equal(apiKey.Id, result.Id);
        });
    }

    [Fact]
    public async Task Test20_ConcurrentUsageUpdates_LastOneWins()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "Usage Key", "read");
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Act - Multiple concurrent usage updates
        var tasks = Enumerable.Range(0, 5).Select(async _ =>
        {
            var key = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix);
            if (key != null)
            {
                key.MarkAsUsed();
                await _repository.UpdateAsync(key);
                await _dbContext.SaveChangesAsync();
            }
        }).ToArray();

        await Task.WhenAll(tasks);

        // Assert - Should have LastUsedAt updated
        var updated = await _dbContext!.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.NotNull(updated);
        Assert.NotNull(updated.LastUsedAt);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task Test21_NullableFields_Metadata_HandledCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "No Metadata Key", "read", metadata: null);

        // Act
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Assert
        var persisted = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.NotNull(persisted);
        Assert.Null(persisted.Metadata);
    }

    [Fact]
    public async Task Test22_AsNoTracking_QueriesDoNotTrackEntities()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(Guid.NewGuid(), userId, "No Tracking Key", "read");
        await _repository!.AddAsync(apiKey);
        await _dbContext!.SaveChangesAsync();

        // Act
        var retrieved = await _repository.GetByKeyPrefixAsync(apiKey.KeyPrefix);

        // Modify retrieved object
        retrieved!.MarkAsUsed();

        // SaveChanges without explicit Update call
        await _dbContext!.SaveChangesAsync();

        // Assert - Changes should NOT be persisted (AsNoTracking)
        var reloaded = await _dbContext.ApiKeys.FirstOrDefaultAsync(k => k.Id == apiKey.Id);
        Assert.Null(reloaded!.LastUsedAt); // Should remain null
    }

    #endregion
}
