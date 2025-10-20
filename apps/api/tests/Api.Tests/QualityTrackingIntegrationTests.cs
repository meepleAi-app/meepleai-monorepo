using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD integration tests for quality tracking system.
/// These tests verify end-to-end flow with real database (TDD RED phase).
/// </summary>
public class QualityTrackingIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private HttpClient? _adminClient;

    /// <summary>
    /// Scenario: Low-quality response flagged and logged to database
    /// Given a Q&A request that produces low-quality response (RAG 0.40, overall 0.45)
    /// When the response is processed
    /// Then ai_request_logs should contain record with is_low_quality = true
    /// </summary>
    [Fact]
    public async Task QaEndpoint_LowQualityResponse_LoggedToDatabase()
    {
        // Arrange
        var request = new
        {
            gameId = Guid.NewGuid(),
            query = "What is this rule?"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/qa", request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Query database for logged request
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logs = dbContext.AiRequestLogs
            .Where(log => log.IsLowQuality)
            .ToList();

        Assert.NotEmpty(logs);
        var log = logs.First();
        Assert.True(log.IsLowQuality);
        Assert.True(log.OverallConfidence < 0.60);
    }

    /// <summary>
    /// Scenario: High-quality response not flagged
    /// Given a Q&A request that produces high-quality response (RAG 0.85, overall 0.87)
    /// When the response is processed
    /// Then ai_request_logs should contain record with is_low_quality = false
    /// </summary>
    [Fact]
    public async Task QaEndpoint_HighQualityResponse_NotFlagged()
    {
        // Arrange
        var request = new
        {
            gameId = Guid.NewGuid(),
            query = "How do I win?"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/qa", request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        // Query database
        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logs = dbContext.AiRequestLogs
            .OrderByDescending(log => log.CreatedAt)
            .First();

        Assert.False(logs.IsLowQuality);
        Assert.True(logs.OverallConfidence >= 0.60);
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
        // Arrange
        var request = new
        {
            gameId = Guid.NewGuid(),
            query = "Test query"
        };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/v1/agents/qa", request);

        // Assert
        Assert.True(response.IsSuccessStatusCode);

        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var log = dbContext.AiRequestLogs
            .OrderByDescending(l => l.CreatedAt)
            .First();

        Assert.NotNull(log.RagConfidence);
        Assert.InRange(log.RagConfidence.Value, 0.0, 1.0);
        Assert.NotNull(log.LlmConfidence);
        Assert.InRange(log.LlmConfidence.Value, 0.0, 1.0);
        Assert.NotNull(log.CitationQuality);
        Assert.InRange(log.CitationQuality.Value, 0.0, 1.0);
        Assert.NotNull(log.OverallConfidence);
        Assert.InRange(log.OverallConfidence.Value, 0.0, 1.0);
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
        // Arrange - Seed database with mixed quality responses
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = new[]
            {
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Query = "Query 1",
                    RagConfidence = 0.40,
                    LlmConfidence = 0.45,
                    CitationQuality = 0.50,
                    OverallConfidence = 0.45,
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Query = "Query 2",
                    RagConfidence = 0.85,
                    LlmConfidence = 0.87,
                    CitationQuality = 0.90,
                    OverallConfidence = 0.87,
                    IsLowQuality = false
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Query = "Query 3",
                    RagConfidence = 0.35,
                    LlmConfidence = 0.40,
                    CitationQuality = 0.45,
                    OverallConfidence = 0.40,
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Query = "Query 4",
                    RagConfidence = 0.80,
                    LlmConfidence = 0.82,
                    CitationQuality = 0.85,
                    OverallConfidence = 0.82,
                    IsLowQuality = false
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.UtcNow,
                    Query = "Query 5",
                    RagConfidence = 0.75,
                    LlmConfidence = 0.78,
                    CitationQuality = 0.80,
                    OverallConfidence = 0.78,
                    IsLowQuality = false
                }
            };
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await _adminClient!.GetAsync("/admin/quality/low-responses");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Responses, r => Assert.True(r.IsLowQuality));
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
        // Arrange - Seed 25 low-quality responses
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = Enumerable.Range(1, 25).Select(i => new AiRequestLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                Query = $"Query {i}",
                RagConfidence = 0.40,
                LlmConfidence = 0.45,
                CitationQuality = 0.50,
                OverallConfidence = 0.45,
                IsLowQuality = true
            }).ToArray();
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await _adminClient!.GetAsync("/admin/quality/low-responses?limit=10&offset=0");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        Assert.NotNull(result);
        Assert.Equal(25, result.TotalCount);
        Assert.Equal(10, result.Responses.Count);
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
        // Arrange - Use regular user client (not admin)
        var response = await _client!.GetAsync("/admin/quality/low-responses");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
        // Arrange - Create unauthenticated client
        var unauthClient = _factory!.CreateClient();

        // Act
        var response = await unauthClient.GetAsync("/admin/quality/low-responses");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
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
        // Arrange - Seed responses
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = Enumerable.Range(1, 50).Select(i => new AiRequestLogEntity
            {
                Id = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow.AddDays(-i / 7.0),
                Query = $"Query {i}",
                RagConfidence = i <= 15 ? 0.40 : 0.80,
                LlmConfidence = i <= 15 ? 0.45 : 0.82,
                CitationQuality = i <= 15 ? 0.50 : 0.85,
                OverallConfidence = i <= 15 ? 0.45 : 0.82,
                IsLowQuality = i <= 15
            }).ToArray();
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await _adminClient!.GetAsync("/admin/quality/report?days=7");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var report = await response.Content.ReadFromJsonAsync<QualityReport>();
        Assert.NotNull(report);
        Assert.Equal(50, report.TotalResponses);
        Assert.Equal(15, report.LowQualityCount);
        Assert.InRange(report.AverageRagConfidence!.Value, 0.60, 0.75);
        Assert.InRange(report.AverageOverallConfidence!.Value, 0.60, 0.75);
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
        // Arrange
        using (var scope = _factory!.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var logs = new[]
            {
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2024, 12, 25, 0, 0, 0, DateTimeKind.Utc),
                    Query = "Old query",
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2025, 1, 3, 0, 0, 0, DateTimeKind.Utc),
                    Query = "In range query 1",
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2025, 1, 5, 0, 0, 0, DateTimeKind.Utc),
                    Query = "In range query 2",
                    IsLowQuality = true
                },
                new AiRequestLogEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                    Query = "Future query",
                    IsLowQuality = true
                }
            };
            dbContext.AiRequestLogs.AddRange(logs);
            await dbContext.SaveChangesAsync();
        }

        // Act
        var response = await _adminClient!.GetAsync(
            "/admin/quality/low-responses?startDate=2025-01-01&endDate=2025-01-07");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<LowQualityResponsesResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Responses, r =>
        {
            Assert.True(r.CreatedAt >= new DateTime(2025, 1, 1));
            Assert.True(r.CreatedAt <= new DateTime(2025, 1, 7));
        });
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
        // Arrange
        var requests = Enumerable.Range(1, 10).Select(i => new
        {
            gameId = Guid.NewGuid(),
            query = $"Concurrent query {i}"
        }).ToList();

        // Act
        var tasks = requests.Select(req =>
            _client!.PostAsJsonAsync("/api/v1/agents/qa", req)
        ).ToArray();
        await Task.WhenAll(tasks);

        // Assert
        Assert.All(tasks, t => Assert.True(t.Result.IsSuccessStatusCode));

        using var scope = _factory!.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var logCount = dbContext.AiRequestLogs.Count();

        Assert.True(logCount >= 10, "All concurrent requests should be logged");
    }

    #region Test Lifecycle

    public async Task InitializeAsync()
    {
        // Start Postgres container
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("meepleai_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();

        // Create factory with test database
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Replace connection string with test container
                    // Configure test services
                });
            });

        // Create authenticated clients
        _client = _factory.CreateClient(); // Regular user
        _adminClient = _factory.CreateClient(); // Admin user

        // TODO: Authenticate clients (session cookies)
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _adminClient?.Dispose();
        _factory?.Dispose();
        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    #endregion
}

#region Models (these will fail compilation - expected in RED phase)

/// <summary>
/// Result model for low-quality responses endpoint.
/// </summary>
public class LowQualityResponsesResult
{
    public int TotalCount { get; set; }
    public List<LowQualityResponseDto> Responses { get; set; } = new();
}

/// <summary>
/// DTO for low-quality response.
/// </summary>
public class LowQualityResponseDto
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Query { get; set; } = string.Empty;
    public double RagConfidence { get; set; }
    public double LlmConfidence { get; set; }
    public double CitationQuality { get; set; }
    public double OverallConfidence { get; set; }
    public bool IsLowQuality { get; set; }
}

#endregion
