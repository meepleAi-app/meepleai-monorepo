using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Integration tests for API key management endpoints.
/// Tests the complete flow through HTTP endpoints, middleware, and services.
/// Related to Issue #259 - API-04: API Key Management and Quota System.
/// </summary>
public class ApiKeyManagementEndpointsTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public ApiKeyManagementEndpointsTests(WebApplicationFactoryFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    #region POST /api/v1/api-keys - Create API Key

    [Fact]
    public async Task CreateApiKey_WithValidRequest_ReturnsCreatedKey()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("create-key-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var request = new CreateApiKeyRequest
        {
            KeyName = "Production Key",
            Scopes = new[] { "read", "write" },
            Environment = "live",
            ExpiresAt = DateTime.UtcNow.AddYears(1),
            MaxRequestsPerDay = 1000,
            MaxRequestsPerHour = 100
        };

        // When: User creates an API key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: API key is created successfully
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        result.Should().NotBeNull();
        result.PlaintextKey.Should().NotBeNull();
        result.PlaintextKey.Should().StartWith("mpl_live_");
        result.ApiKey.KeyName.Should().Be("Production Key");
        result.ApiKey.Scopes.Length.Should().Be(2);
        result.ApiKey.IsActive.Should().BeTrue();
        result.ApiKey.Quota.Should().NotBeNull();
        result.ApiKey.Quota.MaxRequestsPerDay.Should().Be(1000);
        result.ApiKey.Quota.MaxRequestsPerHour.Should().Be(100);
    }

    [Fact]
    public async Task CreateApiKey_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Given: No authentication
        var request = new CreateApiKeyRequest
        {
            KeyName = "Test Key",
            Scopes = new[] { "read" },
            Environment = "test"
        };

        // When: Unauthenticated user tries to create API key
        var client = Factory.CreateHttpsClient();
        var response = await client.PostAsJsonAsync("/api/v1/api-keys", request);

        // Then: Request is unauthorized
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateApiKey_WithEmptyKeyName_ReturnsBadRequest()
    {
        // Given: An authenticated user with invalid request
        var user = await CreateTestUserAsync("invalid-request-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var request = new CreateApiKeyRequest
        {
            KeyName = "", // Invalid: empty name
            Scopes = new[] { "read" }
        };

        // When: User creates API key with invalid data
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Request fails with bad request
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("live")]
    [InlineData("test")]
    public async Task CreateApiKey_WithDifferentEnvironments_CreatesCorrectKeyFormat(string environment)
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync($"env-{environment}-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var request = new CreateApiKeyRequest
        {
            KeyName = $"{environment} Key",
            Scopes = new[] { "read" },
            Environment = environment
        };

        // When: User creates API key for specific environment
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys")
        {
            Content = JsonContent.Create(request)
        };
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: API key has correct environment prefix
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var result = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        result.Should().NotBeNull();
        result.PlaintextKey.Should().StartWith($"mpl_{environment}_");
    }

    #endregion

    #region GET /api/v1/api-keys - List API Keys

    [Fact]
    public async Task ListApiKeys_WithMultipleKeys_ReturnsPaginatedList()
    {
        // Given: A user with multiple API keys
        var user = await CreateTestUserAsync("list-keys-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // Create 3 API keys
        for (int i = 1; i <= 3; i++)
        {
            var (_, _) = await CreateTestApiKeyAsync(
                user.Id,
                $"Key {i}",
                new[] { "read" });
        }

        // When: User lists their API keys
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys?page=1&pageSize=2");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Paginated list is returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyListResponse>();
        result.Should().NotBeNull();
        result.Keys.Count.Should().Be(2);
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task ListApiKeys_ExcludesRevokedKeys_ByDefault()
    {
        // Given: A user with active and revoked keys
        var user = await CreateTestUserAsync("revoked-keys-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // Create active key
        await CreateTestApiKeyAsync(user.Id, "Active Key", new[] { "read" });

        // Create and revoke another key
        var (_, revokedKey) = await CreateTestApiKeyAsync(user.Id, "Revoked Key", new[] { "read" });
        await RevokeTestApiKeyAsync(revokedKey.Id, user.Id);

        // When: User lists API keys without includeRevoked
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Only active keys are returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyListResponse>();
        result.Should().NotBeNull();
        result.Keys.Should().ContainSingle();
        result.Keys[0].RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ListApiKeys_IncludesRevokedKeys_WhenRequested()
    {
        // Given: A user with revoked key
        var user = await CreateTestUserAsync("include-revoked-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var (_, key) = await CreateTestApiKeyAsync(user.Id, "Revoked Key", new[] { "read" });
        await RevokeTestApiKeyAsync(key.Id, user.Id);

        // When: User lists API keys with includeRevoked=true
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys?includeRevoked=true");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Revoked keys are included
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyListResponse>();
        result.Should().NotBeNull();
        result.Keys.Should().ContainSingle();
        result.Keys[0].RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ListApiKeys_OnlyReturnsCurrentUserKeys()
    {
        // Given: Two users with their own keys
        var user1 = await CreateTestUserAsync("user1-isolation", UserRole.User);
        var user2 = await CreateTestUserAsync("user2-isolation", UserRole.User);

        await CreateTestApiKeyAsync(user1.Id, "User1 Key", new[] { "read" });
        await CreateTestApiKeyAsync(user2.Id, "User2 Key", new[] { "read" });

        // When: User1 lists their API keys
        var cookies = await AuthenticateUserAsync(user1.Email);
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Only User1's keys are returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyListResponse>();
        result.Should().NotBeNull();
        result.Keys.Should().ContainSingle();
        result.Keys[0].KeyName.Should().Be("User1 Key");
    }

    #endregion

    #region GET /api/v1/api-keys/{id} - Get API Key

    [Fact]
    public async Task GetApiKey_WithValidId_ReturnsKey()
    {
        // Given: A user with an API key
        var user = await CreateTestUserAsync("get-key-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var (_, key) = await CreateTestApiKeyAsync(
            user.Id,
            "Test Key",
            new[] { "read", "write" });

        // When: User retrieves their API key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/{key.Id}");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: API key details are returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyDto>();
        result.Should().NotBeNull();
        result.Id.Should().Be(key.Id);
        result.KeyName.Should().Be("Test Key");
        result.Scopes.Length.Should().Be(2);
    }

    [Fact]
    public async Task GetApiKey_WithInvalidId_ReturnsNotFound()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("invalid-id-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // When: User requests non-existent key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys/non-existent-id");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Not found is returned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetApiKey_ForOtherUsersKey_ReturnsNotFound()
    {
        // Given: Two users, User1 has a key
        var user1 = await CreateTestUserAsync("user1-get", UserRole.User);
        var user2 = await CreateTestUserAsync("user2-get", UserRole.User);

        var (_, key) = await CreateTestApiKeyAsync(user1.Id, "User1 Key", new[] { "read" });

        // When: User2 tries to access User1's key
        var cookies = await AuthenticateUserAsync(user2.Email);
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/{key.Id}");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Not found is returned (authorization check)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region PUT /api/v1/api-keys/{id} - Update API Key

    [Fact]
    public async Task UpdateApiKey_WithValidRequest_UpdatesKey()
    {
        // Given: A user with an API key
        var user = await CreateTestUserAsync("update-key-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var (_, key) = await CreateTestApiKeyAsync(
            user.Id,
            "Original Name",
            new[] { "read" });

        var updateRequest = new UpdateApiKeyRequest
        {
            KeyName = "Updated Name",
            Scopes = new[] { "read", "write", "admin" },
            MaxRequestsPerDay = 5000
        };

        // When: User updates their API key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/api-keys/{key.Id}")
        {
            Content = JsonContent.Create(updateRequest)
        };
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: API key is updated successfully
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyDto>();
        result.Should().NotBeNull();
        result.KeyName.Should().Be("Updated Name");
        result.Scopes.Length.Should().Be(3);
        result.Scopes.Should().Contain("admin");
        result.Quota.Should().NotBeNull();
        result.Quota.MaxRequestsPerDay.Should().Be(5000);
    }

    [Fact]
    public async Task UpdateApiKey_WithInvalidId_ReturnsNotFound()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("update-invalid-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var updateRequest = new UpdateApiKeyRequest
        {
            KeyName = "Updated Name"
        };

        // When: User tries to update non-existent key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/api-keys/non-existent-id")
        {
            Content = JsonContent.Create(updateRequest)
        };
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Not found is returned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateApiKey_ForOtherUsersKey_ReturnsNotFound()
    {
        // Given: Two users, User1 has a key
        var user1 = await CreateTestUserAsync("user1-update", UserRole.User);
        var user2 = await CreateTestUserAsync("user2-update", UserRole.User);

        var (_, key) = await CreateTestApiKeyAsync(user1.Id, "User1 Key", new[] { "read" });

        var updateRequest = new UpdateApiKeyRequest
        {
            KeyName = "Hacked Name"
        };

        // When: User2 tries to update User1's key
        var cookies = await AuthenticateUserAsync(user2.Email);
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/api-keys/{key.Id}")
        {
            Content = JsonContent.Create(updateRequest)
        };
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Not found is returned (authorization check)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region POST /api/v1/api-keys/{id}/rotate - Rotate API Key

    [Fact]
    public async Task RotateApiKey_WithValidKey_CreatesNewKeyAndRevokesOld()
    {
        // Given: A user with an API key
        var user = await CreateTestUserAsync("rotate-key-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var (oldPlaintextKey, oldKey) = await CreateTestApiKeyAsync(
            user.Id,
            "Original Key",
            new[] { "read", "write" });

        var rotateRequest = new RotateApiKeyRequest
        {
            ExpiresAt = DateTime.UtcNow.AddYears(1)
        };

        // When: User rotates their API key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/api-keys/{oldKey.Id}/rotate")
        {
            Content = JsonContent.Create(rotateRequest)
        };
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: New key is created and old key is revoked
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<RotateApiKeyResponse>();
        result.Should().NotBeNull();
        result.PlaintextKey.Should().NotBe(oldPlaintextKey);
        result.RevokedKeyId.Should().Be(oldKey.Id);
        result.NewApiKey.KeyName.Should().Be("Original Key (Rotated)");
        result.NewApiKey.Scopes.Length.Should().Be(2);

        // Verify old key cannot be used
        var testRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        testRequest.Headers.Add("X-API-Key", oldPlaintextKey);
        var testResponse = await client.SendAsync(testRequest);
        testResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Verify new key works
        var newTestRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        newTestRequest.Headers.Add("X-API-Key", result.PlaintextKey);
        var newTestResponse = await client.SendAsync(newTestRequest);
        newTestResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RotateApiKey_WithInvalidId_ReturnsNotFound()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("rotate-invalid-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var rotateRequest = new RotateApiKeyRequest();

        // When: User tries to rotate non-existent key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/api-keys/non-existent-id/rotate")
        {
            Content = JsonContent.Create(rotateRequest)
        };
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Not found is returned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RotateApiKey_ForOtherUsersKey_ReturnsNotFound()
    {
        // Given: Two users, User1 has a key
        var user1 = await CreateTestUserAsync("user1-rotate", UserRole.User);
        var user2 = await CreateTestUserAsync("user2-rotate", UserRole.User);

        var (_, key) = await CreateTestApiKeyAsync(user1.Id, "User1 Key", new[] { "read" });

        var rotateRequest = new RotateApiKeyRequest();

        // When: User2 tries to rotate User1's key
        var cookies = await AuthenticateUserAsync(user2.Email);
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/api-keys/{key.Id}/rotate")
        {
            Content = JsonContent.Create(rotateRequest)
        };
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Not found is returned (authorization check)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region DELETE /api/v1/api-keys/{id} - Revoke API Key

    [Fact]
    public async Task RevokeApiKey_WithValidKey_RevokesSuccessfully()
    {
        // Given: A user with an API key
        var user = await CreateTestUserAsync("revoke-key-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var (plaintextKey, key) = await CreateTestApiKeyAsync(
            user.Id,
            "To Be Revoked",
            new[] { "read" });

        // When: User revokes their API key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/api-keys/{key.Id}");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: API key is revoked
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify key cannot be used
        var testRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        testRequest.Headers.Add("X-API-Key", plaintextKey);
        var testResponse = await client.SendAsync(testRequest);
        testResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RevokeApiKey_WithInvalidId_ReturnsNotFound()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("revoke-invalid-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // When: User tries to revoke non-existent key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/api-keys/non-existent-id");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Not found is returned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RevokeApiKey_ForOtherUsersKey_ReturnsNotFound()
    {
        // Given: Two users, User1 has a key
        var user1 = await CreateTestUserAsync("user1-revoke", UserRole.User);
        var user2 = await CreateTestUserAsync("user2-revoke", UserRole.User);

        var (_, key) = await CreateTestApiKeyAsync(user1.Id, "User1 Key", new[] { "read" });

        // When: User2 tries to revoke User1's key
        var cookies = await AuthenticateUserAsync(user2.Email);
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/api-keys/{key.Id}");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Not found is returned (authorization check)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region GET /api/v1/api-keys/{id}/usage - Get API Key Usage

    [Fact]
    public async Task GetApiKeyUsage_WithValidKey_ReturnsUsageStats()
    {
        // Given: A user with an API key that has quota
        var user = await CreateTestUserAsync("usage-key-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        var (_, key) = await CreateTestApiKeyAsync(
            user.Id,
            "Usage Tracking Key",
            new[] { "read" },
            maxRequestsPerDay: 1000,
            maxRequestsPerHour: 100);

        // When: User requests usage statistics
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/api-keys/{key.Id}/usage");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Usage statistics are returned
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyQuotaDto>();
        result.Should().NotBeNull();
        result.MaxRequestsPerDay.Should().Be(1000);
        result.MaxRequestsPerHour.Should().Be(100);
        result.ResetsAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetApiKeyUsage_WithInvalidId_ReturnsNotFound()
    {
        // Given: An authenticated user
        var user = await CreateTestUserAsync("usage-invalid-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // When: User requests usage for non-existent key
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/api-keys/non-existent-id/usage");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Not found is returned
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Admin Endpoints - DELETE /admin/api-keys/{id}

    [Fact]
    public async Task AdminDeleteApiKey_AsAdmin_DeletesSuccessfully()
    {
        // Given: An admin user and a regular user with an API key
        var admin = await CreateTestUserAsync("admin-delete", UserRole.Admin);
        var regularUser = await CreateTestUserAsync("regular-delete", UserRole.User);

        var (_, key) = await CreateTestApiKeyAsync(regularUser.Id, "User Key", new[] { "read" });

        // When: Admin deletes the user's API key
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/api-keys/{key.Id}");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: API key is deleted permanently
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AdminDeleteApiKey_AsNonAdmin_ReturnsForbidden()
    {
        // Given: A regular user trying to use admin endpoint
        var user = await CreateTestUserAsync("non-admin-delete", UserRole.User);
        var (_, key) = await CreateTestApiKeyAsync(user.Id, "User Key", new[] { "read" });

        // When: Non-admin tries to delete via admin endpoint
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/api-keys/{key.Id}");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Then: Forbidden is returned
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    #endregion

    #region Helper Methods

    private async Task<(string PlaintextKey, ApiKeyEntity Entity)> CreateTestApiKeyAsync(
        string userId,
        string keyName,
        string[] scopes,
        DateTime? expiresAt = null,
        string environment = "test",
        int? maxRequestsPerDay = null,
        int? maxRequestsPerHour = null)
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<Api.Services.ApiKeyAuthenticationService>();

        var (plaintextKey, entity) = await service.GenerateApiKeyAsync(
            userId,
            keyName,
            scopes,
            expiresAt,
            environment);

        // Persist the generated key for endpoint tests
        // Aligns with IntegrationTestBase helper behavior
        var db = scope.ServiceProvider.GetRequiredService<Api.Infrastructure.MeepleAiDbContext>();
        // Avoid EF tracking conflict on User navigation
        entity.User = null!;
        // Apply quota metadata if provided (used by usage tests)
        if (maxRequestsPerDay.HasValue || maxRequestsPerHour.HasValue)
        {
            var quota = new { maxRequestsPerDay, maxRequestsPerHour };
            entity.Metadata = System.Text.Json.JsonSerializer.Serialize(quota);
        }
        db.ApiKeys.Add(entity);
        await db.SaveChangesAsync();

        // Track for automatic cleanup
        TrackApiKeyId(entity.Id);

        // Note: Quota fields (maxRequestsPerDay, maxRequestsPerHour) are not yet implemented
        // They will be added in a future iteration of the API key management feature

        return (plaintextKey, entity);
    }

    private async Task RevokeTestApiKeyAsync(string keyId, string userId)
    {
        using var scope = Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<Api.Services.ApiKeyAuthenticationService>();
        await service.RevokeApiKeyAsync(keyId, userId);
    }

    #endregion
}
