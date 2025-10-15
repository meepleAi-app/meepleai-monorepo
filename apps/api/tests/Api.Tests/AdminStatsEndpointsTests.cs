using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for GET /admin/stats endpoint.
///
/// Feature: Admin dashboard statistics aggregation
/// As an admin user
/// I want to view aggregated statistics about AI requests and feedback
/// So that I can monitor system usage and quality metrics
/// </summary>
[Collection("Admin Endpoints")]
public class AdminStatsEndpointsTests : AdminTestFixture
{
    public AdminStatsEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Admin requests dashboard statistics with filters
    ///   Given admin user is authenticated
    ///   And system has AI request logs and feedback data
    ///   When admin requests /admin/stats with userId and gameId filters
    ///   Then system returns aggregated statistics
    ///   And response includes totalRequests, avgLatencyMs, totalTokens, successRate
    ///   And response includes endpointCounts breakdown
    ///   And response includes feedbackCounts and feedbackByEndpoint metrics
    /// </summary>
    [Fact]
    public async Task GetAdminStats_WhenAdminAuthenticated_ReturnsAggregatedStatistics()
    {
        // Given: Admin user is authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-stats-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // And: System has request logs and feedback data
        var userEmail = $"user-stats-{Guid.NewGuid():N}@example.com";
        using var userClient = CreateClientWithoutCookies();
        await RegisterAndAuthenticateAsync(userClient, userEmail, "User");
        var userId = await GetUserIdByEmailAsync(userEmail);

        var seedContext = await SeedDashboardDataAsync(adminUserId, userId);

        // When: Admin requests stats with filters
        var requestUri = $"/api/v1/admin/stats?userId={adminUserId}&gameId=game-1" +
                         $"&startDate={Uri.EscapeDataString(seedContext.StartDate.ToString("O"))}" +
                         $"&endDate={Uri.EscapeDataString(seedContext.EndDate.ToString("O"))}";

        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns HTTP 200 OK
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // And: Response includes core aggregated metrics
        Assert.Equal(2, root.GetProperty("totalRequests").GetInt32());
        Assert.Equal(100, root.GetProperty("avgLatencyMs").GetDouble(), 2);
        Assert.Equal(80, root.GetProperty("totalTokens").GetInt32());
        Assert.Equal(0.5, root.GetProperty("successRate").GetDouble(), 3);

        // And: Response includes endpoint-specific counts
        var endpointCounts = root.GetProperty("endpointCounts");
        Assert.Equal(1, endpointCounts.GetProperty("qa").GetInt32());
        Assert.Equal(1, endpointCounts.GetProperty("setup").GetInt32());

        // And: Response includes feedback aggregation
        var feedbackCounts = root.GetProperty("feedbackCounts");
        Assert.Equal(1, feedbackCounts.GetProperty("helpful").GetInt32());
        Assert.Equal(1, feedbackCounts.GetProperty("not-helpful").GetInt32());
        Assert.Equal(2, root.GetProperty("totalFeedback").GetInt32());

        // And: Response includes feedback breakdown by endpoint
        var feedbackByEndpoint = root.GetProperty("feedbackByEndpoint");
        Assert.Equal(1, feedbackByEndpoint.GetProperty("qa").GetProperty("helpful").GetInt32());
        Assert.Equal(1, feedbackByEndpoint.GetProperty("setup").GetProperty("not-helpful").GetInt32());
    }

    /// <summary>
    /// Scenario: Non-admin user attempts to access admin stats
    ///   Given user is authenticated with Editor role
    ///   When user requests /admin/stats
    ///   Then system returns HTTP 403 Forbidden
    /// </summary>
    [Theory]
    [InlineData("Editor")]
    [InlineData("User")]
    public async Task GetAdminStats_WhenNonAdminRole_ReturnsForbidden(string role)
    {
        // Given: User authenticated with non-admin role
        using var nonAdminClient = Factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-stats-{Guid.NewGuid():N}@example.com";
        var cookies = await RegisterAndAuthenticateAsync(nonAdminClient, email, role);

        // When: User requests admin stats
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/stats");
        AddCookies(request, cookies);

        var response = await nonAdminClient.SendAsync(request);

        // Then: System denies access
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Anonymous user attempts to access admin stats
    ///   Given user is not authenticated
    ///   When user requests /admin/stats
    ///   Then system returns HTTP 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task GetAdminStats_WhenNotAuthenticated_ReturnsUnauthorized()
    {
        // Given: User is not authenticated
        using var client = Factory.CreateHttpsClient();

        // When: Anonymous user requests admin stats
        var response = await client.GetAsync("/api/v1/admin/stats");

        // Then: System requires authentication
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
