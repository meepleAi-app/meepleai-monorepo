using System;
using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

public class ApiKeyAuthenticationServiceTests
{
    #region ValidateApiKeyAsync Tests

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsValidResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate API key
        var (plaintextKey, apiKeyEntity) = await service.GenerateApiKeyAsync(
            user.Id,
            "Test Key",
            new[] { "read", "write" },
            environment: "live");
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(apiKeyEntity.Id, result.ApiKeyId);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal(user.Email, result.UserEmail);
        Assert.Equal(user.DisplayName, result.UserDisplayName);
        Assert.Equal("User", result.UserRole);
        Assert.Equal(2, result.Scopes.Length);
        Assert.Contains("read", result.Scopes);
        Assert.Contains("write", result.Scopes);
        Assert.Null(result.InvalidReason);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithEmptyKey_ReturnsInvalidResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act
        var result = await service.ValidateApiKeyAsync("");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("API key is required", result.InvalidReason);
        Assert.Null(result.ApiKeyId);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithNullKey_ReturnsInvalidResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act
        var result = await service.ValidateApiKeyAsync(null!);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("API key is required", result.InvalidReason);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInvalidKey_ReturnsInvalidResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act
        var result = await service.ValidateApiKeyAsync("mpl_live_invalid_key_does_not_exist");

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid, expired, or revoked API key", result.InvalidReason);
        Assert.Null(result.ApiKeyId);
        Assert.Null(result.UserId);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInactiveKey_ReturnsInvalidResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate and deactivate API key
        var (plaintextKey, apiKeyEntity) = await service.GenerateApiKeyAsync(user.Id, "Test Key", new[] { "read" });
        apiKeyEntity.IsActive = false;
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid, expired, or revoked API key", result.InvalidReason);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithExpiredKey_ReturnsInvalidResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate API key with expiration in the past
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(-1);
        var (plaintextKey, apiKeyEntity) = await service.GenerateApiKeyAsync(
            user.Id,
            "Test Key",
            new[] { "read" },
            expiresAt: expiresAt);
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid, expired, or revoked API key", result.InvalidReason);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithRevokedKey_ReturnsInvalidResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate and revoke API key
        var (plaintextKey, apiKeyEntity) = await service.GenerateApiKeyAsync(user.Id, "Test Key", new[] { "read" });
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        await service.RevokeApiKeyAsync(apiKeyEntity.Id, user.Id);

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid, expired, or revoked API key", result.InvalidReason);
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKeyNearExpiration_ReturnsValidResult()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate API key expiring in 1 hour (still valid)
        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddHours(1);
        var (plaintextKey, apiKeyEntity) = await service.GenerateApiKeyAsync(
            user.Id,
            "Test Key",
            new[] { "read" },
            expiresAt: expiresAt);
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(user.Id, result.UserId);
    }

    #endregion

    #region GenerateApiKeyAsync Tests

    [Theory]
    [InlineData("live")]
    [InlineData("test")]
    public async Task GenerateApiKeyAsync_WithValidParameters_ReturnsKeyAndEntity(string environment)
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.Admin,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Act
        var (plaintextKey, entity) = await service.GenerateApiKeyAsync(
            user.Id,
            "Test Key",
            new[] { "read", "write" },
            expiresAt: null,
            environment: environment);

        // Assert
        Assert.NotNull(plaintextKey);
        Assert.StartsWith($"mpl_{environment}_", plaintextKey);
        Assert.True(plaintextKey.Length > 20);

        Assert.NotNull(entity);
        Assert.Equal(user.Id, entity.UserId);
        Assert.Equal("Test Key", entity.KeyName);
        Assert.NotNull(entity.KeyHash);
        Assert.StartsWith("mpl_", entity.KeyPrefix);
        Assert.Equal(2, entity.Scopes.Length);
        Assert.Contains("read", entity.Scopes);
        Assert.Contains("write", entity.Scopes);
        Assert.True(entity.IsActive);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, entity.CreatedAt);
        Assert.Null(entity.LastUsedAt);
        Assert.Null(entity.ExpiresAt);
        Assert.Null(entity.RevokedAt);
        Assert.Null(entity.RevokedBy);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithExpirationDate_SetsExpiresAt()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var expiresAt = timeProvider.GetUtcNow().UtcDateTime.AddDays(30);

        // Act
        var (plaintextKey, entity) = await service.GenerateApiKeyAsync(
            user.Id,
            "Temporary Key",
            new[] { "read" },
            expiresAt: expiresAt);

        // Assert
        Assert.Equal(expiresAt, entity.ExpiresAt);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateApiKeyAsync("", "Test Key", new[] { "read" }));
        Assert.Equal("User ID is required (Parameter 'userId')", exception.Message);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithEmptyKeyName_ThrowsArgumentException()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateApiKeyAsync("user-id", "", new[] { "read" }));
        Assert.Equal("Key name is required (Parameter 'keyName')", exception.Message);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithInvalidEnvironment_ThrowsArgumentException()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.GenerateApiKeyAsync("user-id", "Test Key", new[] { "read" }, environment: "production"));
        Assert.Equal("Environment must be 'live' or 'test' (Parameter 'environment')", exception.Message);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithNonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GenerateApiKeyAsync("non-existent-user-id", "Test Key", new[] { "read" }));
        Assert.StartsWith("User not found:", exception.Message);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_MultipleKeys_GeneratesDifferentKeys()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Act
        var (key1, entity1) = await service.GenerateApiKeyAsync(user.Id, "Key 1", new[] { "read" });
        var (key2, entity2) = await service.GenerateApiKeyAsync(user.Id, "Key 2", new[] { "write" });

        // Assert
        Assert.NotEqual(key1, key2);
        Assert.NotEqual(entity1.KeyHash, entity2.KeyHash);
        Assert.NotEqual(entity1.Id, entity2.Id);
    }

    #endregion

    #region RevokeApiKeyAsync Tests

    [Fact]
    public async Task RevokeApiKeyAsync_WithValidKey_RevokesSuccessfully()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate API key
        var (_, apiKeyEntity) = await service.GenerateApiKeyAsync(user.Id, "Test Key", new[] { "read" });
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.RevokeApiKeyAsync(apiKeyEntity.Id, user.Id);

        // Assert
        Assert.True(result);
        var revokedKey = await dbContext.ApiKeys.FindAsync(apiKeyEntity.Id);
        Assert.NotNull(revokedKey);
        Assert.Equal(timeProvider.GetUtcNow().UtcDateTime, revokedKey!.RevokedAt);
        Assert.Equal(user.Id, revokedKey.RevokedBy);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act
        var result = await service.RevokeApiKeyAsync("non-existent-key-id", "user-id");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithAlreadyRevokedKey_ReturnsTrue()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate and revoke API key
        var (_, apiKeyEntity) = await service.GenerateApiKeyAsync(user.Id, "Test Key", new[] { "read" });
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        await service.RevokeApiKeyAsync(apiKeyEntity.Id, user.Id);

        // Act - revoke again
        var result = await service.RevokeApiKeyAsync(apiKeyEntity.Id, user.Id);

        // Assert
        Assert.True(result);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullLifecycle_GenerateValidateRevoke_WorksCorrectly()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.Editor,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Act - Generate
        var (plaintextKey, apiKeyEntity) = await service.GenerateApiKeyAsync(
            user.Id,
            "Integration Test Key",
            new[] { "read", "write", "delete" },
            expiresAt: null,
            environment: "test");
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act - Validate (should succeed)
        var validationResult1 = await service.ValidateApiKeyAsync(plaintextKey);
        Assert.True(validationResult1.IsValid);
        Assert.Equal("Editor", validationResult1.UserRole);
        Assert.Equal(3, validationResult1.Scopes.Length);

        // Act - Revoke
        var revokeResult = await service.RevokeApiKeyAsync(apiKeyEntity.Id, user.Id);
        Assert.True(revokeResult);

        // Act - Validate (should fail)
        var validationResult2 = await service.ValidateApiKeyAsync(plaintextKey);
        Assert.False(validationResult2.IsValid);
        Assert.Equal("Invalid, expired, or revoked API key", validationResult2.InvalidReason);
    }

    [Fact]
    public async Task KeyHashing_SameKeyGeneratesSameHash()
    {
        // Arrange
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        await using (var setupContext = CreateContext(connection))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext(connection);
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var service = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate key
        var (plaintextKey, apiKeyEntity) = await service.GenerateApiKeyAsync(user.Id, "Test Key", new[] { "read" });
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act - Validate with same key multiple times
        var result1 = await service.ValidateApiKeyAsync(plaintextKey);
        var result2 = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert - Both validations should succeed
        Assert.True(result1.IsValid);
        Assert.True(result2.IsValid);
        Assert.Equal(result1.ApiKeyId, result2.ApiKeyId);
    }

    #endregion

    #region Helper Methods

    private static MeepleAiDbContext CreateContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        return new MeepleAiDbContext(options);
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public FixedTimeProvider(DateTimeOffset now)
        {
            _now = now;
        }

        public override DateTimeOffset GetUtcNow() => _now;

        public void SetTime(DateTimeOffset now) => _now = now;
    }

    #endregion
}
