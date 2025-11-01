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
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Unit tests for ApiKeyManagementService.
/// Tests CRUD operations, rotation, and quota management for API keys.
/// Related to Issue #259 - API-04: API Key Management and Quota System.
/// </summary>
public class ApiKeyManagementServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;

    public ApiKeyManagementServiceTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    #region ListApiKeysAsync Tests

    [Fact]
    public async Task ListApiKeysAsync_WithNoKeys_ReturnsEmptyList()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.ListApiKeysAsync(user.Id);

        // Assert
        result.Should().NotBeNull();
        result.Keys.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(20);
    }

    [Fact]
    public async Task ListApiKeysAsync_WithMultipleKeys_ReturnsPaginatedList()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.Keys.Count.Should().Be(2);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task ListApiKeysAsync_ExcludesRevokedKeys_ByDefault()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.Keys.Should().ContainSingle();
        result.Keys[0].KeyName.Should().Be("Active Key");
        result.Keys[0].RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ListApiKeysAsync_IncludesRevokedKeys_WhenRequested()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.Keys.Should().ContainSingle();
        result.Keys[0].RevokedAt.Should().NotBeNull();
    }

    #endregion

    #region GetApiKeyAsync Tests

    [Fact]
    public async Task GetApiKeyAsync_WithValidKeyId_ReturnsKey()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.Id.Should().BeEquivalentTo(createResponse.ApiKey.Id);
        result.KeyName.Should().BeEquivalentTo("Test Key");
        result.Scopes.Length.Should().Be(2);
        result.Scopes.Should().Contain("read");
        result.Scopes.Should().Contain("write");
    }

    [Fact]
    public async Task GetApiKeyAsync_WithInvalidKeyId_ReturnsNull()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.GetApiKeyAsync("non-existent-id", user.Id);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetApiKeyAsync_WithWrongUserId_ReturnsNull()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().BeNull();
    }

    #endregion

    #region CreateApiKeyAsync Tests

    [Fact]
    public async Task CreateApiKeyAsync_WithValidRequest_CreatesKey()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.PlaintextKey.Should().NotBeNull();
        result.PlaintextKey.Should().StartWith("mpl_live_");
        result.ApiKey.Should().NotBeNull();
        result.ApiKey.KeyName.Should().Be("Production Key");
        result.ApiKey.Scopes.Length.Should().Be(3);
        result.ApiKey.IsActive.Should().BeTrue();
        result.ApiKey.ExpiresAt.Should().NotBeNull();
        result.Warning.Should().BeEquivalentTo("Store this key securely. It will not be shown again.");
    }

    [Fact]
    public async Task CreateApiKeyAsync_WithQuota_StoresQuotaInMetadata()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.ApiKey.Quota.Should().NotBeNull();
        result.ApiKey.Quota.MaxRequestsPerDay.Should().Be(1000);
        result.ApiKey.Quota.MaxRequestsPerHour.Should().Be(100);
    }

    [Fact]
    public async Task CreateApiKeyAsync_WithEmptyKeyName_ThrowsArgumentException()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        var act = async () => await service.CreateApiKeyAsync(user.Id, request);
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("live")]
    [InlineData("test")]
    public async Task CreateApiKeyAsync_WithDifferentEnvironments_GeneratesCorrectPrefix(string environment)
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.PlaintextKey.Should().StartWith($"mpl_{environment}_");
    }

    #endregion

    #region UpdateApiKeyAsync Tests

    [Fact]
    public async Task UpdateApiKeyAsync_WithValidRequest_UpdatesKey()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.KeyName.Should().BeEquivalentTo("Updated Name");
        result.Scopes.Length.Should().Be(3);
        result.Scopes.Should().Contain("admin");
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateApiKeyAsync_UpdatesOnlyProvidedFields()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.KeyName.Should().BeEquivalentTo("Updated Name");
        result.Scopes.Length.Should().Be(2); // Original scopes preserved
        result.IsActive.Should().BeTrue(); // Original active state preserved
    }

    [Fact]
    public async Task UpdateApiKeyAsync_WithInvalidKeyId_ReturnsNull()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateApiKeyAsync_WithWrongUserId_ReturnsNull()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateApiKeyAsync_WithQuota_UpdatesQuotaMetadata()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.Quota.Should().NotBeNull();
        result.Quota.MaxRequestsPerDay.Should().Be(5000);
        result.Quota.MaxRequestsPerHour.Should().Be(500);
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
        result.Should().BeTrue();

        // Verify key is revoked
        var revokedKey = await service.GetApiKeyAsync(createResponse.ApiKey.Id, user.Id);
        revokedKey.Should().NotBeNull();
        revokedKey.RevokedAt.Should().NotBeNull();
        revokedKey.RevokedBy.Should().Be(user.Id);
    }

    [Fact]
    public async Task RevokeApiKeyAsync_WithInvalidKeyId_ReturnsFalse()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.RevokeApiKeyAsync("non-existent-id", user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region RotateApiKeyAsync Tests

    [Fact]
    public async Task RotateApiKeyAsync_WithValidKey_CreatesNewKeyAndRevokesOld()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.PlaintextKey.Should().NotBe(oldPlaintextKey);
        result.RevokedKeyId.Should().BeEquivalentTo(oldKeyId);
        result.NewApiKey.KeyName.Should().Be("Original Key (Rotated)");
        result.NewApiKey.Scopes.Length.Should().Be(2);
        result.NewApiKey.ExpiresAt.Should().NotBeNull();

        // Verify old key is revoked
        var oldKey = await service.GetApiKeyAsync(oldKeyId, user.Id);
        oldKey.Should().NotBeNull();
        oldKey.RevokedAt.Should().NotBeNull();

        // Verify new key inherits quota
        result.NewApiKey.Quota.Should().NotBeNull();
        result.NewApiKey.Quota.MaxRequestsPerDay.Should().Be(1000);
    }

    [Fact]
    public async Task RotateApiKeyAsync_WithInvalidKeyId_ReturnsNull()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        var rotateRequest = new RotateApiKeyRequest();

        // Act
        var result = await service.RotateApiKeyAsync("non-existent-id", user.Id, rotateRequest);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RotateApiKeyAsync_WithWrongUserId_ReturnsNull()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().BeNull();
    }

    #endregion

    #region DeleteApiKeyAsync Tests

    [Fact]
    public async Task DeleteApiKeyAsync_WithValidKey_DeletesSuccessfully()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().BeTrue();

        // Verify key is deleted
        var deletedKey = await service.GetApiKeyAsync(createResponse.ApiKey.Id, user.Id);
        deletedKey.Should().BeNull();
    }

    [Fact]
    public async Task DeleteApiKeyAsync_WithInvalidKeyId_ReturnsFalse()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.DeleteApiKeyAsync("non-existent-id", user.Id);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region GetApiKeyUsageAsync Tests

    [Fact]
    public async Task GetApiKeyUsageAsync_WithValidKey_ReturnsUsageStats()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
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
        result.Should().NotBeNull();
        result.MaxRequestsPerDay.Should().Be(1000);
        result.MaxRequestsPerHour.Should().Be(100);
        result.ResetsAt.Should().NotBe(default(DateTime));
    }

    [Fact]
    public async Task GetApiKeyUsageAsync_WithInvalidKeyId_ReturnsNull()
    {
        // Arrange
        await using (var setupContext = CreateContext())
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = CreateContext();
        var timeProvider = new FixedTimeProvider(DateTimeOffset.Parse("2024-01-01T00:00:00Z"));
        var authService = new ApiKeyAuthenticationService(dbContext, NullLogger<ApiKeyAuthenticationService>.Instance, timeProvider);
        var service = new ApiKeyManagementService(dbContext, authService, NullLogger<ApiKeyManagementService>.Instance, timeProvider);

        var user = await CreateTestUserAsync(dbContext, timeProvider, "user@test.com");

        // Act
        var result = await service.GetApiKeyUsageAsync("non-existent-id", user.Id);

        // Assert
        result.Should().BeNull();
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
