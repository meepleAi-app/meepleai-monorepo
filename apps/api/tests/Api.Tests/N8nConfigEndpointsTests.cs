using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for /admin/n8n endpoints (CRUD operations).
///
/// Feature: N8n workflow configuration management
/// As an admin user
/// I want to create, read, update, and delete n8n configurations
/// So that I can manage workflow integrations and API credentials
/// </summary>
[Collection("Admin Endpoints")]
public class N8nConfigEndpointsTests : AdminTestFixture
{
    public N8nConfigEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Admin creates new n8n configuration with encrypted credentials
    ///   Given admin user is authenticated
    ///   When admin posts new n8n config with name, baseUrl, apiKey, and webhookUrl
    ///   Then system creates configuration
    ///   And system returns HTTP 200 OK with created config DTO
    ///   And API key is stored encrypted in database
    ///   And config is listed in GET /admin/n8n response
    /// </summary>
    [Fact]
    public async Task PostAdminN8n_WhenAdminCreatesConfig_PersistsEncryptedKeyAndReturnsDto()
    {
        // Given: Clean state and authenticated admin
        await ClearN8nConfigsAsync();

        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-create-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // When: Admin creates n8n configuration
        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/n8n")
        {
            Content = JsonContent.Create(new CreateN8nConfigRequest(
                "Primary Workflow",
                "https://n8n.local/",
                "test-api-key",
                "https://n8n.local/webhook"))
        };
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        // Then: System creates config successfully
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<N8nConfigDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal("Primary Workflow", dto!.Name);
        Assert.Equal("https://n8n.local", dto.BaseUrl);
        Assert.Equal("https://n8n.local/webhook", dto.WebhookUrl);

        // And: API key is encrypted in database
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var entity = await db.N8nConfigs.SingleAsync(c => c.Id == dto.Id);
        Assert.Equal(adminUserId, entity.CreatedByUserId);
        Assert.Equal("https://n8n.local", entity.BaseUrl);
        Assert.False(string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted));
        Assert.NotEqual("test-api-key", entity.ApiKeyEncrypted);

        // And: Config appears in list endpoint
        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/admin/n8n");
        AddCookies(listRequest, cookies);
        var listResponse = await adminClient.SendAsync(listRequest);

        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var configs = listDocument.RootElement.GetProperty("configs");
        Assert.Contains(configs.EnumerateArray(), element => element.GetProperty("id").GetString() == dto.Id);
    }

    /// <summary>
    /// Scenario: Admin retrieves existing n8n configuration by ID
    ///   Given admin user is authenticated
    ///   And n8n configuration exists in database
    ///   When admin requests GET /admin/n8n/{id}
    ///   Then system returns HTTP 200 OK
    ///   And response includes complete config DTO
    /// </summary>
    [Fact]
    public async Task GetAdminN8nById_WhenConfigExists_ReturnsPersistedConfig()
    {
        // Given: Clean state and authenticated admin
        await ClearN8nConfigsAsync();

        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-get-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // And: N8n configuration exists
        var config = await CreateN8nConfigAsync(adminUserId, "Existing Workflow");

        // When: Admin requests config by ID
        var request = new HttpRequestMessage(HttpMethod.Get, $"/admin/n8n/{config.Id}");
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns config successfully
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<N8nConfigDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal(config.Id, dto!.Id);
        Assert.Equal(config.Name, dto.Name);
    }

    /// <summary>
    /// Scenario: Admin updates n8n configuration and rotates API key
    ///   Given admin user is authenticated
    ///   And n8n configuration exists
    ///   When admin updates config with new name, baseUrl, apiKey, webhookUrl, and isActive
    ///   Then system updates configuration
    ///   And system returns HTTP 200 OK with updated DTO
    ///   And API key is re-encrypted with new value
    ///   And UpdatedAt timestamp is after CreatedAt
    /// </summary>
    [Fact]
    public async Task PutAdminN8n_WhenAdminUpdatesConfig_RotatesApiKeyAndPersistsChanges()
    {
        // Given: Clean state and authenticated admin
        await ClearN8nConfigsAsync();

        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-update-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // And: Existing n8n configuration
        var existing = await CreateN8nConfigAsync(adminUserId, "Workflow To Update");

        string originalEncryptedKey;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            originalEncryptedKey = (await db.N8nConfigs.SingleAsync(c => c.Id == existing.Id)).ApiKeyEncrypted;
        }

        // When: Admin updates configuration
        var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/admin/n8n/{existing.Id}")
        {
            Content = JsonContent.Create(new UpdateN8nConfigRequest(
                "Updated Workflow",
                "https://n8n.updated/",
                "rotated-api-key",
                "https://n8n.updated/webhook",
                false))
        };
        AddCookies(updateRequest, cookies);

        var response = await adminClient.SendAsync(updateRequest);

        // Then: System updates config successfully
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<N8nConfigDto>(JsonOptions);
        Assert.NotNull(dto);
        Assert.Equal("Updated Workflow", dto!.Name);
        Assert.Equal("https://n8n.updated", dto.BaseUrl);
        Assert.Equal("https://n8n.updated/webhook", dto.WebhookUrl);
        Assert.False(dto.IsActive);

        // And: API key is rotated and re-encrypted
        await using (var verifyScope = Factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.N8nConfigs.SingleAsync(c => c.Id == existing.Id);
            Assert.Equal("Updated Workflow", entity.Name);
            Assert.Equal("https://n8n.updated", entity.BaseUrl);
            Assert.Equal("https://n8n.updated/webhook", entity.WebhookUrl);
            Assert.False(entity.IsActive);
            Assert.NotEqual(originalEncryptedKey, entity.ApiKeyEncrypted);
            Assert.True(entity.UpdatedAt > entity.CreatedAt);
        }
    }

    /// <summary>
    /// Scenario: Admin attempts to delete non-existent configuration
    ///   Given admin user is authenticated
    ///   And configuration ID does not exist
    ///   When admin requests DELETE /admin/n8n/{non-existent-id}
    ///   Then system returns HTTP 404 Not Found
    ///   And error message is "Configuration not found"
    /// </summary>
    [Fact]
    public async Task DeleteAdminN8n_WhenConfigNotFound_ReturnsNotFoundError()
    {
        // Given: Authenticated admin
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-delete-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        // When: Admin attempts to delete non-existent config
        var request = new HttpRequestMessage(HttpMethod.Delete, "/admin/n8n/non-existent-config");
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns 404 with error message
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Configuration not found", document.RootElement.GetProperty("error").GetString());
    }

    /// <summary>
    /// Scenario: Admin tests n8n configuration connection
    ///   Given admin user is authenticated
    ///   And n8n configuration exists
    ///   When admin requests POST /admin/n8n/{id}/test
    ///   Then system tests connection
    ///   And system returns HTTP 200 OK with test result
    ///   And result includes success=true and latencyMs
    ///   And config LastTestedAt and LastTestResult are updated
    /// </summary>
    [Fact]
    public async Task PostAdminN8nTestConnection_WhenConnectionSucceeds_UpdatesTestMetadata()
    {
        // Given: Clean state and authenticated admin
        await ClearN8nConfigsAsync();

        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-n8n-test-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // And: N8n configuration exists
        var config = await CreateN8nConfigAsync(adminUserId, "Workflow To Test");

        // When: Admin tests connection
        var request = new HttpRequestMessage(HttpMethod.Post, $"/admin/n8n/{config.Id}/test");
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns successful test result
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<N8nTestResult>(JsonOptions);
        Assert.NotNull(result);
        Assert.True(result!.Success);
        Assert.StartsWith("Connection successful", result.Message, StringComparison.Ordinal);
        Assert.True(result.LatencyMs.HasValue);

        // And: Test metadata is persisted
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.N8nConfigs.SingleAsync(c => c.Id == config.Id);
            Assert.NotNull(entity.LastTestedAt);
            Assert.False(string.IsNullOrWhiteSpace(entity.LastTestResult));
            Assert.StartsWith("Connection successful", entity.LastTestResult, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Scenario: Non-admin user attempts to create n8n configuration
    ///   Given user is authenticated with Editor role
    ///   When user posts to /admin/n8n
    ///   Then system returns HTTP 403 Forbidden
    /// </summary>
    [Theory]
    [InlineData("Editor")]
    [InlineData("User")]
    public async Task PostAdminN8n_WhenNonAdminRole_ReturnsForbidden(string role)
    {
        // Given: User authenticated with non-admin role
        using var nonAdminClient = Factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-n8n-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(nonAdminClient, email, role);

        // When: User attempts to create n8n config
        var request = new HttpRequestMessage(HttpMethod.Post, "/admin/n8n")
        {
            Content = JsonContent.Create(new CreateN8nConfigRequest("Forbidden", "https://n8n.invalid", "key", null))
        };
        AddCookies(request, cookies);

        var response = await nonAdminClient.SendAsync(request);

        // Then: System denies access
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
