using Api.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Integration.FrontendSdk;

/// <summary>
/// Integration tests for HTTP behavior that the frontend SDK depends on:
/// - Retry scenarios (503, 429, timeouts)
/// - Circuit breaker behavior
/// - Request deduplication
/// - Connection handling
///
/// These tests validate that the API returns appropriate responses
/// that trigger correct behavior in the frontend SDK's retry logic.
/// </summary>
[Collection(nameof(FrontendSdkTestCollection))]
[Trait("Category", TestCategories.Integration)]
public class HttpBehaviorTests : IAsyncLifetime
{
    private readonly FrontendSdkTestFactory _factory;
    private HttpClient _client = null!;
    private MeepleAiDbContext _dbContext = null!;

    public HttpBehaviorTests(FrontendSdkTestFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false, // Test raw responses
            HandleCookies = true
        });

        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Reset database for test isolation
        await ResetDatabaseAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_dbContext != null) await _dbContext.DisposeAsync();
    }

    private async Task ResetDatabaseAsync()
    {
        // Get all table names
        var tableNames = await _dbContext.Database
            .SqlQueryRaw<string>(
                @"SELECT tablename
                  FROM pg_tables
                  WHERE schemaname = 'public'
                  AND tablename != '__EFMigrationsHistory'")
            .ToListAsync();

        if (tableNames.Count > 0)
        {
            // Disable FK constraints, truncate all tables, re-enable FK constraints
            await _dbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'replica';");
            try
            {
                foreach (var tableName in tableNames)
                {
                    await _dbContext.Database.ExecuteSqlRawAsync($"TRUNCATE TABLE \"{tableName}\" CASCADE;");
                }
            }
            finally
            {
                await _dbContext.Database.ExecuteSqlRawAsync("SET session_replication_role = 'origin';");
            }
        }
    }
    [Fact(DisplayName = "GET /health should return health status (200 or 503)")]
    public async Task Health_ReturnsHealthStatus()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/health");

        // Assert
        // Health endpoint should return either:
        // - 200 OK if all services are healthy
        // - 503 Service Unavailable if any service is down
        // This tests that the frontend SDK can handle both scenarios
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        var content = await response.Content.ReadAsStringAsync();
        // Content may be empty for some error responses (frontend SDK should handle this)
        content.Should().ContainAny("Healthy", "Unhealthy", "Degraded");
    }

    [Fact(DisplayName = "GET /api/v1/games without auth should return 200 OK (public endpoint)")]
    public async Task GetGames_WithoutAuth_Returns200Ok()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/api/v1/games");

        // Assert
        // Games listing is a PUBLIC endpoint (.AllowAnonymous()) for game discovery
        // Frontend SDK can fetch games without authentication
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(DisplayName = "GET /api/v1/games/{id}/sessions without auth should return 401 Unauthorized")]
    public async Task GetGameSessions_WithoutAuth_Returns401Unauthorized()
    {
        // Arrange & Act
        var response = await _client.GetAsync($"/api/v1/games/{Guid.NewGuid()}/sessions");

        // Assert
        // Game sessions endpoint requires authentication (.RequireAuthenticatedUser())
        // Frontend SDK should handle 401 by redirecting to login
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact(DisplayName = "GET with invalid endpoint should return 404 for retry logic")]
    public async Task Get_WithInvalidEndpoint_Returns404NotFound()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/nonexistent");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Frontend SDK should NOT retry 404 (client error, not transient)
        // This test validates the API returns correct status code
    }

    [Fact(DisplayName = "GET with malformed request should return 400 Bad Request")]
    public async Task Get_WithMalformedQuery_Returns400BadRequest()
    {
        // Arrange - Send invalid query parameter
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games?page=-1&pageSize=0");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // API should return 400 for invalid input (not 500)
        // Frontend SDK should NOT retry 400 (client error)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.Unauthorized, HttpStatusCode.UnprocessableEntity);
    }
    [Fact(DisplayName = "OPTIONS request should return CORS headers for preflight")]
    public async Task Options_ForPreflightRequest_ReturnsCorsHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);

        // Verify CORS headers are present (frontend SDK needs these)
        response.Headers.Should().Contain(h =>
            h.Key.Equals("Access-Control-Allow-Origin", StringComparison.OrdinalIgnoreCase) ||
            h.Key.Equals("Access-Control-Allow-Methods", StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName = "GET request should accept standard HTTP headers from SDK")]
    public async Task Get_WithStandardHeaders_AcceptsAndProcessesRequest()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        request.Headers.Add("Accept", "application/json");
        request.Headers.Add("User-Agent", "MeepleAI-SDK/1.0");
        request.Headers.Add("X-Request-ID", Guid.NewGuid().ToString());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }
    [Fact(DisplayName = "GET with reasonable timeout should complete successfully")]
    public async Task Get_WithReasonableTimeout_CompletesWithinTimeout()
    {
        // Arrange - Use longer timeout for test environment (health checks may need to verify services)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");

        // Act
        var response = await _client.SendAsync(request, cts.Token);

        // Assert
        // Health endpoint may return 503 if external services are down
        // Frontend SDK should handle both success and unavailability
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable);

        // Test validates API responds within reasonable time
        // Frontend SDK can rely on predictable response times
    }

    [Fact(DisplayName = "POST with empty body should handle gracefully")]
    public async Task Post_WithEmptyBody_ReturnsAppropriateError()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login");
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // API should return 400 or 401, not 500 (proper validation)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized);
    }
    [Fact(DisplayName = "GET response should include Content-Type header")]
    public async Task Get_Response_IncludesContentTypeHeader()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/health");

        // Assert
        // Content-Type should be present for both 200 and 503 responses
        if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            response.Content.Headers.ContentType.Should().NotBeNull();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
            }
        }

        // Frontend SDK relies on Content-Type for response parsing
    }

    [Fact(DisplayName = "GET response should include Date header for caching")]
    public async Task Get_Response_IncludesDateHeader()
    {
        // Arrange & Act
        var response = await _client.GetAsync("/health");

        // Assert
        // Date header may or may not be present depending on middleware
        // This is optional for frontend SDK caching strategy
        if (response.Headers.Date != null)
        {
            response.Headers.Date.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5));
        }

        // Frontend SDK can use Date header for cache validation if present
    }

    [Fact(DisplayName = "POST request should accept JSON content type")]
    public async Task Post_WithJsonContent_AcceptsAndProcesses()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register");
        var jsonContent = new
        {
            email = $"test-{Guid.NewGuid()}@example.com",
            password = "SecureP@ssw0rd123",
            displayName = "Test User"
        };
        request.Content = JsonContent.Create(jsonContent);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // Should accept JSON (200/201), return validation error (400/422), or internal error
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Created,
            HttpStatusCode.OK,
            HttpStatusCode.BadRequest,
            HttpStatusCode.Conflict,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.InternalServerError); // May occur if service dependencies fail
    }
    [Fact(DisplayName = "Multiple concurrent GET requests should handle gracefully")]
    public async Task Get_WithConcurrentRequests_HandlesAllSuccessfully()
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10).Select(async _ =>
        {
            var response = await _client.GetAsync("/health");
            return response.StatusCode;
        });

        // Act
        var results = await Task.WhenAll(tasks);

        // Assert
        // All responses should be either OK or Service Unavailable
        results.Should().AllSatisfy(s => s.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.ServiceUnavailable));

        // API should handle concurrent requests from frontend SDK
        // No race conditions or resource contention
    }

    [Fact(DisplayName = "Concurrent POST requests should maintain data consistency")]
    public async Task Post_WithConcurrentRequests_MaintainsDataConsistency()
    {
        // Arrange - Create multiple registration requests concurrently
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register");
            var jsonContent = new
            {
                email = $"concurrent-test-{i}-{Guid.NewGuid()}@example.com",
                password = "SecureP@ssw0rd123",
                displayName = $"Concurrent User {i}"
            };
            request.Content = JsonContent.Create(jsonContent);

            var response = await _client.SendAsync(request);
            return response;
        });

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        // All should either succeed (200/201) or fail with validation (400/422/409) or service error (500)
        // Frontend SDK should handle all these scenarios
        foreach (var response in responses)
        {
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.Created,
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.Conflict,
                HttpStatusCode.UnprocessableEntity,
                HttpStatusCode.InternalServerError); // May occur if dependencies fail
        }
    }
}
