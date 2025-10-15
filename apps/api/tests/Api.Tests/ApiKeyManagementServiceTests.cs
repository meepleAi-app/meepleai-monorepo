using System;
using System.Linq;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Unit tests for ApiKeyManagementService.
/// Tests CRUD operations, rotation, and quota management for API keys.
/// Related to Issue #259 - API-04: API Key Management and Quota System.
/// </summary>
public class ApiKeyManagementServiceTests
{
    #region ListApiKeysAsync Tests

    [Fact]
    public async Task ListApiKeysAsync_WithNoKeys_ReturnsEmptyList()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.ListApiKeysAsync(user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Keys);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    [Fact]
    public async Task ListApiKeysAsync_WithMultipleKeys_ReturnsPaginatedList()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Create 3 API keys
        for (int i = 1; i <= 3; i++)
        {
            var request = new CreateApiKeyRequest
            {
                KeyName = $"Test Key {i}",
                Scopes = new[] { "read" },
                Environment = "test"
            };
            await service.CreateApiKeyAsync(user.Id, request);
        }

        // Act
        var result = await service.ListApiKeysAsync(user.Id, page: 1, pageSize: 2);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Keys.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
    }

    [Fact]
    public async Task ListApiKeysAsync_ExcludesRevokedKeys_ByDefault()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Create active key
        var activeKeyRequest = new CreateApiKeyRequest
        {
            KeyName = "Active Key",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        await service.CreateApiKeyAsync(user.Id, activeKeyRequest);

        // Create and revoke another key
        var revokedKeyRequest = new CreateApiKeyRequest
        {
            KeyName = "Revoked Key",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        var revokedKeyResponse = await service.CreateApiKeyAsync(user.Id, revokedKeyRequest);
        await service.RevokeApiKeyAsync(revokedKeyResponse.ApiKey.Id, user.Id);

        // Act
        var result = await service.ListApiKeysAsync(user.Id, includeRevoked: false);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Keys);
        Assert.Equal("Active Key", result.Keys[0].KeyName);
        Assert.Null(result.Keys[0].RevokedAt);
    }

    [Fact]
    public async Task ListApiKeysAsync_IncludesRevokedKeys_WhenRequested()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Create and revoke a key
        var request = new CreateApiKeyRequest
        {
            KeyName = "Revoked Key",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        var response = await service.CreateApiKeyAsync(user.Id, request);
        await service.RevokeApiKeyAsync(response.ApiKey.Id, user.Id);

        // Act
        var result = await service.ListApiKeysAsync(user.Id, includeRevoked: true);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Keys);
        Assert.NotNull(result.Keys[0].RevokedAt);
    }

    #endregion

    #region GetApiKeyAsync Tests

    [Fact]
    public async Task GetApiKeyAsync_WithValidKeyId_ReturnsKey()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "Test Key",
            Scopes = new[] { "read", "write" },
            Environment = "live"
        };
        var createResponse = await service.CreateApiKeyAsync(user.Id, createRequest);

        // Act
        var result = await service.GetApiKeyAsync(createResponse.ApiKey.Id, user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(createResponse.ApiKey.Id, result.Id);
        Assert.Equal("Test Key", result.KeyName);
        Assert.Equal(2, result.Scopes.Length);
        Assert.Contains("read", result.Scopes);
        Assert.Contains("write", result.Scopes);
    }

    [Fact]
    public async Task GetApiKeyAsync_WithInvalidKeyId_ReturnsNull()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.GetApiKeyAsync("non-existent-id", user.Id);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetApiKeyAsync_WithWrongUserId_ReturnsNull()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user1 = await CreateTestUserAsync(dbContext, timeProvider, "user1@test.com");
        var user2 = await CreateTestUserAsync(dbContext, timeProvider, "user2@test.com");

        var request = new CreateApiKeyRequest
        {
            KeyName = "User1 Key",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        var response = await service.CreateApiKeyAsync(user1.Id, request);

        // Act - User2 tries to access User1's key
        var result = await service.GetApiKeyAsync(response.ApiKey.Id, user2.Id);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region CreateApiKeyAsync Tests

    [Fact]
    public async Task CreateApiKeyAsync_WithValidRequest_CreatesKey()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var request = new CreateApiKeyRequest
        {
            KeyName = "Production Key",
            Scopes = new[] { "read", "write", "admin" },
            Environment = "live",
            ExpiresAt = DateTime.UtcNow.AddYears(1)
        };

        // Act
        var result = await service.CreateApiKeyAsync(user.Id, request);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.PlaintextKey);
        Assert.StartsWith("mpl_live_", result.PlaintextKey);
        Assert.NotNull(result.ApiKey);
        Assert.Equal("Production Key", result.ApiKey.KeyName);
        Assert.Equal(3, result.ApiKey.Scopes.Length);
        Assert.True(result.ApiKey.IsActive);
        Assert.NotNull(result.ApiKey.ExpiresAt);
        Assert.Equal("Store this key securely. It will not be shown again.", result.Warning);
    }

    [Fact]
    public async Task CreateApiKeyAsync_WithQuota_StoresQuotaInMetadata()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var request = new CreateApiKeyRequest
        {
            KeyName = "Quota Key",
            Scopes = new[] { "read" },
            Environment = "test",
            MaxRequestsPerDay = 1000,
            MaxRequestsPerHour = 100
        };

        // Act
        var result = await service.CreateApiKeyAsync(user.Id, request);

        // Assert
        Assert.NotNull(result.ApiKey.Quota);
        Assert.Equal(1000, result.ApiKey.Quota.MaxRequestsPerDay);
        Assert.Equal(100, result.ApiKey.Quota.MaxRequestsPerHour);
    }

    [Fact]
    public async Task CreateApiKeyAsync_WithEmptyKeyName_ThrowsArgumentException()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var request = new CreateApiKeyRequest
        {
            KeyName = "",
            Scopes = new[] { "read" }
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateApiKeyAsync(user.Id, request));
    }

    [Theory]
    [InlineData("live")]
    [InlineData("test")]
    public async Task CreateApiKeyAsync_WithDifferentEnvironments_GeneratesCorrectPrefix(string environment)
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var request = new CreateApiKeyRequest
        {
            KeyName = $"{environment} Key",
            Scopes = new[] { "read" },
            Environment = environment
        };

        // Act
        var result = await service.CreateApiKeyAsync(user.Id, request);

        // Assert
        Assert.StartsWith($"mpl_{environment}_", result.PlaintextKey);
    }

    #endregion

    #region UpdateApiKeyAsync Tests

    [Fact]
    public async Task UpdateApiKeyAsync_WithValidRequest_UpdatesKey()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "Original Name",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        var createResponse = await service.CreateApiKeyAsync(user.Id, createRequest);

        var updateRequest = new UpdateApiKeyRequest
        {
            KeyName = "Updated Name",
            Scopes = new[] { "read", "write", "admin" },
            IsActive = false
        };

        // Act
        var result = await service.UpdateApiKeyAsync(createResponse.ApiKey.Id, user.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.KeyName);
        Assert.Equal(3, result.Scopes.Length);
        Assert.Contains("admin", result.Scopes);
        Assert.False(result.IsActive);
    }

    [Fact]
    public async Task UpdateApiKeyAsync_UpdatesOnlyProvidedFields()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "Original Name",
            Scopes = new[] { "read", "write" },
            Environment = "test"
        };
        var createResponse = await service.CreateApiKeyAsync(user.Id, createRequest);

        var updateRequest = new UpdateApiKeyRequest
        {
            KeyName = "Updated Name"
            // Scopes and IsActive not provided - should remain unchanged
        };

        // Act
        var result = await service.UpdateApiKeyAsync(createResponse.ApiKey.Id, user.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.KeyName);
        Assert.Equal(2, result.Scopes.Length); // Original scopes preserved
        Assert.True(result.IsActive); // Original active state preserved
    }

    [Fact]
    public async Task UpdateApiKeyAsync_WithInvalidKeyId_ReturnsNull()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var updateRequest = new UpdateApiKeyRequest
        {
            KeyName = "Updated Name"
        };

        // Act
        var result = await service.UpdateApiKeyAsync("non-existent-id", user.Id, updateRequest);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateApiKeyAsync_WithWrongUserId_ReturnsNull()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user1 = await CreateTestUserAsync(dbContext, timeProvider, "user1@test.com");
        var user2 = await CreateTestUserAsync(dbContext, timeProvider, "user2@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "User1 Key",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        var createResponse = await service.CreateApiKeyAsync(user1.Id, createRequest);

        var updateRequest = new UpdateApiKeyRequest
        {
            KeyName = "Hacked Name"
        };

        // Act - User2 tries to update User1's key
        var result = await service.UpdateApiKeyAsync(createResponse.ApiKey.Id, user2.Id, updateRequest);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task UpdateApiKeyAsync_WithQuota_UpdatesQuotaMetadata()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "Quota Key",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        var createResponse = await service.CreateApiKeyAsync(user.Id, createRequest);

        var updateRequest = new UpdateApiKeyRequest
        {
            MaxRequestsPerDay = 5000,
            MaxRequestsPerHour = 500
        };

        // Act
        var result = await service.UpdateApiKeyAsync(createResponse.ApiKey.Id, user.Id, updateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Quota);
        Assert.Equal(5000, result.Quota.MaxRequestsPerDay);
        Assert.Equal(500, result.Quota.MaxRequestsPerHour);
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "To Be Revoked",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        var createResponse = await service.CreateApiKeyAsync(user.Id, createRequest);

        // Act
        var result = await service.RevokeApiKeyAsync(createResponse.ApiKey.Id, user.Id);

        // Assert
        Assert.True(result);

        // Verify key is revoked
        var revokedKey = await service.GetApiKeyAsync(createResponse.ApiKey.Id, user.Id);
        Assert.NotNull(revokedKey);
        Assert.NotNull(revokedKey.RevokedAt);
        Assert.Equal(user.Id, revokedKey.RevokedBy);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithInvalidKeyId_ReturnsFalse()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.RevokeApiKeyAsync("non-existent-id", user.Id);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region RotateApiKeyAsync Tests

    [Fact]
    public async Task RotateApiKeyAsync_WithValidKey_CreatesNewKeyAndRevokesOld()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "Original Key",
            Scopes = new[] { "read", "write" },
            Environment = "live",
            MaxRequestsPerDay = 1000
        };
        var createResponse = await service.CreateApiKeyAsync(user.Id, createRequest);
        var oldKeyId = createResponse.ApiKey.Id;
        var oldPlaintextKey = createResponse.PlaintextKey;

        var rotateRequest = new RotateApiKeyRequest
        {
            ExpiresAt = DateTime.UtcNow.AddYears(1)
        };

        // Act
        var result = await service.RotateApiKeyAsync(oldKeyId, user.Id, rotateRequest);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(oldPlaintextKey, result.PlaintextKey);
        Assert.Equal(oldKeyId, result.RevokedKeyId);
        Assert.Equal("Original Key (Rotated)", result.NewApiKey.KeyName);
        Assert.Equal(2, result.NewApiKey.Scopes.Length);
        Assert.NotNull(result.NewApiKey.ExpiresAt);

        // Verify old key is revoked
        var oldKey = await service.GetApiKeyAsync(oldKeyId, user.Id);
        Assert.NotNull(oldKey);
        Assert.NotNull(oldKey.RevokedAt);

        // Verify new key inherits quota
        Assert.NotNull(result.NewApiKey.Quota);
        Assert.Equal(1000, result.NewApiKey.Quota.MaxRequestsPerDay);
    }

    [Fact]
    public async Task RotateApiKeyAsync_WithInvalidKeyId_ReturnsNull()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var rotateRequest = new RotateApiKeyRequest();

        // Act
        var result = await service.RotateApiKeyAsync("non-existent-id", user.Id, rotateRequest);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task RotateApiKeyAsync_WithWrongUserId_ReturnsNull()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user1 = await CreateTestUserAsync(dbContext, timeProvider, "user1@test.com");
        var user2 = await CreateTestUserAsync(dbContext, timeProvider, "user2@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "User1 Key",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        var createResponse = await service.CreateApiKeyAsync(user1.Id, createRequest);

        var rotateRequest = new RotateApiKeyRequest();

        // Act - User2 tries to rotate User1's key
        var result = await service.RotateApiKeyAsync(createResponse.ApiKey.Id, user2.Id, rotateRequest);

        // Assert
        Assert.Null(result);
    }

    #endregion

    #region DeleteApiKeyAsync Tests

    [Fact]
    public async Task DeleteApiKeyAsync_WithValidKey_DeletesSuccessfully()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "To Be Deleted",
            Scopes = new[] { "read" },
            Environment = "test"
        };
        var createResponse = await service.CreateApiKeyAsync(user.Id, createRequest);

        // Act
        var result = await service.DeleteApiKeyAsync(createResponse.ApiKey.Id, user.Id);

        // Assert
        Assert.True(result);

        // Verify key is deleted
        var deletedKey = await service.GetApiKeyAsync(createResponse.ApiKey.Id, user.Id);
        Assert.Null(deletedKey);
    }

    [Fact]
    public async Task DeleteApiKeyAsync_WithInvalidKeyId_ReturnsFalse()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.DeleteApiKeyAsync("non-existent-id", user.Id);

        // Assert
        Assert.False(result);
    }

    #endregion

    #region GetApiKeyUsageAsync Tests

    [Fact]
    public async Task GetApiKeyUsageAsync_WithValidKey_ReturnsUsageStats()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var createRequest = new CreateApiKeyRequest
        {
            KeyName = "Usage Tracking Key",
            Scopes = new[] { "read" },
            Environment = "test",
            MaxRequestsPerDay = 1000,
            MaxRequestsPerHour = 100
        };
        var createResponse = await service.CreateApiKeyAsync(user.Id, createRequest);

        // Act
        var result = await service.GetApiKeyUsageAsync(createResponse.ApiKey.Id, user.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1000, result.MaxRequestsPerDay);
        Assert.Equal(100, result.MaxRequestsPerHour);
        Assert.NotNull(result.ResetsAt);
    }

    [Fact]
    public async Task GetApiKeyUsageAsync_WithInvalidKeyId_ReturnsNull()
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
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.GetApiKeyUsageAsync("non-existent-id", user.Id);

        // Assert
        Assert.Null(result);
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

    private static async Task<UserEntity> CreateTestUserAsync(
        MeepleAiDbContext dbContext,
        TimeProvider timeProvider,
        string email)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = email,
            DisplayName = "Test User",
            PasswordHash = "dummy-hash",
            Role = UserRole.User,
            CreatedAt = timeProvider.GetUtcNow().UtcDateTime
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        return user;
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
