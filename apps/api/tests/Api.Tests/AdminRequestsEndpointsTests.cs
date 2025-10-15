using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for GET /admin/requests endpoint.
///
/// Feature: Admin request logs retrieval with filtering
/// As an admin user
/// I want to view filtered AI request logs with metadata
/// So that I can audit system usage and troubleshoot issues
/// </summary>
[Collection("Admin Endpoints")]
public class AdminRequestsEndpointsTests : AdminTestFixture
{
    public AdminRequestsEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Admin retrieves filtered request logs with complete metadata
    ///   Given admin user is authenticated
    ///   And system has AI request logs for multiple users and games
    ///   When admin requests /admin/requests with userId, gameId, and date filters
    ///   Then system returns filtered request logs
    ///   And each log includes endpoint, userAgent, ipAddress, model, finishReason
    ///   And each log includes status, tokenCount, promptTokens, completionTokens
    ///   And successful requests show "Success" status
    ///   And failed requests include errorMessage
    /// </summary>
    [Fact]
    public async Task GetAdminRequests_WhenAdminAuthenticatedWithFilters_ReturnsFilteredLogsWithMetadata()
    {
        // Given: Admin user is authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-requests-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // And: System has request logs for multiple users
        var editorEmail = $"editor-requests-{Guid.NewGuid():N}@example.com";
        using var editorClient = CreateClientWithoutCookies();
        await RegisterAndAuthenticateAsync(editorClient, editorEmail, "Editor");
        var editorUserId = await GetUserIdByEmailAsync(editorEmail);

        var seedContext = await SeedDashboardDataAsync(adminUserId, editorUserId);

        // When: Admin requests logs with filters
        var requestUri = $"/api/v1/admin/requests?limit=10&offset=0&userId={adminUserId}&gameId=game-1" +
                         $"&startDate={Uri.EscapeDataString(seedContext.StartDate.ToString("O"))}" +
                         $"&endDate={Uri.EscapeDataString(seedContext.EndDate.ToString("O"))}";

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("requests", out var requestsElement));
        Assert.Equal(JsonValueKind.Array, requestsElement.ValueKind);

        // And: Response includes totalCount
        Assert.True(document.RootElement.TryGetProperty("totalCount", out var totalCountElement));
        Assert.Equal(2, totalCountElement.GetInt32());

        // And: Response includes exactly 2 filtered logs (admin's logs for game-1)
        Assert.Equal(2, requestsElement.GetArrayLength());

        var endpoints = requestsElement
            .EnumerateArray()
            .Select(element => element.GetProperty("endpoint").GetString())
            .ToList();

        Assert.Contains("qa", endpoints);
        Assert.Contains("setup", endpoints);

        // And: Successful request includes complete metadata
        var qaLog = requestsElement
            .EnumerateArray()
            .Single(element => element.GetProperty("endpoint").GetString() == "qa");

        Assert.Equal("integration-test/1.0", qaLog.GetProperty("userAgent").GetString());
        Assert.Equal("127.0.0.1", qaLog.GetProperty("ipAddress").GetString());
        Assert.Equal("gpt-4", qaLog.GetProperty("model").GetString());
        Assert.Equal("stop", qaLog.GetProperty("finishReason").GetString());
        Assert.Equal("Success", qaLog.GetProperty("status").GetString());
        Assert.Equal(50, qaLog.GetProperty("tokenCount").GetInt32());
        Assert.Equal(30, qaLog.GetProperty("promptTokens").GetInt32());
        Assert.Equal(20, qaLog.GetProperty("completionTokens").GetInt32());

        // And: Failed request includes error details
        var setupLog = requestsElement
            .EnumerateArray()
            .Single(element => element.GetProperty("endpoint").GetString() == "setup");

        Assert.Equal("Error", setupLog.GetProperty("status").GetString());
        Assert.Equal("timeout", setupLog.GetProperty("errorMessage").GetString());
    }

    /// <summary>
    /// Scenario: Non-admin user attempts to access request logs
    ///   Given user is authenticated with Editor role
    ///   When user requests /admin/requests
    ///   Then system returns HTTP 403 Forbidden
    /// </summary>
    [Theory]
    [InlineData("Editor")]
    [InlineData("User")]
    public async Task GetAdminRequests_WhenNonAdminRole_ReturnsForbidden(string role)
    {
        // Given: User authenticated with non-admin role
        using var nonAdminClient = Factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-requests-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(nonAdminClient, email, role);

        // When: User requests admin request logs
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/requests");
        AddCookies(request, cookies);

        var response = await nonAdminClient.SendAsync(request);

        // Then: System denies access
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Anonymous user attempts to access request logs
    ///   Given user is not authenticated
    ///   When user requests /admin/requests
    ///   Then system returns HTTP 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task GetAdminRequests_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Given: User is not authenticated
        using var client = Factory.CreateHttpsClient();

        // When: Anonymous user requests admin logs
        var response = await client.GetAsync("/api/v1/admin/requests");

        // Then: System requires authentication
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
