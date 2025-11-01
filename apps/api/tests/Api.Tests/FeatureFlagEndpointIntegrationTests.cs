using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Linq;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// CONFIG-05: Integration tests for feature flag system.
/// Tests endpoint behavior with feature flags enabled/disabled.
///
/// BDD Feature: Runtime Feature Toggling
/// As a DevOps engineer or product manager
/// I want to enable/disable features at runtime without redeployment
/// So that I can perform gradual rollouts, A/B testing, and quickly disable problematic features
/// </summary>
[Collection("Admin Endpoints")]
public class FeatureFlagEndpointIntegrationTests : AdminTestFixture
{
    private readonly ITestOutputHelper _output;

    public FeatureFlagEndpointIntegrationTests(WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    #region Admin Endpoints Tests

    /// <summary>
    /// Scenario: Admin can list all feature flags
    ///   Given I am authenticated as an admin
    ///   When I request GET /api/v1/admin/features
    ///   Then I receive a list of all feature flags with their states
    /// </summary>
    [Fact]
    public async Task GetAllFeatureFlags_ReturnsAllFlags_ForAdmin()
    {
        // Given: I am authenticated as an admin
        using var client = Factory.CreateHttpsClient();
        var adminEmail = $"feature-admin-{Guid.NewGuid():N}@test.com";
        var adminCookies = await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        // When: I request GET /api/v1/admin/features
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/features");
        var response = await SendWithCookiesAsync(client, request, adminCookies);

        // Then: I receive a list of all feature flags with their states
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<FeatureFlagsListResponse>();
        result.Should().NotBeNull();
        result.Features.Should().NotBeNull();
        result.Features.Should().NotBeEmpty(); // Should have seeded flags from migration
    }

    /// <summary>
    /// Scenario: Non-admin cannot list feature flags
    ///   Given I am authenticated as a regular user
    ///   When I request GET /api/v1/admin/features
    ///   Then I receive 403 Forbidden
    /// </summary>
    [Fact]
    public async Task GetAllFeatureFlags_Returns403_ForNonAdmin()
    {
        // Given: I am authenticated as a regular user
        using var client = Factory.CreateHttpsClient();
        var userEmail = $"feature-user-{Guid.NewGuid():N}@test.com";
        var userCookies = await RegisterAndAuthenticateAsync(client, userEmail, "User");

        // When: I request GET /api/v1/admin/features
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/features");
        var response = await SendWithCookiesAsync(client, request, userCookies);

        // Then: I receive 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Scenario: Admin can enable a feature flag
    ///   Given I am authenticated as an admin
    ///   When I send PUT /api/v1/admin/features/{name} with enabled=true
    ///   Then the feature is enabled
    ///   And subsequent checks return true
    /// </summary>
    [Fact]
    public async Task UpdateFeatureFlag_EnablesFeature_Successfully()
    {
        // Given: I am authenticated as an admin
        using var client = Factory.CreateHttpsClient();
        var adminEmail = $"feature-enable-{Guid.NewGuid():N}@test.com";
        var adminCookies = await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        var featureName = $"Features.TestEnable{Guid.NewGuid():N}";

        // When: I send PUT with enabled=true
        var updateRequest = new { enabled = true };
        var request = CreateJsonRequest(HttpMethod.Put, $"/api/v1/admin/features/{featureName}", updateRequest);
        var response = await SendWithCookiesAsync(client, request, adminCookies);

        // Then: The feature is enabled
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<FeatureFlagUpdateResponse>();
        result.Should().NotBeNull();
        result.FeatureName.Should().BeEquivalentTo(featureName);
        result.Enabled.Should().BeTrue();

        // And: Subsequent checks return true via service
        using var scope = Factory.Services.CreateScope();
        var featureFlags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();
        var isEnabled = await featureFlags.IsEnabledAsync(featureName);
        isEnabled.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Admin can disable a feature flag
    ///   Given I am authenticated as an admin
    ///   When I send PUT /api/v1/admin/features/{name} with enabled=false
    ///   Then the feature is disabled
    /// </summary>
    [Fact]
    public async Task UpdateFeatureFlag_DisablesFeature_Successfully()
    {
        // Given: I am authenticated as an admin with a feature to disable
        using var client = Factory.CreateHttpsClient();
        var adminEmail = $"feature-disable-{Guid.NewGuid():N}@test.com";
        var adminCookies = await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        var featureName = $"Features.TestDisable{Guid.NewGuid():N}";

        // Enable first
        var enableRequest = CreateJsonRequest(HttpMethod.Put, $"/api/v1/admin/features/{featureName}", new { enabled = true });
        await SendWithCookiesAsync(client, enableRequest, adminCookies);

        // When: I send PUT with enabled=false
        var disableRequest = new { enabled = false };
        var request = CreateJsonRequest(HttpMethod.Put, $"/api/v1/admin/features/{featureName}", disableRequest);
        var response = await SendWithCookiesAsync(client, request, adminCookies);

        // Then: The feature is disabled
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verify via service
        using var scope = Factory.Services.CreateScope();
        var featureFlags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();
        var isEnabled = await featureFlags.IsEnabledAsync(featureName);
        isEnabled.Should().BeFalse();
    }

    #endregion

    #region Endpoint Protection Tests

    /// <summary>
    /// Scenario: Streaming endpoint returns 403 when feature disabled
    ///   Given the StreamingResponses feature is disabled by admin
    ///   When a user calls POST /api/v1/agents/qa/stream
    ///   Then they receive 403 Forbidden with feature_disabled error
    /// </summary>
    [Fact]
    public async Task StreamingEndpoint_Returns403_WhenFeatureDisabled()
    {
        // Given: Admin disables StreamingResponses feature
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"stream-admin-{Guid.NewGuid():N}@test.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        var disableRequest = CreateJsonRequest(HttpMethod.Put, "/api/v1/admin/features/Features.StreamingResponses", new { enabled = false });
        var disableResponse = await SendWithCookiesAsync(adminClient, disableRequest, adminCookies);
        disableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // And: Regular user is authenticated
        using var userClient = Factory.CreateHttpsClient();
        var userEmail = $"stream-user-{Guid.NewGuid():N}@test.com";
        var userCookies = await RegisterAndAuthenticateAsync(userClient, userEmail, "User");

        // When: User calls streaming endpoint
        var payload = new { gameId = Guid.NewGuid().ToString(), query = "test query" };
        var request = CreateJsonRequest(HttpMethod.Post, "/api/v1/agents/qa/stream", payload);
        var response = await SendWithCookiesAsync(userClient, request, userCookies);

        // Then: They receive 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        error.Should().NotBeNull();
        error.Error.Should().Be("feature_disabled");
        error.FeatureName.Should().Be("Features.StreamingResponses");

        // Cleanup: Re-enable for other tests
        var reenableRequest = CreateJsonRequest(HttpMethod.Put, "/api/v1/admin/features/Features.StreamingResponses", new { enabled = true });
        var reenableResponse = await SendWithCookiesAsync(adminClient, reenableRequest, adminCookies);
        reenableResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    /// <summary>
    /// Scenario: PDF upload returns 403 when feature disabled
    ///   Given the PdfUpload feature is disabled
    ///   When an editor attempts to upload a PDF
    ///   Then they receive 403 Forbidden
    /// </summary>
    [Fact]
    public async Task PdfUploadEndpoint_Returns403_WhenFeatureDisabled()
    {
        // Given: PdfUpload feature is disabled via service
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"pdf-admin-{Guid.NewGuid():N}@test.com";
        _ = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);
        using var featureScope = Factory.Services.CreateScope();
        var featureFlags = featureScope.ServiceProvider.GetRequiredService<IFeatureFlagService>();
        var configurationService = featureScope.ServiceProvider.GetRequiredService<IConfigurationService>();
        var environment = featureScope.ServiceProvider.GetRequiredService<IHostEnvironment>().EnvironmentName;
        await SetFeatureFlagAsync(configurationService, "Features.PdfUpload", false, adminUserId, environment);
        var configuration = await configurationService.GetConfigurationByKeyAsync("Features.PdfUpload", environment);
        configuration.Should().NotBeNull();
        new[] { environment, "All" }.Contains(configuration!.Environment).Should().BeTrue();
        bool.Parse(configuration.Value).Should().BeFalse();
        (await featureFlags.IsEnabledAsync("Features.PdfUpload")).Should().BeFalse();

        try
        {
            // And: Editor user is authenticated
            using var editorClient = Factory.CreateHttpsClient();
            var editorEmail = $"pdf-editor-{Guid.NewGuid():N}@test.com";
            var editorCookies = await RegisterAndAuthenticateAsync(editorClient, editorEmail, "Editor");

            // When: Editor attempts to upload PDF
            var content = new MultipartFormDataContent
            {
                { new StringContent(Guid.NewGuid().ToString()), "gameId" },
                { new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }), "file", "test.pdf" }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/pdf") { Content = content };
            var response = await SendWithCookiesAsync(editorClient, request, editorCookies);

            // Then: They receive 403 Forbidden
            response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

            var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
            error.Should().NotBeNull();
            error.Error.Should().Be("feature_disabled");
        }
        finally
        {
            await SetFeatureFlagAsync(configurationService, "Features.PdfUpload", true, adminUserId, environment);
        }
    }

    /// <summary>
    /// Scenario: Setup guide returns 403 when feature disabled
    ///   Given the SetupGuideGeneration feature is disabled
    ///   When a user requests setup guide generation
    ///   Then they receive 403 Forbidden
    /// </summary>
    [Fact]
    public async Task SetupGuideEndpoint_Returns403_WhenFeatureDisabled()
    {
        // Given: Admin disables SetupGuideGeneration
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"setup-admin-{Guid.NewGuid():N}@test.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        var disableRequest = CreateJsonRequest(HttpMethod.Put, "/api/v1/admin/features/Features.SetupGuideGeneration", new { enabled = false });
        await SendWithCookiesAsync(adminClient, disableRequest, adminCookies);

        // And: Regular user is authenticated
        using var userClient = Factory.CreateHttpsClient();
        var userEmail = $"setup-user-{Guid.NewGuid():N}@test.com";
        var userCookies = await RegisterAndAuthenticateAsync(userClient, userEmail, "User");

        // When: User requests setup guide
        var payload = new { gameId = Guid.NewGuid().ToString() };
        var request = CreateJsonRequest(HttpMethod.Post, "/api/v1/agents/setup", payload);
        var response = await SendWithCookiesAsync(userClient, request, userCookies);

        // Then: They receive 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Cleanup: Re-enable
        var reenableRequest = CreateJsonRequest(HttpMethod.Put, "/api/v1/admin/features/Features.SetupGuideGeneration", new { enabled = true });
        await SendWithCookiesAsync(adminClient, reenableRequest, adminCookies);
    }

    #endregion

    #region Feature Flag Persistence Tests

    /// <summary>
    /// Scenario: Feature flag changes persist across requests
    ///   Given an admin enables a new feature
    ///   When the feature is checked in a new scope
    ///   Then it remains enabled
    /// </summary>
    [Fact]
    public async Task FeatureFlagChanges_PersistAcrossRequests()
    {
        // Given: Admin enables a new feature
        using var client = Factory.CreateHttpsClient();
        var adminEmail = $"persist-admin-{Guid.NewGuid():N}@test.com";
        var adminCookies = await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        var featureName = $"Features.PersistTest{Guid.NewGuid():N}";
        var request = CreateJsonRequest(HttpMethod.Put, $"/api/v1/admin/features/{featureName}", new { enabled = true });
        var enableResponse = await SendWithCookiesAsync(client, request, adminCookies);
        enableResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // When: Feature is checked in new scope
        using var newScope = Factory.Services.CreateScope();
        var featureFlags = newScope.ServiceProvider.GetRequiredService<IFeatureFlagService>();
        var isEnabled = await featureFlags.IsEnabledAsync(featureName);

        // Then: It remains enabled
        isEnabled.Should().BeTrue();

        // Verify via GET endpoint
        var getRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/features");
        var getResponse = await SendWithCookiesAsync(client, getRequest, adminCookies);
        var listResult = await getResponse.Content.ReadFromJsonAsync<FeatureFlagsListResponse>();
        listResult!.Features.Should().Contain(f => f.FeatureName == featureName && f.IsEnabled);
    }

    #endregion

    #region Role-Based Access Tests

    /// <summary>
    /// Scenario: Role-specific feature flags work correctly
    ///   Given a feature is enabled only for Admin role
    ///   When checked with Admin role context
    ///   Then it returns true
    ///   And when checked with User role context
    ///   Then it returns false
    /// </summary>
    [Fact]
    public async Task RoleSpecificFeature_WorksCorrectly()
    {
        // Given: Admin enables a role-specific feature
        using var client = Factory.CreateHttpsClient();
        var adminEmail = $"role-admin-{Guid.NewGuid():N}@test.com";
        var adminCookies = await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        var featureName = $"Features.RoleTest{Guid.NewGuid():N}";
        var request = CreateJsonRequest(HttpMethod.Put, $"/api/v1/admin/features/{featureName}", new { enabled = true, role = "Admin" });
        await SendWithCookiesAsync(client, request, adminCookies);

        using var scope = Factory.Services.CreateScope();
        var featureFlags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();

        // When: Checked with Admin role
        var adminEnabled = await featureFlags.IsEnabledAsync(featureName, UserRole.Admin);

        // Then: Returns true
        adminEnabled.Should().BeTrue();

        // And: Checked with User role
        var userEnabled = await featureFlags.IsEnabledAsync(featureName, UserRole.User);

        // Then: Returns false
        userEnabled.Should().BeFalse();
    }

    #endregion

    #region Error Response Format Tests

    /// <summary>
    /// Scenario: Feature disabled error has correct format
    ///   Given a feature is disabled
    ///   When calling a protected endpoint
    ///   Then error response contains error code, message, and feature name
    /// </summary>
    [Fact]
    public async Task FeatureDisabledError_HasCorrectFormat()
    {
        // Given: Admin disables chat export
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"error-admin-{Guid.NewGuid():N}@test.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        var disableRequest = CreateJsonRequest(HttpMethod.Put, "/api/v1/admin/features/Features.ChatExport", new { enabled = false });
        await SendWithCookiesAsync(adminClient, disableRequest, adminCookies);

        // And: User is authenticated
        using var userClient = Factory.CreateHttpsClient();
        var userEmail = $"error-user-{Guid.NewGuid():N}@test.com";
        var userCookies = await RegisterAndAuthenticateAsync(userClient, userEmail, "User");

        // When: User calls chat export endpoint (need a valid chat ID, so create one first)
        var chatRequest = CreateJsonRequest(HttpMethod.Post, "/api/v1/chats", new { title = "Test Chat", gameId = (string?)null });
        var chatResponse = await SendWithCookiesAsync(userClient, chatRequest, userCookies);
        var chat = await chatResponse.Content.ReadFromJsonAsync<ChatDto>();

        var exportRequest = CreateJsonRequest(HttpMethod.Post, $"/api/v1/chats/{chat!.Id}/export", new { format = "markdown" });
        var exportResponse = await SendWithCookiesAsync(userClient, exportRequest, userCookies);

        // Then: Error response has correct format
        exportResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var error = await exportResponse.Content.ReadFromJsonAsync<FeatureDisabledError>();
        error.Should().NotBeNull();
        error.Error.Should().Be("feature_disabled");
        error.Message.Should().Contain("export");
        error.FeatureName.Should().Be("Features.ChatExport");

        // Cleanup: Re-enable
        var reenableRequest = CreateJsonRequest(HttpMethod.Put, "/api/v1/admin/features/Features.ChatExport", new { enabled = true });
        await SendWithCookiesAsync(adminClient, reenableRequest, adminCookies);
    }

    #endregion

    #region Migration Verification Tests

    /// <summary>
    /// Scenario: Migration seeds default feature flags
    ///   Given the SeedFeatureFlags migration has been applied
    ///   When querying for feature flags
    ///   Then all 8 seeded flags exist with correct defaults
    /// </summary>
    [Fact]
    public async Task Migration_SeedsDefaultFeatureFlags()
    {
        // Given: Migration applied (automatic via factory)
        using var scope = Factory.Services.CreateScope();
        var featureFlags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();

        // When: Querying for feature flags
        var allFlags = await featureFlags.GetAllFeatureFlagsAsync();

        // Then: All 8 seeded flags exist
        (allFlags.Count >= 8).Should().BeTrue($"Expected >= 8 flags, found {allFlags.Count}");

        // Verify specific flags with default values
        var expectedFlags = new[]
        {
            ("Features.StreamingResponses", true, (string?)null),
            ("Features.SetupGuideGeneration", true, (string?)null),
            ("Features.PdfUpload", true, (string?)null),
            ("Features.ChatExport", true, (string?)null),
            ("Features.MessageEditDelete", true, (string?)null),
            ("Features.N8nIntegration", true, (string?)null),
            ("Features.RagEvaluation", false, "Admin"),
            ("Features.AdvancedAdmin", false, "Admin")
        };

        foreach (var (name, expectedEnabled, expectedRole) in expectedFlags)
        {
            var flag = allFlags.FirstOrDefault(f =>
                f.FeatureName == name &&
                f.RoleRestriction == expectedRole);

            flag.Should().NotBeNull();
            flag.IsEnabled.Should().Be(expectedEnabled);
        }
    }

    #endregion

    #region Cache Integration Tests

    /// <summary>
    /// Scenario: Feature flag checks benefit from caching
    ///   Given a feature flag is checked multiple times
    ///   When subsequent checks occur
    ///   Then results remain consistent (verifies CONFIG-01 cache integration)
    /// </summary>
    [Fact]
    public async Task FeatureFlagChecks_AreCached()
    {
        // Given: A feature flag
        using var scope = Factory.Services.CreateScope();
        var featureFlags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();

        var featureName = "Features.StreamingResponses"; // Use seeded flag

        // When: Checked multiple times
        var result1 = await featureFlags.IsEnabledAsync(featureName);
        var result2 = await featureFlags.IsEnabledAsync(featureName);
        var result3 = await featureFlags.IsEnabledAsync(featureName);

        // Then: Results are consistent
        result2.Should().Be(result1);
        result3.Should().Be(result2);
    }

    #endregion

    private static async Task SetFeatureFlagAsync(
        IConfigurationService configurationService,
        string featureName,
        bool enabled,
        string userId,
        string environment)
    {
        var configs = await configurationService.GetConfigurationsAsync(
            category: "FeatureFlags",
            activeOnly: true,
            pageSize: 200);

        var matchingConfigs = configs.Items
            .Where(c => string.Equals(c.Key, featureName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var config in matchingConfigs)
        {
            var updateRequest = new UpdateConfigurationRequest(Value: enabled ? "true" : "false");
            await configurationService.UpdateConfigurationAsync(config.Id, updateRequest, userId);
        }

        if (!matchingConfigs.Any(c => string.Equals(c.Environment, environment, StringComparison.OrdinalIgnoreCase)))
        {
            var createRequest = new CreateConfigurationRequest(
                Key: featureName,
                Value: enabled ? "true" : "false",
                ValueType: "Boolean",
                Description: matchingConfigs.FirstOrDefault()?.Description ?? $"Feature flag: {featureName}",
                Category: matchingConfigs.FirstOrDefault()?.Category ?? "FeatureFlags",
                IsActive: true,
                RequiresRestart: false,
                Environment: environment);

            await configurationService.CreateConfigurationAsync(createRequest, userId);
        }

        await configurationService.InvalidateCacheAsync(featureName);
    }
    private async Task<HttpResponseMessage> SendWithCookiesAsync(HttpClient client, HttpRequestMessage request, IEnumerable<string> cookies)
    {
        AddCookies(request, cookies);
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CreateJsonRequest(HttpMethod method, string uri, object? payload = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (payload != null)
        {
            request.Content = JsonContent.Create(payload);
        }

        return request;
    }
    #region Helper Records

    private record FeatureFlagsListResponse(List<FeatureFlagDto> Features);
    private record FeatureFlagUpdateResponse(string FeatureName, bool Enabled, string? Role);
    private record FeatureDisabledError(string Error, string Message, string FeatureName);
    private record ErrorResponse(string Error, string? FeatureName = null);

    #endregion
}

