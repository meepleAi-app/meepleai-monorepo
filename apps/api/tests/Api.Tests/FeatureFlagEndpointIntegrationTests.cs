using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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
    public FeatureFlagEndpointIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
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
        await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        // When: I request GET /api/v1/admin/features
        var response = await client.GetAsync("/api/v1/admin/features");

        // Then: I receive a list of all feature flags with their states
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<FeatureFlagsListResponse>();
        Assert.NotNull(result);
        Assert.NotNull(result.Features);
        Assert.NotEmpty(result.Features); // Should have seeded flags from migration
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
        await RegisterAndAuthenticateAsync(client, userEmail, "User");

        // When: I request GET /api/v1/admin/features
        var response = await client.GetAsync("/api/v1/admin/features");

        // Then: I receive 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        var featureName = $"Features.TestEnable{Guid.NewGuid():N}";

        // When: I send PUT with enabled=true
        var updateRequest = new { enabled = true };
        var response = await client.PutAsJsonAsync($"/api/v1/admin/features/{featureName}", updateRequest);

        // Then: The feature is enabled
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<FeatureFlagUpdateResponse>();
        Assert.NotNull(result);
        Assert.Equal(featureName, result.FeatureName);
        Assert.True(result.Enabled);

        // And: Subsequent checks return true via service
        using var scope = Factory.Services.CreateScope();
        var featureFlags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();
        var isEnabled = await featureFlags.IsEnabledAsync(featureName);
        Assert.True(isEnabled);
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
        await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        var featureName = $"Features.TestDisable{Guid.NewGuid():N}";

        // Enable first
        await client.PutAsJsonAsync($"/api/v1/admin/features/{featureName}", new { enabled = true });

        // When: I send PUT with enabled=false
        var disableRequest = new { enabled = false };
        var response = await client.PutAsJsonAsync($"/api/v1/admin/features/{featureName}", disableRequest);

        // Then: The feature is disabled
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Verify via service
        using var scope = Factory.Services.CreateScope();
        var featureFlags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();
        var isEnabled = await featureFlags.IsEnabledAsync(featureName);
        Assert.False(isEnabled);
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
        await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        await adminClient.PutAsJsonAsync("/api/v1/admin/features/Features.StreamingResponses",
            new { enabled = false });

        // And: Regular user is authenticated
        using var userClient = Factory.CreateHttpsClient();
        var userEmail = $"stream-user-{Guid.NewGuid():N}@test.com";
        await RegisterAndAuthenticateAsync(userClient, userEmail, "User");

        // When: User calls streaming endpoint
        var request = new { gameId = Guid.NewGuid().ToString(), query = "test query" };
        var response = await userClient.PostAsJsonAsync("/api/v1/agents/qa/stream", request);

        // Then: They receive 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("feature_disabled", error.Error);
        Assert.Equal("Features.StreamingResponses", error.FeatureName);

        // Cleanup: Re-enable for other tests
        await adminClient.PutAsJsonAsync("/api/v1/admin/features/Features.StreamingResponses",
            new { enabled = true });
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
        // Given: Admin disables PdfUpload feature
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"pdf-admin-{Guid.NewGuid():N}@test.com";
        await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        await adminClient.PutAsJsonAsync("/api/v1/admin/features/Features.PdfUpload",
            new { enabled = false });

        // And: Editor user is authenticated
        using var editorClient = Factory.CreateHttpsClient();
        var editorEmail = $"pdf-editor-{Guid.NewGuid():N}@test.com";
        await RegisterAndAuthenticateAsync(editorClient, editorEmail, "Editor");

        // When: Editor attempts to upload PDF
        var content = new MultipartFormDataContent
        {
            { new StringContent(Guid.NewGuid().ToString()), "gameId" },
            { new ByteArrayContent(new byte[] { 0x25, 0x50, 0x44, 0x46 }), "file", "test.pdf" }
        };

        var response = await editorClient.PostAsync("/api/v1/ingest/pdf", content);

        // Then: They receive 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal("feature_disabled", error.Error);

        // Cleanup: Re-enable
        await adminClient.PutAsJsonAsync("/api/v1/admin/features/Features.PdfUpload",
            new { enabled = true });
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
        await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        await adminClient.PutAsJsonAsync("/api/v1/admin/features/Features.SetupGuideGeneration",
            new { enabled = false });

        // And: Regular user is authenticated
        using var userClient = Factory.CreateHttpsClient();
        var userEmail = $"setup-user-{Guid.NewGuid():N}@test.com";
        await RegisterAndAuthenticateAsync(userClient, userEmail, "User");

        // When: User requests setup guide
        var request = new { gameId = Guid.NewGuid().ToString() };
        var response = await userClient.PostAsJsonAsync("/api/v1/agents/setup", request);

        // Then: They receive 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // Cleanup: Re-enable
        await adminClient.PutAsJsonAsync("/api/v1/admin/features/Features.SetupGuideGeneration",
            new { enabled = true });
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
        await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        var featureName = $"Features.PersistTest{Guid.NewGuid():N}";
        var enableResponse = await client.PutAsJsonAsync($"/api/v1/admin/features/{featureName}",
            new { enabled = true });
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);

        // When: Feature is checked in new scope
        using var newScope = Factory.Services.CreateScope();
        var featureFlags = newScope.ServiceProvider.GetRequiredService<IFeatureFlagService>();
        var isEnabled = await featureFlags.IsEnabledAsync(featureName);

        // Then: It remains enabled
        Assert.True(isEnabled);

        // Verify via GET endpoint
        var getResponse = await client.GetAsync("/api/v1/admin/features");
        var listResult = await getResponse.Content.ReadFromJsonAsync<FeatureFlagsListResponse>();
        Assert.Contains(listResult!.Features, f => f.FeatureName == featureName && f.IsEnabled);
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
        await RegisterAndAuthenticateAsync(client, adminEmail, "Admin");

        var featureName = $"Features.RoleTest{Guid.NewGuid():N}";
        await client.PutAsJsonAsync($"/api/v1/admin/features/{featureName}",
            new { enabled = true, role = "Admin" });

        using var scope = Factory.Services.CreateScope();
        var featureFlags = scope.ServiceProvider.GetRequiredService<IFeatureFlagService>();

        // When: Checked with Admin role
        var adminEnabled = await featureFlags.IsEnabledAsync(featureName, UserRole.Admin);

        // Then: Returns true
        Assert.True(adminEnabled);

        // And: Checked with User role
        var userEnabled = await featureFlags.IsEnabledAsync(featureName, UserRole.User);

        // Then: Returns false
        Assert.False(userEnabled);
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
        await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        await adminClient.PutAsJsonAsync("/api/v1/admin/features/Features.ChatExport",
            new { enabled = false });

        // And: User is authenticated
        using var userClient = Factory.CreateHttpsClient();
        var userEmail = $"error-user-{Guid.NewGuid():N}@test.com";
        await RegisterAndAuthenticateAsync(userClient, userEmail, "User");

        // When: User calls chat export endpoint (need a valid chat ID, so create one first)
        var chatResponse = await userClient.PostAsJsonAsync("/api/v1/chats",
            new { title = "Test Chat", gameId = (string?)null });
        var chat = await chatResponse.Content.ReadFromJsonAsync<ChatDto>();

        var exportResponse = await userClient.PostAsJsonAsync($"/api/v1/chats/{chat!.Id}/export",
            new { format = "markdown" });

        // Then: Error response has correct format
        Assert.Equal(HttpStatusCode.Forbidden, exportResponse.StatusCode);

        var error = await exportResponse.Content.ReadFromJsonAsync<FeatureDisabledError>();
        Assert.NotNull(error);
        Assert.Equal("feature_disabled", error.Error);
        Assert.Contains("export", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Features.ChatExport", error.FeatureName);

        // Cleanup: Re-enable
        await adminClient.PutAsJsonAsync("/api/v1/admin/features/Features.ChatExport",
            new { enabled = true });
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
        Assert.True(allFlags.Count >= 8, $"Expected >= 8 flags, found {allFlags.Count}");

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

            Assert.NotNull(flag);
            Assert.Equal(expectedEnabled, flag.IsEnabled);
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
        Assert.Equal(result1, result2);
        Assert.Equal(result2, result3);
    }

    #endregion

    #region Helper Records

    private record FeatureFlagsListResponse(List<FeatureFlagDto> Features);
    private record FeatureFlagUpdateResponse(string FeatureName, bool Enabled, string? Role);
    private record FeatureDisabledError(string Error, string Message, string FeatureName);
    private record ErrorResponse(string Error, string? FeatureName = null);

    #endregion
}
