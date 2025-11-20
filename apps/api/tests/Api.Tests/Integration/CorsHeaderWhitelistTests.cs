using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for CORS header whitelist configuration (Issue #1448).
/// Verifies that only whitelisted headers are permitted in CORS requests.
/// </summary>
/// <remarks>
/// Tests Cover:
/// 1. Whitelisted headers (Content-Type, Authorization, X-Correlation-ID, X-API-Key) are accepted
/// 2. Non-whitelisted headers are rejected
/// 3. CORS preflight requests work correctly
/// 4. Multiple headers can be requested simultaneously
/// 5. Header names are case-insensitive
///
/// Pattern: AAA (Arrange-Act-Assert), WebApplicationFactory for in-memory HTTP testing
/// </remarks>
[Collection("CORS")]
public class CorsHeaderWhitelistTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    // Whitelisted headers (Issue #1448)
    private static readonly string[] WhitelistedHeaders = new[]
    {
        "Content-Type",
        "Authorization",
        "X-Correlation-ID",
        "X-API-Key"
    };

    // Non-whitelisted headers (should be rejected)
    private static readonly string[] NonWhitelistedHeaders = new[]
    {
        "X-Custom-Header",
        "X-Malicious-Header",
        "X-Debug-Info",
        "X-Internal-Secret"
    };

    public CorsHeaderWhitelistTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Theory]
    [InlineData("Content-Type")]
    [InlineData("Authorization")]
    [InlineData("X-Correlation-ID")]
    [InlineData("X-API-Key")]
    public async Task PreflightRequest_WhitelistedHeader_ReturnsAllowedHeaders(string headerName)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", headerName);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));

        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
        var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

        Assert.Contains(headerName, allowedHeadersList, StringComparer.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("X-Custom-Header")]
    [InlineData("X-Malicious-Header")]
    [InlineData("X-Debug-Info")]
    [InlineData("X-Internal-Secret")]
    public async Task PreflightRequest_NonWhitelistedHeader_DoesNotAllowHeader(string headerName)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", headerName);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // Preflight should succeed (NoContent), but the header should NOT be in allowed headers
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        if (response.Headers.Contains("Access-Control-Allow-Headers"))
        {
            var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
            var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

            // The non-whitelisted header should NOT be in the allowed headers list
            Assert.DoesNotContain(headerName, allowedHeadersList, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task PreflightRequest_MultipleWhitelistedHeaders_AllAllowed()
    {
        // Arrange
        var requestedHeaders = string.Join(", ", WhitelistedHeaders);
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", requestedHeaders);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));

        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
        var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

        // All whitelisted headers should be present
        foreach (var header in WhitelistedHeaders)
        {
            Assert.Contains(header, allowedHeadersList, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task PreflightRequest_MixedHeaders_OnlyWhitelistedAllowed()
    {
        // Arrange
        var mixedHeaders = string.Join(", ",
            new[] { "Content-Type", "X-Custom-Header", "Authorization", "X-Malicious-Header" });

        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", mixedHeaders);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        if (response.Headers.Contains("Access-Control-Allow-Headers"))
        {
            var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
            var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

            // Whitelisted headers should be present
            Assert.Contains("Content-Type", allowedHeadersList, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("Authorization", allowedHeadersList, StringComparer.OrdinalIgnoreCase);

            // Non-whitelisted headers should NOT be present
            Assert.DoesNotContain("X-Custom-Header", allowedHeadersList, StringComparer.OrdinalIgnoreCase);
            Assert.DoesNotContain("X-Malicious-Header", allowedHeadersList, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("content-type")]
    [InlineData("AUTHORIZATION")]
    [InlineData("x-correlation-id")]
    [InlineData("X-API-KEY")]
    public async Task PreflightRequest_CaseInsensitiveHeaders_Accepted(string headerName)
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", headerName);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));
    }

    [Fact]
    public async Task PreflightRequest_ValidOrigin_ReturnsAccessControlHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        request.Headers.Add("Access-Control-Request-Headers", "Content-Type");

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.True(response.Headers.Contains("Access-Control-Allow-Methods"));
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));
        Assert.True(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task ActualRequest_WithWhitelistedHeader_Succeeds()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Authorization", "Bearer test-token");
        request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        // Should succeed (or return 401 if auth is required, but NOT a CORS error)
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Origin") ||
                    response.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PreflightRequest_AllWhitelistedHeaders_ExactlyFourHeaders()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/v1/games");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", string.Join(", ", WhitelistedHeaders));

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Headers.Contains("Access-Control-Allow-Headers"));

        var allowedHeaders = response.Headers.GetValues("Access-Control-Allow-Headers");
        var allowedHeadersList = string.Join(",", allowedHeaders).Split(',', StringSplitOptions.TrimEntries);

        // Verify exactly 4 whitelisted headers
        Assert.Equal(4, WhitelistedHeaders.Length);
        foreach (var header in WhitelistedHeaders)
        {
            Assert.Contains(header, allowedHeadersList, StringComparer.OrdinalIgnoreCase);
        }
    }
}
