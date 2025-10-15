using System.Net;
using System.Net.Http.Json;
using System.Text;
using Api.Infrastructure.Entities;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Edge scenario tests for API endpoints.
/// Tests Content-Type validation, payload size limits, special characters, malformed requests, and concurrent operations.
/// Related to Issue #260 - TEST-01: Expand Integration Test Coverage (Phase 5).
/// </summary>
public class EdgeScenarioTests : IntegrationTestBase
{
    public EdgeScenarioTests(WebApplicationFactoryFixture fixture) : base(fixture)
    {
    }

    #region Content-Type Validation

    [Fact]
    public async Task PostRequest_WithoutContentType_ReturnsBadRequest()
    {
        // Given: Authenticated user without Content-Type header
        var user = await CreateTestUserAsync("no-content-type-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = new StringContent("{\"gameId\":\"test\",\"query\":\"test\"}", Encoding.UTF8);
        request.Content.Headers.ContentType = null; // Remove Content-Type
        AddCookies(request, cookies);

        // When: Request is sent without Content-Type
        var response = await client.SendAsync(request);

        // Then: Returns 415 Unsupported Media Type or 400 Bad Request
        Assert.True(response.StatusCode == HttpStatusCode.UnsupportedMediaType ||
                    response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 415 or 400, got {response.StatusCode}");
    }

    [Fact]
    public async Task PostRequest_WithWrongContentType_ReturnsBadRequest()
    {
        // Given: Authenticated user with wrong Content-Type
        var user = await CreateTestUserAsync("wrong-content-type-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = new StringContent("{\"gameId\":\"test\",\"query\":\"test\"}", Encoding.UTF8, "text/plain");
        AddCookies(request, cookies);

        // When: Request is sent with text/plain instead of application/json
        var response = await client.SendAsync(request);

        // Then: Returns 415 Unsupported Media Type or 400 Bad Request
        Assert.True(response.StatusCode == HttpStatusCode.UnsupportedMediaType ||
                    response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected 415 or 400, got {response.StatusCode}");
    }

    [Fact]
    public async Task PostRequest_WithCorrectContentType_Succeeds()
    {
        // Given: Authenticated user with correct Content-Type
        var user = await CreateTestUserAsync("correct-content-type-user", UserRole.User);
        var game = await CreateTestGameAsync("Content Type Test Game");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = JsonContent.Create(new { gameId = game.Id, query = "test" });
        AddCookies(request, cookies);

        // When: Request is sent with application/json
        var response = await client.SendAsync(request);

        // Then: Request succeeds (or fails for business logic, not Content-Type)
        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    #endregion

    #region Payload Size Limits

    [Fact]
    public async Task PostRequest_WithVeryLargePayload_HandlesGracefully()
    {
        // Given: Authenticated user with very large payload
        var user = await CreateTestUserAsync("large-payload-user", UserRole.User);
        var game = await CreateTestGameAsync("Large Payload Test Game");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // Create a very large query (10MB of text)
        var largeQuery = new string('A', 10 * 1024 * 1024);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = JsonContent.Create(new { gameId = game.Id, query = largeQuery });
        AddCookies(request, cookies);

        // When: Request is sent
        var response = await client.SendAsync(request);

        // Then: Server handles gracefully (may accept, truncate, or reject with 413)
        // NOTE: Actual behavior depends on server configuration
        Assert.True(response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.RequestEntityTooLarge ||
                    response.StatusCode == HttpStatusCode.BadRequest,
            $"Expected OK, 413, or 400, got {response.StatusCode}");
    }

    [Fact]
    public async Task PostRequest_WithEmptyBody_ReturnsBadRequest()
    {
        // Given: Authenticated user with empty request body
        var user = await CreateTestUserAsync("empty-body-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = new StringContent("", Encoding.UTF8, "application/json");
        AddCookies(request, cookies);

        // When: Request is sent with empty body
        var response = await client.SendAsync(request);

        // Then: Returns 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Special Characters Handling

    [Fact]
    public async Task PostRequest_WithSpecialCharactersInGameId_HandlesCorrectly()
    {
        // Given: Authenticated user with special characters in gameId
        var user = await CreateTestUserAsync("special-chars-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // GameId with special characters: spaces, unicode, symbols
        var specialGameId = "Test Game 123 !@#$%^&*() 中文 émojis🎮";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = JsonContent.Create(new { gameId = specialGameId, query = "test" });
        AddCookies(request, cookies);

        // When: Request is sent
        var response = await client.SendAsync(request);

        // Then: Server handles gracefully (validates gameId format or processes)
        // NOTE: Should not crash or return 500
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task PostRequest_WithUnicodeInQuery_HandlesCorrectly()
    {
        // Given: Authenticated user with Unicode in query
        var user = await CreateTestUserAsync("unicode-user", UserRole.User);
        var game = await CreateTestGameAsync("Unicode Test Game");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // Query with various Unicode characters
        var unicodeQuery = "Comment jouer? 如何玩? Как играть? 🎲🃏";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = JsonContent.Create(new { gameId = game.Id, query = unicodeQuery });
        AddCookies(request, cookies);

        // When: Request is sent
        var response = await client.SendAsync(request);

        // Then: Server handles Unicode correctly (should not crash)
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Fact]
    public async Task PostRequest_WithSqlInjectionAttempt_IsSafelyHandled()
    {
        // Given: Authenticated user attempting SQL injection
        var user = await CreateTestUserAsync("sql-injection-user", UserRole.User);
        var game = await CreateTestGameAsync("SQL Injection Test Game");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // Common SQL injection patterns
        var maliciousQuery = "'; DROP TABLE games; --";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = JsonContent.Create(new { gameId = game.Id, query = maliciousQuery });
        AddCookies(request, cookies);

        // When: Request is sent
        var response = await client.SendAsync(request);

        // Then: Server handles safely (parameterized queries prevent SQL injection)
        // Should not crash or return 500, and certainly should not execute SQL
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);

        // And: Can still query games (proves table wasn't dropped)
        var gamesRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        AddCookies(gamesRequest, cookies);
        var gamesResponse = await client.SendAsync(gamesRequest);
        Assert.Equal(HttpStatusCode.OK, gamesResponse.StatusCode);
    }

    [Fact]
    public async Task PostRequest_WithXssAttempt_IsSafelyHandled()
    {
        // Given: Authenticated user attempting XSS
        var user = await CreateTestUserAsync("xss-user", UserRole.User);
        var game = await CreateTestGameAsync("XSS Test Game");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // XSS payload
        var xssQuery = "<script>alert('XSS')</script>";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = JsonContent.Create(new { gameId = game.Id, query = xssQuery });
        AddCookies(request, cookies);

        // When: Request is sent
        var response = await client.SendAsync(request);

        // Then: Server handles safely (should escape/sanitize)
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);

        // NOTE: XSS protection is primarily client-side concern,
        // but server should not execute script tags
    }

    #endregion

    #region Malformed Requests

    [Fact]
    public async Task PostRequest_WithMalformedJson_ReturnsBadRequest()
    {
        // Given: Authenticated user with malformed JSON
        var user = await CreateTestUserAsync("malformed-json-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = new StringContent("{\"gameId\":\"test\",\"query\":", Encoding.UTF8, "application/json");
        AddCookies(request, cookies);

        // When: Request is sent with incomplete JSON
        var response = await client.SendAsync(request);

        // Then: Returns 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRequest_WithMissingRequiredFields_ReturnsBadRequest()
    {
        // Given: Authenticated user with missing required fields
        var user = await CreateTestUserAsync("missing-fields-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = JsonContent.Create(new { query = "test" }); // Missing gameId
        AddCookies(request, cookies);

        // When: Request is sent
        var response = await client.SendAsync(request);

        // Then: Returns 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostRequest_WithExtraUnknownFields_HandlesGracefully()
    {
        // Given: Authenticated user with extra unknown fields
        var user = await CreateTestUserAsync("extra-fields-user", UserRole.User);
        var game = await CreateTestGameAsync("Extra Fields Test Game");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
        request.Content = JsonContent.Create(new
        {
            gameId = game.Id,
            query = "test",
            unknownField1 = "value1",
            unknownField2 = 123,
            nestedUnknown = new { foo = "bar" }
        });
        AddCookies(request, cookies);

        // When: Request is sent
        var response = await client.SendAsync(request);

        // Then: Server handles gracefully (ignores unknown fields or processes normally)
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    #endregion

    #region Concurrent Operations

    [Fact]
    public async Task ConcurrentRequests_FromSameUser_HandleCorrectly()
    {
        // Given: Authenticated user making concurrent requests
        var user = await CreateTestUserAsync("concurrent-user", UserRole.User);
        var game = await CreateTestGameAsync("Concurrent Test Game");
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // When: Multiple concurrent requests are sent
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 5; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/agents/qa");
            request.Content = JsonContent.Create(new { gameId = game.Id, query = $"test query {i}" });
            AddCookies(request, cookies);
            tasks.Add(client.SendAsync(request));
        }

        var responses = await Task.WhenAll(tasks);

        // Then: All requests are handled correctly (no crashes)
        foreach (var response in responses)
        {
            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        // And: Most should succeed (some may be rate limited)
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.OK);
        Assert.True(successCount > 0, "At least some concurrent requests should succeed");
    }

    [Fact]
    public async Task ConcurrentLogins_WithSameUser_HandleCorrectly()
    {
        // Given: User attempting concurrent logins
        var user = await CreateTestUserAsync("concurrent-login-user", UserRole.User, "Password123!");
        var client = Factory.CreateHttpsClient();

        // When: Multiple concurrent login attempts
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 3; i++)
        {
            var loginPayload = new { email = user.Email, password = "Password123!" };
            tasks.Add(client.PostAsJsonAsync("/api/v1/auth/login", loginPayload));
        }

        var responses = await Task.WhenAll(tasks);

        // Then: All logins succeed (creates multiple valid sessions)
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.Contains("Set-Cookie"), "Should set session cookie");
        }

        // And: Each login creates a distinct session token
        var sessionTokens = responses
            .SelectMany(r => r.Headers.GetValues("Set-Cookie"))
            .Select(cookie => cookie.Split(';')[0].Split('=')[1])
            .ToList();

        Assert.Equal(3, sessionTokens.Distinct().Count());
    }

    [Fact]
    public async Task ConcurrentGameCreation_WithDifferentNames_Succeeds()
    {
        // Given: Editor attempting to create games with different names concurrently
        var editor = await CreateTestUserAsync("concurrent-game-editor", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = Factory.CreateHttpsClient();

        // When: Multiple concurrent game creation attempts with UNIQUE names
        var tasks = new List<Task<HttpResponseMessage>>();
        for (int i = 0; i < 3; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games");
            request.Content = JsonContent.Create(new { name = $"Concurrent Test Game {Guid.NewGuid()}", gameId = $"concurrent-game-{i}" });
            AddCookies(request, cookies);
            tasks.Add(client.SendAsync(request));
        }

        var responses = await Task.WhenAll(tasks);

        // Then: All succeed since names are unique
        var successCount = responses.Count(r => r.StatusCode == HttpStatusCode.Created);

        // All should succeed (or some may be rate limited)
        Assert.True(successCount >= 2,
            $"Expected at least 2 successes out of 3, got {successCount}");

        // NOTE: With unique names, concurrent creation should work fine
    }

    #endregion
}
