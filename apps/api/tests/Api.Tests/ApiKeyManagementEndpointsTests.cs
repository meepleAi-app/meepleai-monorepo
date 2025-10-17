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

namespace Api.Tests;

/// <summary>
/// Integration tests for API key management endpoints.
/// Tests the complete flow through HTTP endpoints, middleware, and services.
/// Related to Issue #259 - API-04: API Key Management and Quota System.
/// </summary>
public class ApiKeyManagementEndpointsTests : IntegrationTestBase
{
    public ApiKeyManagementEndpointsTests(WebApplicationFactoryFixture fixture) : base(fixture)
    {
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.PlaintextKey);
        Assert.StartsWith("mpl_live_", result.PlaintextKey);
        Assert.Equal("Production Key", result.ApiKey.KeyName);
        Assert.Equal(2, result.ApiKey.Scopes.Length);
        Assert.True(result.ApiKey.IsActive);
        Assert.NotNull(result.ApiKey.Quota);
        Assert.Equal(1000, result.ApiKey.Quota.MaxRequestsPerDay);
        Assert.Equal(100, result.ApiKey.Quota.MaxRequestsPerHour);
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
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateApiKeyResponse>();
        Assert.NotNull(result);
        Assert.StartsWith($"mpl_{environment}_", result.PlaintextKey);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyListResponse>();
        Assert.NotNull(result);
        Assert.Equal(2, result.Keys.Count);
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Page);
        Assert.Equal(2, result.PageSize);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyListResponse>();
        Assert.NotNull(result);
        Assert.Single(result.Keys);
        Assert.Null(result.Keys[0].RevokedAt);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyListResponse>();
        Assert.NotNull(result);
        Assert.Single(result.Keys);
        Assert.NotNull(result.Keys[0].RevokedAt);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyListResponse>();
        Assert.NotNull(result);
        Assert.Single(result.Keys);
        Assert.Equal("User1 Key", result.Keys[0].KeyName);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyDto>();
        Assert.NotNull(result);
        Assert.Equal(key.Id, result.Id);
        Assert.Equal("Test Key", result.KeyName);
        Assert.Equal(2, result.Scopes.Length);
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
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyDto>();
        Assert.NotNull(result);
        Assert.Equal("Updated Name", result.KeyName);
        Assert.Equal(3, result.Scopes.Length);
        Assert.Contains("admin", result.Scopes);
        Assert.NotNull(result.Quota);
        Assert.Equal(5000, result.Quota.MaxRequestsPerDay);
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
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<RotateApiKeyResponse>();
        Assert.NotNull(result);
        Assert.NotEqual(oldPlaintextKey, result.PlaintextKey);
        Assert.Equal(oldKey.Id, result.RevokedKeyId);
        Assert.Equal("Original Key (Rotated)", result.NewApiKey.KeyName);
        Assert.Equal(2, result.NewApiKey.Scopes.Length);

        // Verify old key cannot be used
        var testRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        testRequest.Headers.Add("X-API-Key", oldPlaintextKey);
        var testResponse = await client.SendAsync(testRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, testResponse.StatusCode);

        // Verify new key works
        var newTestRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        newTestRequest.Headers.Add("X-API-Key", result.PlaintextKey);
        var newTestResponse = await client.SendAsync(newTestRequest);
        Assert.Equal(HttpStatusCode.OK, newTestResponse.StatusCode);
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
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify key cannot be used
        var testRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        testRequest.Headers.Add("X-API-Key", plaintextKey);
        var testResponse = await client.SendAsync(testRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, testResponse.StatusCode);
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
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ApiKeyQuotaDto>();
        Assert.NotNull(result);
        Assert.Equal(1000, result.MaxRequestsPerDay);
        Assert.Equal(100, result.MaxRequestsPerHour);
        Assert.NotNull(result.ResetsAt);
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
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
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
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Helper Methods

    private async Task<(string PlaintextKey, Infrastructure.Entities.ApiKeyEntity Entity)> CreateTestApiKeyAsync(
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
