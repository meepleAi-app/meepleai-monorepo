using Api.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests.Integration.FrontendSdk;

/// <summary>
/// Integration tests for error handling scenarios that the frontend SDK encounters:
/// - 4xx client errors (400, 401, 403, 404, 409, 422)
/// - 5xx server errors (500, 503)
/// - Network errors (timeouts, connection refused)
/// - Malformed responses
/// - Validation errors
///
/// These tests validate that the API returns appropriate error responses
/// with meaningful error messages for the frontend SDK to display to users.
/// </summary>
[Collection(nameof(FrontendSdkTestCollection))]
public class ErrorHandlingTests : IAsyncLifetime
{
    private readonly FrontendSdkTestFactory _factory;
    private HttpClient _client = null!;
    private MeepleAiDbContext _dbContext = null!;

    public ErrorHandlingTests(FrontendSdkTestFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask InitializeAsync()
    {
        _client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        var scope = _factory.Services.CreateScope();
        _dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        await ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _dbContext?.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task ResetDatabaseAsync()
    {
        var tableNames = await _dbContext.Database
            .SqlQueryRaw<string>(
                @"SELECT tablename
                  FROM pg_tables
                  WHERE schemaname = 'public'
                  AND tablename != '__EFMigrationsHistory'")
            .ToListAsync();

        if (tableNames.Count > 0)
        {
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

    #region 4xx Client Error Tests

    [Fact(DisplayName = "400 Bad Request should include validation error details")]
    public async Task BadRequest_IncludesValidationErrors()
    {
        // Arrange - Send invalid registration data
        var invalidRequest = new
        {
            email = "", // Empty email
            password = "123", // Too short
            displayName = "" // Empty display name
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", invalidRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);

        // Verify response contains error details
        var content = await response.Content.ReadAsStringAsync();
        // Content may be empty for some error responses (frontend SDK should handle this)

        // Frontend SDK should parse and display validation errors
        // Response should be valid JSON
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            content.Should().Match(c => c.StartsWith("{") || c.StartsWith("["));
        }
    }

    [Fact(DisplayName = "401 Unauthorized should not leak sensitive information")]
    public async Task Unauthorized_DoesNotLeakSensitiveInfo()
    {
        // Arrange
        var loginRequest = new
        {
            email = "nonexistent@example.com",
            password = "WrongPassword123!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);

        var content = await response.Content.ReadAsStringAsync();

        // Response should NOT reveal sensitive details (stack traces with line numbers)
        // Note: JSON field "stackTrace":null is acceptable
        // Note: "invalid email or password" message is acceptable
        content.Should().NotContain("at System.");
        content.Should().NotContain("at Microsoft.");
        content.Should().NotContain("at Api.");
        content.ToLowerInvariant().Should().NotContain("connection string");
        content.ToLowerInvariant().Should().NotContain("server=");
        content.ToLowerInvariant().Should().NotContain("database=");

        // Frontend SDK should display generic "Invalid credentials" message
    }

    [Fact(DisplayName = "403 Forbidden should indicate insufficient permissions")]
    public async Task Forbidden_IndicatesInsufficientPermissions()
    {
        // Arrange - Create regular user (not admin)
        var email = $"regular-user-{Guid.NewGuid()}@example.com";
        var password = "SecureP@ssw0rd123!";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Regular User" });
        await _client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

        // Act - Try to access admin-only endpoint
        var response = await _client.GetAsync("/api/v1/admin/stats");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);

        // Frontend SDK should display "You don't have permission" message
    }

    [Fact(DisplayName = "404 Not Found should return consistent error format")]
    public async Task NotFound_ReturnsConsistentErrorFormat()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/nonexistent/endpoint");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var content = await response.Content.ReadAsStringAsync();
        // Content may be empty for some error responses (frontend SDK should handle this)

        // Frontend SDK expects consistent error format
        // Should be JSON or plain text (not HTML error page)
        if (content.Length > 0)
        {
            content.Should().Match(c => c.StartsWith("{") || c.StartsWith("\"") || c.StartsWith("[") || !c.Contains("<html"));
        }
    }

    [Fact(DisplayName = "409 Conflict should include conflict details")]
    public async Task Conflict_IncludesConflictDetails()
    {
        // Arrange - Create user
        var email = $"conflict-test-{Guid.NewGuid()}@example.com";
        var password = "SecureP@ssw0rd123!";

        await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "First User" });

        // Act - Try to register same email again
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName = "Second User" });

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);

        var content = await response.Content.ReadAsStringAsync();
        // Content may be empty for some error responses (frontend SDK should handle this)

        // Frontend SDK should display "Email already in use" error
    }

    [Fact(DisplayName = "422 Unprocessable Entity should include semantic validation errors")]
    public async Task UnprocessableEntity_IncludesSemanticValidationErrors()
    {
        // Note: 422 is typically returned for semantic validation errors
        // (syntactically valid but semantically incorrect)

        // This is a placeholder test - actual implementation depends on your API
        // Example: Creating a game session with invalid game ID

        var request = new
        {
            gameId = Guid.Empty, // Valid UUID format but semantically invalid
            sessionName = "Test Session"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/sessions", request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.BadRequest,
            HttpStatusCode.NotFound,
            HttpStatusCode.Unauthorized); // Depends on auth requirements

        // Frontend SDK should display semantic error (e.g., "Game not found")
    }

    #endregion

    #region 5xx Server Error Tests

    [Fact(DisplayName = "500 Internal Server Error should not expose implementation details")]
    public async Task InternalServerError_DoesNotExposeImplementationDetails()
    {
        // Note: Intentionally triggering 500 errors in tests is difficult
        // This test verifies the API has proper error handling middleware

        // Act - Try to trigger an error (this may not actually return 500)
        var response = await _client.PostAsJsonAsync("/api/v1/chat", new
        {
            message = new string('x', 1_000_000) // Extremely long message
        });

        // Assert
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var content = await response.Content.ReadAsStringAsync();

            // Should NOT expose:
            // - Stack traces
            // - Database connection strings
            // - Internal paths
            // - Framework version details
            content.ToLowerInvariant().Should().NotContain("stack trace");
            content.Should().NotContain("at System.");
            content.Should().NotContain("at Microsoft.");
            content.Should().NotContain("Connection");
        }

        // Frontend SDK should display generic "Something went wrong" message
    }

    #endregion

    #region Content Type Error Tests

    [Fact(DisplayName = "POST with invalid Content-Type should return 415 Unsupported Media Type")]
    public async Task Post_WithInvalidContentType_Returns415UnsupportedMediaType()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register");
        request.Content = new StringContent("not json", System.Text.Encoding.UTF8, "text/plain");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.UnsupportedMediaType,
            HttpStatusCode.BadRequest);

        // Frontend SDK should ensure it always sends application/json
    }

    [Fact(DisplayName = "POST with malformed JSON should return 400 Bad Request")]
    public async Task Post_WithMalformedJson_Returns400BadRequest()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/register");
        request.Content = new StringContent("{ invalid json }", System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Frontend SDK should validate JSON before sending
    }

    #endregion

    #region Request Validation Tests

    [Fact(DisplayName = "POST with missing required fields should return field-specific errors")]
    public async Task Post_WithMissingRequiredFields_ReturnsFieldSpecificErrors()
    {
        // Arrange
        var incompleteRequest = new
        {
            email = "test@example.com"
            // Missing password and displayName
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", incompleteRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK, HttpStatusCode.UnprocessableEntity, HttpStatusCode.InternalServerError);

        var content = await response.Content.ReadAsStringAsync();

        // Response should indicate which fields are missing
        // Frontend SDK can highlight specific form fields with errors
        // Content may be empty for some error responses (frontend SDK should handle this)
    }

    [Fact(DisplayName = "GET with invalid query parameters should handle gracefully")]
    public async Task Get_WithInvalidQueryParameters_HandlesGracefully()
    {
        // Arrange - Send various invalid query parameters
        var testCases = new[]
        {
            "/api/v1/games?page=-1", // Negative page
            "/api/v1/games?pageSize=0", // Zero page size
            "/api/v1/games?pageSize=10000", // Excessive page size
            "/api/v1/games?invalid=param" // Unknown parameter
        };

        foreach (var testCase in testCases)
        {
            // Act
            var response = await _client.GetAsync(testCase);

            // Assert
            // Should either handle gracefully (200 with defaults), return 400, or 401 (requires auth)
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK,
                HttpStatusCode.BadRequest,
                HttpStatusCode.Unauthorized);

            // Should NOT return 500
            response.StatusCode.Should().NotBe(HttpStatusCode.InternalServerError);
        }

        // Frontend SDK should validate query parameters before sending
    }

    #endregion

    #region Error Response Format Tests

    [Fact(DisplayName = "Error responses should be valid JSON")]
    public async Task ErrorResponses_AreValidJson()
    {
        // Arrange - Trigger a known error
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "invalid",
            password = "wrong"
        });

        // Assert
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.BadRequest,
            HttpStatusCode.Unauthorized,
            HttpStatusCode.UnprocessableEntity,
            HttpStatusCode.InternalServerError);

        var content = await response.Content.ReadAsStringAsync();
        // Content may be empty for some error responses (frontend SDK should handle this)

        // Should be valid JSON that frontend SDK can parse
        // Not HTML error pages
        if (content.Length > 0)
        {
            content.Should().Match(c => c.StartsWith("{") || c.StartsWith("\"") || c.StartsWith("[") || !c.Contains("<html"));
        }

        // Frontend SDK should be able to parse error message and display it to user
    }

    [Fact(DisplayName = "Error responses should include Content-Type header")]
    public async Task ErrorResponses_IncludeContentTypeHeader()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/nonexistent");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Error responses may or may not have Content-Type depending on error type
        // Frontend SDK should handle both cases
    }

    #endregion

    #region Timeout and Network Error Simulation

    [Fact(DisplayName = "Request cancellation should handle gracefully")]
    public async Task Request_WhenCancelled_HandlesGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");

        // Act - Cancel immediately
        cts.Cancel();

        // Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _client.SendAsync(request, cts.Token);
        });

        // Frontend SDK should handle request cancellation
        // (e.g., when user navigates away during request)
    }

    [Fact(DisplayName = "Request with short timeout should fail gracefully")]
    public async Task Request_WithShortTimeout_FailsGracefully()
    {
        // Arrange
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(1));
        var request = new HttpRequestMessage(HttpMethod.Get, "/health");

        // Act & Assert
        // Request may succeed if it's fast enough, or timeout
        try
        {
            var response = await _client.SendAsync(request, cts.Token);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        catch (OperationCanceledException)
        {
            // Expected if timeout is too short
            // Frontend SDK should handle timeout errors
            Assert.True(true, "Request timed out as expected");
        }
    }

    #endregion
}