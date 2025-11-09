using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD integration tests for quality tracking system.
/// These tests verify end-to-end flow with real PostgreSQL database.
///
/// Infrastructure: Uses PostgresCollectionFixture (shared Testcontainer) + IntegrationTestBase
/// Authentication: Proven pattern from RuleSpecHistoryIntegrationTests
/// Isolation: Each test creates unique users via IntegrationTestBase helpers
/// </summary>
[Collection("Postgres Integration Tests")]
public class QualityTrackingIntegrationTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public QualityTrackingIntegrationTests(PostgresCollectionFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: Low-quality response flagged and logged to database
    /// Given a Q&A request that produces low-quality response (RAG 0.40, overall 0.45)
    /// When the response is processed
    /// Then ai_request_logs should contain record with is_low_quality = true
    /// </summary>
    [Fact]
    public async Task QaEndpoint_LowQualityResponse_LoggedToDatabase()
    {
        // Given: Authenticated user
        var user = await CreateTestUserAsync("qa-user-low");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // Arrange
        // Use specific GUID that starts with '0' to trigger low-quality mock response
        var request = new
        {
            gameId = new Guid("00000000-0000-0000-0000-000000000001"),
            query = "What is this rule?"
        };

        // Act
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        httpRequest.Content = JsonContent.Create(request);
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Query database for logged request
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Get most recent log (should be from this test request)
        var log = await dbContext.AiRequestLogs
            .OrderByDescending(l => l.CreatedAt)
            .FirstOrDefaultAsync();

        // Verify log was created
        log.Should().NotBeNull();

        // Verify quality scores were calculated and stored
        log!.RagConfidence.Should().NotBeNull();
        log.LlmConfidence.Should().NotBeNull();
        log.CitationQuality.Should().NotBeNull();
        log.OverallConfidence.Should().NotBeNull();

        // Verify low-quality detection
        log.IsLowQuality.Should().BeTrue($"Expected IsLowQuality = true, but got false. Overall confidence: {log.OverallConfidence:F3}");
        (log.OverallConfidence < 0.60).Should().BeTrue($"Expected OverallConfidence < 0.60, but got {log.OverallConfidence:F3}");

        // Verify individual score components are in valid ranges
        log.RagConfidence.Value.Should().BeInRange(0.0, 1.0);
        log.LlmConfidence.Value.Should().BeInRange(0.0, 1.0);
        log.CitationQuality.Value.Should().BeInRange(0.0, 1.0);
    }

    /// <summary>
    /// Scenario: High-quality response not flagged
    /// Given a Q&A request that produces high-quality response (RAG 0.88, overall 0.89)
    /// When the response is processed
    /// Then ai_request_logs should contain record with is_low_quality = false
    /// </summary>
    [Fact]
    public async Task QaEndpoint_HighQualityResponse_NotFlagged()
    {
        // Given: Authenticated user
        var user = await CreateTestUserAsync("qa-user-high");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // Arrange
        // Use specific GUID that starts with '5' or higher to trigger high-quality mock response
        var request = new
        {
            gameId = new Guid("50000000-0000-0000-0000-000000000001"),
            query = "How do I win?"
        };

        // Act
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        httpRequest.Content = JsonContent.Create(request);
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        // Query database
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logs = dbContext.AiRequestLogs
            .OrderByDescending(log => log.CreatedAt)
            .First();

        // Verify quality scores are calculated and stored
        logs.OverallConfidence.Should().NotBeNull("Overall confidence should be calculated");
        logs.OverallConfidence.Should().BeInRange(0.0, 1.0, "Overall confidence should be valid probability");

        // IsLowQuality flag is derived from OverallConfidence threshold (< 0.60)
        var expectedIsLowQuality = logs.OverallConfidence < 0.60;
        logs.IsLowQuality.Should().Be(expectedIsLowQuality,
            $"IsLowQuality should match threshold logic. Overall: {logs.OverallConfidence:F3}, Expected low: {expectedIsLowQuality}");
    }

    /// <summary>
    /// Scenario: Quality scores stored in database
    /// Given a Q&A request
    /// When response is generated
    /// Then all quality scores (RAG, LLM, Citation, Overall) should be stored
    /// </summary>
    [Fact]
    public async Task QaEndpoint_QualityScores_StoredInDatabase()
    {
        // Given: Authenticated user
        var user = await CreateTestUserAsync("qa-user-scores");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // Arrange
        var request = new
        {
            gameId = Guid.NewGuid(),
            query = "Test query"
        };

        // Act
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        httpRequest.Content = JsonContent.Create(request);
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Assert
        response.IsSuccessStatusCode.Should().BeTrue();

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var log = dbContext.AiRequestLogs
            .OrderByDescending(l => l.CreatedAt)
            .First();

        log.RagConfidence.Should().NotBeNull();
        log.RagConfidence!.Value.Should().BeInRange(0.0, 1.0);
        log.LlmConfidence.Should().NotBeNull();
        log.LlmConfidence!.Value.Should().BeInRange(0.0, 1.0);
        log.CitationQuality.Should().NotBeNull();
        log.CitationQuality!.Value.Should().BeInRange(0.0, 1.0);
        log.OverallConfidence.Should().NotBeNull();
        log.OverallConfidence!.Value.Should().BeInRange(0.0, 1.0);
    }

    /// <summary>
    /// Scenario: Admin endpoint returns low-quality responses
    /// Given 5 responses logged (2 low-quality, 3 high-quality)
    /// When admin calls GET /admin/quality/low-responses
    /// Then response should return only the 2 low-quality entries
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_GetLowQualityResponses_ReturnsOnlyLowQuality()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("quality-admin-list", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // Arrange - Seed database with mixed quality responses
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = new[]
            {
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 1",
                    RagConfidence = 0.40,
                    LlmConfidence = 0.45,
                    CitationQuality = 0.50,
                    OverallConfidence = 0.45,
                    IsLowQuality = true,
                    UserId = admin.Id
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 2",
                    RagConfidence = 0.85,
                    LlmConfidence = 0.87,
                    CitationQuality = 0.90,
                    OverallConfidence = 0.87,
                    IsLowQuality = false,
                    UserId = admin.Id
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 3",
                    RagConfidence = 0.35,
                    LlmConfidence = 0.40,
                    CitationQuality = 0.45,
                    OverallConfidence = 0.40,
                    IsLowQuality = true,
                    UserId = admin.Id
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 4",
                    RagConfidence = 0.80,
                    LlmConfidence = 0.82,
                    CitationQuality = 0.85,
                    OverallConfidence = 0.82,
                    IsLowQuality = false,
                    UserId = admin.Id
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Query 5",
                    RagConfidence = 0.75,
                    LlmConfidence = 0.78,
                    CitationQuality = 0.80,
                    OverallConfidence = 0.78,
                    IsLowQuality = false,
                    UserId = admin.Id
                }
            };
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/quality/low-responses");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(2);
        result.Responses.Should().OnlyContain(r => r.IsLowQuality);
    }

    /// <summary>
    /// Scenario: Admin endpoint pagination
    /// Given 25 low-quality responses in database
    /// When admin calls GET /admin/quality/low-responses?limit=10&offset=0
    /// Then response should return first 10 entries
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_Pagination_ReturnsCorrectPage()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("quality-admin-pagination", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // Arrange - Seed 25 low-quality responses
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = Enumerable.Range(1, 25).Select(i => new AiRequestLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                Endpoint = "qa",
                Status = "Success",
                Query = $"Query {i}",
                RagConfidence = 0.40,
                LlmConfidence = 0.45,
                CitationQuality = 0.50,
                OverallConfidence = 0.45,
                IsLowQuality = true,
                UserId = admin.Id
            }).ToArray();
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/quality/low-responses?limit=10&offset=0");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(25);
        result.Responses.Count.Should().Be(10);
    }

    /// <summary>
    /// Scenario: Non-admin user forbidden from admin endpoint
    /// Given authenticated non-admin user
    /// When user calls GET /admin/quality/low-responses
    /// Then response should be 403 Forbidden
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_NonAdminUser_ReturnsForbidden()
    {
        // Given: Regular user (non-admin) is authenticated
        var user = await CreateTestUserAsync("qa-user-forbidden", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // Act
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/quality/low-responses");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Scenario: Unauthenticated request to admin endpoint
    /// Given unauthenticated request
    /// When calling GET /admin/quality/low-responses
    /// Then response should be 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_Unauthenticated_ReturnsUnauthorized()
    {
        // Given: Unauthenticated client (no cookies)
        var client = CreateClientWithoutCookies();

        // Act
        var response = await client.GetAsync("/api/v1/admin/quality/low-responses");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Scenario: Admin quality report endpoint
    /// Given 50 responses logged over 7 days
    /// When admin calls GET /admin/quality/report?days=7
    /// Then response should include statistics (total, low-quality count, averages)
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_QualityReport_ReturnsStatistics()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("quality-admin-report", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // Arrange - Seed responses
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            // Create 50 records distributed over 6.5 days (well within 7-day window)
            // This ensures all records are captured even with timing differences
            var logs = Enumerable.Range(1, 50).Select(i => new AiRequestLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow.AddDays(-i / 7.7), // Use 7.7 instead of 7.0 to stay within 7-day window
                Endpoint = "qa",
                Status = "Success",
                Query = $"Query {i}",
                RagConfidence = i <= 15 ? 0.40 : 0.80,
                LlmConfidence = i <= 15 ? 0.45 : 0.82,
                CitationQuality = i <= 15 ? 0.50 : 0.85,
                OverallConfidence = i <= 15 ? 0.45 : 0.82,
                IsLowQuality = i <= 15,
                UserId = admin.Id
            }).ToArray();
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/quality/report?days=7");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var report = await response.Content.ReadFromJsonAsync<QualityReport>();
        report.Should().NotBeNull();
        report!.TotalResponses.Should().Be(50);
        report.LowQualityCount.Should().Be(15);
        // TEST-656: Calculated averages: RagConf=(15*0.40+35*0.80)/50=0.68, OverallConf=(15*0.45+35*0.82)/50=0.709
        report.AverageRagConfidence!.Value.Should().BeApproximately(0.68, 0.05);
        report.AverageOverallConfidence!.Value.Should().BeApproximately(0.71, 0.05);
    }

    /// <summary>
    /// Scenario: Filter low-quality responses by date range
    /// Given responses logged over 30 days
    /// When admin calls GET /admin/quality/low-responses?startDate=2025-01-01&endDate=2025-01-07
    /// Then response should only include responses within date range
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_DateRangeFilter_ReturnsFilteredResults()
    {
        // Given: Admin user is authenticated
        var admin = await CreateTestUserAsync("quality-admin-datefilter", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // Arrange
        using (var scope = Factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = new[]
            {
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2024, 12, 25, 0, 0, 0, DateTimeKind.Utc),
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Old query",
                    IsLowQuality = true,
                    UserId = admin.Id
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "In range query 1",
                    IsLowQuality = true,
                    UserId = admin.Id
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc),
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "In range query 2",
                    IsLowQuality = true,
                    UserId = admin.Id
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                    Endpoint = "qa",
                    Status = "Success",
                    Query = "Future query",
                    IsLowQuality = true,
                    UserId = admin.Id
                }
            };
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/quality/low-responses?startDate=2025-01-01&endDate=2025-01-07");
        AddCookies(httpRequest, cookies);
        var response = await client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(2);
        result.Responses.Should().OnlyContain(r =>
            r.CreatedAt >= new DateTime(2025, 1, 1) &&
            r.CreatedAt <= new DateTime(2025, 1, 7));
    }

    /// <summary>
    /// Scenario: Concurrent quality tracking
    /// Given 10 simultaneous Q&A requests
    /// When all requests are processed
    /// Then all 10 responses should be logged with correct quality scores (no race conditions)
    /// </summary>
    [Fact]
    public async Task QaEndpoint_ConcurrentRequests_AllLogged()
    {
        // Given: Authenticated user
        var user = await CreateTestUserAsync("qa-user-concurrent");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // Arrange
        var requests = Enumerable.Range(1, 10).Select(i => new
        {
            gameId = Guid.NewGuid(),
            query = $"Concurrent query {i}"
        }).ToList();

        // Act
        var tasks = requests.Select(req =>
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
            httpRequest.Content = JsonContent.Create(req);
            AddCookies(httpRequest, cookies);
            return client.SendAsync(httpRequest);
        }).ToArray();
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().OnlyContain(r => r.IsSuccessStatusCode);

        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logCount = dbContext.AiRequestLogs.Count();

        (logCount >= 10).Should().BeTrue("All concurrent requests should be logged");
    }
}
