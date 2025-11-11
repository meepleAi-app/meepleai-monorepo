using System;
using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;

public class ApiKeyAuthenticationServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;

    public ApiKeyAuthenticationServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    #region ValidateApiKeyAsync Tests

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKey_ReturnsValidResult()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate API key
        (string plaintextKey, var apiKeyEntity) = await service.GenerateApiKeyAsync(
            userId.ToString(),
            "Test Key",
            new[] { "read", "write" },
            environment: "live");
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        result.IsValid.Should().BeTrue();
        result.ApiKeyId.Should().BeEquivalentTo(apiKeyEntity.Id.ToString());
        result.UserId.Should().BeEquivalentTo(userId.ToString());
        result.UserEmail.Should().BeEquivalentTo(user.Email);
        result.UserDisplayName.Should().BeEquivalentTo(user.DisplayName);
        result.UserRole.Should().BeEquivalentTo("User");
        result.Scopes.Length.Should().Be(2);
        result.Scopes.Should().Contain("read");
        result.Scopes.Should().Contain("write");
        result.InvalidReason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithEmptyKey_ReturnsInvalidResult()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act
        var result = await service.ValidateApiKeyAsync("");

        // Assert
        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().BeEquivalentTo("API key is required");
        result.ApiKeyId.Should().BeNull();
        result.UserId.Should().BeNull();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithNullKey_ReturnsInvalidResult()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act
        var result = await service.ValidateApiKeyAsync(null!);

        // Assert
        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().BeEquivalentTo("API key is required");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInvalidKey_ReturnsInvalidResult()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act
        var result = await service.ValidateApiKeyAsync("mpl_live_invalid_key_does_not_exist");

        // Assert
        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().BeEquivalentTo("Invalid, expired, or revoked API key");
        result.ApiKeyId.Should().BeNull();
        result.UserId.Should().BeNull();
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithInactiveKey_ReturnsInvalidResult()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate and deactivate API key
        (string plaintextKey, var apiKeyEntity) = await service.GenerateApiKeyAsync(userId.ToString(), "Test Key", new[] { "read" });
        apiKeyEntity.IsActive = false;
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().BeEquivalentTo("Invalid, expired, or revoked API key");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithExpiredKey_ReturnsInvalidResult()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
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
        (string plaintextKey, var apiKeyEntity) = await service.GenerateApiKeyAsync(
            userId.ToString(),
            "Test Key",
            new[] { "read" },
            expiresAt: expiresAt);
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().BeEquivalentTo("Invalid, expired, or revoked API key");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithRevokedKey_ReturnsInvalidResult()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate and revoke API key
        (string plaintextKey, var apiKeyEntity) = await service.GenerateApiKeyAsync(userId.ToString(), "Test Key", new[] { "read" });
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        await service.RevokeApiKeyAsync(apiKeyEntity.Id.ToString(), userId.ToString());

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        result.IsValid.Should().BeFalse();
        result.InvalidReason.Should().BeEquivalentTo("Invalid, expired, or revoked API key");
    }

    [Fact]
    public async Task ValidateApiKeyAsync_WithValidKeyNearExpiration_ReturnsValidResult()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
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
        (string plaintextKey, var apiKeyEntity) = await service.GenerateApiKeyAsync(
            userId.ToString(),
            "Test Key",
            new[] { "read" },
            expiresAt: expiresAt);
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert
        result.IsValid.Should().BeTrue();
        result.UserId.Should().BeEquivalentTo(userId.ToString());
    }

    #endregion

    #region GenerateApiKeyAsync Tests

    [Theory]
    [InlineData("live")]
    [InlineData("test")]
    public async Task GenerateApiKeyAsync_WithValidParameters_ReturnsKeyAndEntity(string environment)
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
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
            userId.ToString(),
            "Test Key",
            new[] { "read", "write" },
            expiresAt: null,
            environment: environment);

        // Assert
        plaintextKey.Should().NotBeNull();
        plaintextKey.Should().StartWith($"mpl_{environment}_");
        (plaintextKey.Length > 20).Should().BeTrue();

        entity.Should().NotBeNull();
        entity.UserId.Should().Be(userId.ToString());
        entity.KeyName.Should().Be("Test Key");
        entity.KeyHash.Should().NotBeNull();
        entity.KeyPrefix.Should().StartWith("mpl_");
        entity.Scopes.Length.Should().Be(2);
        entity.Scopes.Should().Contain("read");
        entity.Scopes.Should().Contain("write");
        entity.IsActive.Should().BeTrue();
        entity.CreatedAt.Should().Be(timeProvider.GetUtcNow().UtcDateTime);
        entity.LastUsedAt.Should().BeNull();
        entity.ExpiresAt.Should().BeNull();
        entity.RevokedAt.Should().BeNull();
        entity.RevokedBy.Should().BeNull();
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithExpirationDate_SetsExpiresAt()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
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
            userId.ToString(),
            "Temporary Key",
            new[] { "read" },
            expiresAt: expiresAt);

        // Assert
        entity.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act & Assert
        var act = async () => await service.GenerateApiKeyAsync("", "Test Key", new[] { "read" });
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Be("User ID is required (Parameter 'userId')");
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithEmptyKeyName_ThrowsArgumentException()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act & Assert
        var act = async () => await service.GenerateApiKeyAsync("user-id", "", new[] { "read" });
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Be("Key name is required (Parameter 'keyName')");
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithInvalidEnvironment_ThrowsArgumentException()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act & Assert
        var act = async () => await service.GenerateApiKeyAsync("user-id", "Test Key", new[] { "read" }, environment: "production");
        var exception = await act.Should().ThrowAsync<ArgumentException>();
        exception.Which.Message.Should().Be("Environment must be 'live' or 'test' (Parameter 'environment')");
    }

    [Fact]
    public async Task GenerateApiKeyAsync_WithNonExistentUser_ThrowsInvalidOperationException()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act & Assert
        var act = async () => await service.GenerateApiKeyAsync("non-existent-user-id", "Test Key", new[] { "read" });
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Message.Should().StartWith("User not found:");
    }

    [Fact]
    public async Task GenerateApiKeyAsync_MultipleKeys_GeneratesDifferentKeys()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Act
        var (key1, entity1) = await service.GenerateApiKeyAsync(userId.ToString(), "Key 1", new[] { "read" });
        var (key2, entity2) = await service.GenerateApiKeyAsync(userId.ToString(), "Key 2", new[] { "write" });

        // Assert
        key2.Should().NotBe(key1);
        entity2.KeyHash.Should().NotBe(entity1.KeyHash);
        entity2.Id.Should().NotBe(entity1.Id);
    }

    #endregion

    #region RevokeApiKeyAsync Tests

    [Fact]
    public async Task RevokeApiKeyAsync_WithValidKey_RevokesSuccessfully()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate API key
        var (_, apiKeyEntity) = await service.GenerateApiKeyAsync(userId.ToString(), "Test Key", new[] { "read" });
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act
        var result = await service.RevokeApiKeyAsync(apiKeyEntity.Id.ToString(), userId.ToString());

        // Assert
        result.Should().BeTrue();
        var revokedKey = await dbContext.ApiKeys.FindAsync(apiKeyEntity.Id);
        revokedKey.Should().NotBeNull();
        revokedKey!.RevokedAt.Should().Be(timeProvider.GetUtcNow().UtcDateTime);
        revokedKey.RevokedBy.Should().Be(userId.ToString());
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithNonExistentKey_ReturnsFalse()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Act
        var result = await service.RevokeApiKeyAsync("non-existent-key-id", "user-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithAlreadyRevokedKey_ReturnsTrue()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate and revoke API key
        var (_, apiKeyEntity) = await service.GenerateApiKeyAsync(userId.ToString(), "Test Key", new[] { "read" });
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        await service.RevokeApiKeyAsync(apiKeyEntity.Id.ToString(), userId.ToString());

        // Act - revoke again
        var result = await service.RevokeApiKeyAsync(apiKeyEntity.Id.ToString(), userId.ToString());

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task FullLifecycle_GenerateValidateRevoke_WorksCorrectly()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.Editor,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Act - Generate
        (string plaintextKey, var apiKeyEntity) = await service.GenerateApiKeyAsync(
            userId.ToString(),
            "Integration Test Key",
            new[] { "read", "write", "delete" },
            expiresAt: null,
            environment: "test");
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act - Validate (should succeed)
        var validationResult1 = await service.ValidateApiKeyAsync(plaintextKey);
        validationResult1.IsValid.Should().BeTrue();
        validationResult1.UserRole.Should().Be("Editor");
        validationResult1.Scopes.Length.Should().Be(3);

        // Act - Revoke
        var revokeResult = await service.RevokeApiKeyAsync(apiKeyEntity.Id.ToString(), userId.ToString());
        revokeResult.Should().BeTrue();

        // Act - Validate (should fail)
        var validationResult2 = await service.ValidateApiKeyAsync(plaintextKey);
        validationResult2.IsValid.Should().BeFalse();
        validationResult2.InvalidReason.Should().Be("Invalid, expired, or revoked API key");
    }

    [Fact]
    public async Task KeyHashing_SameKeyGeneratesSameHash()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var passwordHashingService = new PasswordHashingService();
        var service = new ApiKeyAuthenticationService(dbContext, passwordHashingService, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);

        // Create test user
        var userId = Guid.NewGuid();
        var user = new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Test User",
            PasswordHash = "dummy",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        // Generate key
        (string plaintextKey, var apiKeyEntity) = await service.GenerateApiKeyAsync(userId.ToString(), "Test Key", new[] { "read" });
        dbContext.ApiKeys.Add(apiKeyEntity);
        await dbContext.SaveChangesAsync();

        // Act - Validate with same key multiple times
        var result1 = await service.ValidateApiKeyAsync(plaintextKey);
        var result2 = await service.ValidateApiKeyAsync(plaintextKey);

        // Assert - Both validations should succeed
        result1.IsValid.Should().BeTrue();
        result2.IsValid.Should().BeTrue();
        result2.ApiKeyId.Should().Be(result1.ApiKeyId);
    }

    #endregion

    #region Helper Methods

    private MeepleAiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
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
