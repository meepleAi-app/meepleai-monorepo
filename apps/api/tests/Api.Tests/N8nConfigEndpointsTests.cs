using Api.Tests.Fixtures;
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
using FluentAssertions;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    public N8nConfigEndpointsTests(PostgresCollectionFixture postgresFixture, WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(postgresFixture, factory)
    {
        _output = output;
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
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/n8n")
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<N8nConfigDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Primary Workflow");
        dto.BaseUrl.Should().Be("https://n8n.local");
        dto.WebhookUrl.Should().Be("https://n8n.local/webhook");

        // And: API key is encrypted in database
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var entity = await db.N8nConfigs.SingleAsync(c => c.Id == dto.Id);
        entity.CreatedByUserId.Should().Be(adminUserId);
        entity.BaseUrl.Should().Be("https://n8n.local");
        string.IsNullOrWhiteSpace(entity.ApiKeyEncrypted).Should().BeFalse();
        entity.ApiKeyEncrypted.Should().NotBe("test-api-key");

        // And: Config appears in list endpoint
        using var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/n8n");
        AddCookies(listRequest, cookies);
        var listResponse = await adminClient.SendAsync(listRequest);

        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var configs = listDocument.RootElement.GetProperty("configs");
        configs.EnumerateArray().Should().Contain(element => element.GetProperty("id").GetString() == dto.Id);
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
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/n8n/{config.Id}");
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns config successfully
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<N8nConfigDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(config.Id);
        dto.Name.Should().Be(config.Name);
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
        using var updateRequest = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/admin/n8n/{existing.Id}")
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
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var dto = await response.Content.ReadFromJsonAsync<N8nConfigDto>(JsonOptions);
        dto.Should().NotBeNull();
        dto!.Name.Should().Be("Updated Workflow");
        dto.BaseUrl.Should().Be("https://n8n.updated");
        dto.WebhookUrl.Should().Be("https://n8n.updated/webhook");
        dto.IsActive.Should().BeFalse();

        // And: API key is rotated and re-encrypted
        await using (var verifyScope = Factory.Services.CreateAsyncScope())
        {
            var db = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.N8nConfigs.SingleAsync(c => c.Id == existing.Id);
            entity.Name.Should().Be("Updated Workflow");
            entity.BaseUrl.Should().Be("https://n8n.updated");
            entity.WebhookUrl.Should().Be("https://n8n.updated/webhook");
            entity.IsActive.Should().BeFalse();
            entity.ApiKeyEncrypted.Should().NotBe(originalEncryptedKey);
            (entity.UpdatedAt > entity.CreatedAt).Should().BeTrue();
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
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/n8n/non-existent-config");
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns 404 with error message
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        document.RootElement.GetProperty("error").GetString().Should().Be("Configuration not found");
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
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/admin/n8n/{config.Id}/test");
        AddCookies(request, cookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns successful test result
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<N8nTestResult>(JsonOptions);
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        result.Message.Should().StartWith("Connection successful");
        result.LatencyMs.HasValue.Should().BeTrue();

        // And: Test metadata is persisted
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.N8nConfigs.SingleAsync(c => c.Id == config.Id);
            entity.LastTestedAt.Should().NotBeNull();
            string.IsNullOrWhiteSpace(entity.LastTestResult).Should().BeFalse();
            entity.LastTestResult.Should().StartWith("Connection successful");
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
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/n8n")
        {
            Content = JsonContent.Create(new CreateN8nConfigRequest("Forbidden", "https://n8n.invalid", "key", null))
        };
        AddCookies(request, cookies);

        var response = await nonAdminClient.SendAsync(request);

        // Then: System denies access
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
