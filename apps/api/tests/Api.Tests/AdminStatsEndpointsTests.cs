using Api.Tests.Fixtures;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    public AdminStatsEndpointsTests(PostgresCollectionFixture postgresFixture, WebApplicationFactoryFixture factory, ITestOutputHelper output) : base(postgresFixture, factory)
    {
        _output = output;
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

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns HTTP 200 OK
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // And: Response includes core aggregated metrics
        root.GetProperty("totalRequests").GetInt32().Should().Be(2);
        root.GetProperty("avgLatencyMs").GetDouble().Should().BeApproximately(100, 0.01);
        root.GetProperty("totalTokens").GetInt32().Should().Be(80);
        root.GetProperty("successRate").GetDouble().Should().BeApproximately(0.5, 0.001);

        // And: Response includes endpoint-specific counts
        var endpointCounts = root.GetProperty("endpointCounts");
        endpointCounts.GetProperty("qa").GetInt32().Should().Be(1);
        endpointCounts.GetProperty("setup").GetInt32().Should().Be(1);

        // And: Response includes feedback aggregation
        var feedbackCounts = root.GetProperty("feedbackCounts");
        feedbackCounts.GetProperty("helpful").GetInt32().Should().Be(1);
        feedbackCounts.GetProperty("not-helpful").GetInt32().Should().Be(1);
        root.GetProperty("totalFeedback").GetInt32().Should().Be(2);

        // And: Response includes feedback breakdown by endpoint
        var feedbackByEndpoint = root.GetProperty("feedbackByEndpoint");
        feedbackByEndpoint.GetProperty("qa").GetProperty("helpful").GetInt32().Should().Be(1);
        feedbackByEndpoint.GetProperty("setup").GetProperty("not-helpful").GetInt32().Should().Be(1);
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
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/stats");
        AddCookies(request, cookies);

        var response = await nonAdminClient.SendAsync(request);

        // Then: System denies access
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Scenario: Admin requests stats with no data in system
    ///   Given admin user is authenticated
    ///   And system has no AI request logs
    ///   When admin requests /admin/stats
    ///   Then system returns empty statistics
    ///   And totalRequests is 0
    ///   And avgLatencyMs is 0
    ///   And successRate is 0
    /// </summary>
    [Fact]
    public async Task GetAdminStats_WhenNoData_ReturnsEmptyStats()
    {
        // Given: Admin user is authenticated with no data in system
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-empty-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");

        // When: Admin requests stats
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/stats");
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns HTTP 200 OK with empty stats
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("totalRequests").GetInt32().Should().Be(0);
        root.GetProperty("avgLatencyMs").GetDouble().Should().Be(0);
        root.GetProperty("totalTokens").GetInt32().Should().Be(0);
        root.GetProperty("successRate").GetDouble().Should().Be(0);
        root.GetProperty("endpointCounts").EnumerateObject().Should().BeEmpty();
    }

    /// <summary>
    /// Scenario: Admin requests stats filtered by date range
    ///   Given admin user is authenticated
    ///   And system has logs spanning multiple days
    ///   When admin requests /admin/stats with startDate and endDate filters
    ///   Then system returns stats only for the specified date range
    /// </summary>
    [Fact]
    public async Task GetAdminStats_WithDateRangeFilter_ReturnsFilteredStats()
    {
        // Given: Admin user is authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-daterange-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // And: System has logs from different dates
        var now = DateTime.UtcNow;
        var seedContext = await SeedDashboardDataAsync(adminUserId, adminUserId);

        // When: Admin requests stats with date range (only recent data)
        var requestUri = $"/api/v1/admin/stats?startDate={Uri.EscapeDataString(seedContext.StartDate.ToString("O"))}" +
                         $"&endDate={Uri.EscapeDataString(seedContext.EndDate.ToString("O"))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns filtered statistics
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // Should have data within date range
        root.GetProperty("totalRequests").GetInt32().Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Scenario: Admin requests stats with only gameId filter
    ///   Given admin user is authenticated
    ///   And system has logs for multiple games
    ///   When admin requests /admin/stats with gameId filter
    ///   Then system returns stats only for specified game
    /// </summary>
    [Fact]
    public async Task GetAdminStats_WithGameIdFilter_ReturnsGameSpecificStats()
    {
        // Given: Admin user is authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-gameid-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // And: System has logs for game-1
        var seedContext = await SeedDashboardDataAsync(adminUserId, adminUserId);

        // When: Admin requests stats filtered by game-1
        var requestUri = "/api/v1/admin/stats?gameId=game-1";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns stats only for game-1
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // Should have 2 requests for game-1 (from seed data)
        root.GetProperty("totalRequests").GetInt32().Should().Be(2);
    }

    /// <summary>
    /// Scenario: Admin requests stats with combined filters (userId + gameId + dateRange)
    ///   Given admin user is authenticated
    ///   And system has diverse log data
    ///   When admin requests /admin/stats with multiple filters
    ///   Then system returns stats matching all filter criteria
    /// </summary>
    [Fact]
    public async Task GetAdminStats_WithCombinedFilters_ReturnsIntersectionOfFilters()
    {
        // Given: Admin user is authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-combined-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // And: Create second user
        var userEmail = $"user-combined-{Guid.NewGuid():N}@example.com";
        using var userClient = CreateClientWithoutCookies();
        await RegisterAndAuthenticateAsync(userClient, userEmail, "User");
        var userId = await GetUserIdByEmailAsync(userEmail);

        var seedContext = await SeedDashboardDataAsync(adminUserId, userId);

        // When: Admin requests stats with userId, gameId, and dateRange filters
        var requestUri = $"/api/v1/admin/stats?userId={adminUserId}&gameId=game-1" +
                         $"&startDate={Uri.EscapeDataString(seedContext.StartDate.ToString("O"))}" +
                         $"&endDate={Uri.EscapeDataString(seedContext.EndDate.ToString("O"))}";

        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns stats matching all criteria
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        // Should have admin's requests for game-1 only
        root.GetProperty("totalRequests").GetInt32().Should().Be(2);
    }

    /// <summary>
    /// Scenario: Admin requests stats when all requests have errors
    ///   Given admin user is authenticated
    ///   And system has only failed AI requests
    ///   When admin requests /admin/stats
    ///   Then system returns 0% success rate
    /// </summary>
    [Fact]
    public async Task GetAdminStats_WhenAllRequestsFailed_ReturnsZeroSuccessRate()
    {
        // Given: Admin user is authenticated
        using var adminClient = Factory.CreateHttpsClient();
        var adminEmail = $"admin-failures-{Guid.NewGuid():N}@example.com";
        var adminCookies = await RegisterAndAuthenticateAsync(adminClient, adminEmail, "Admin");
        var adminUserId = await GetUserIdByEmailAsync(adminEmail);

        // And: System has only failed requests
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                UserId = adminUserId,
                Endpoint = "qa",
                GameId = "game-1",
                LatencyMs = 100,
                TokenCount = 0,
                Status = "Error",
                ErrorMessage = "API timeout",
                CreatedAt = DateTime.UtcNow
            },
            new AiRequestLogEntity
            {
                UserId = adminUserId,
                Endpoint = "explain",
                GameId = "game-1",
                LatencyMs = 150,
                TokenCount = 0,
                Status = "Error",
                ErrorMessage = "Service unavailable",
                CreatedAt = DateTime.UtcNow
            });
        await dbContext.SaveChangesAsync();

        // When: Admin requests stats
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/stats");
        AddCookies(request, adminCookies);

        var response = await adminClient.SendAsync(request);

        // Then: System returns 0% success rate
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        root.GetProperty("totalRequests").GetInt32().Should().Be(2);
        root.GetProperty("successRate").GetDouble().Should().Be(0.0);
    }
}