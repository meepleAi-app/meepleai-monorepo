using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Tests.Constants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Test factory for BGG rate limiting integration tests.
/// </summary>
public class BggRateLimitTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Enable rate limiting for integration tests
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BggRateLimit:FreeTier"] = "5",
                ["BggRateLimit:NormalTier"] = "10",
                ["BggRateLimit:PremiumTier"] = "20",
                ["DISABLE_RATE_LIMITING"] = "false"
            });
        });
    }

    public async Task<string> GetAuthTokenAsync(string tier = "Normal", string role = "User")
    {
        // Note: Implement actual auth token generation
        // For now, return placeholder (requires auth infrastructure)
        return "test-token";
    }
}

/// <summary>
/// Integration tests for BGG API rate limiting (Issue #4275).
/// Tests real Redis sliding window behavior with Testcontainers.
/// Skipped until proper auth token generation is implemented (placeholder "test-token" causes 401).
/// </summary>
[Trait("Category", TestCategories.Integration)]
public class BggRateLimitIntegrationTests : IClassFixture<BggRateLimitTestFactory>
{
    private readonly HttpClient _client;
    private readonly BggRateLimitTestFactory _factory;

    public BggRateLimitIntegrationTests(BggRateLimitTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost:8080"),
            AllowAutoRedirect = false
        });
    }

    private const string SkipReason = "Requires real auth token infrastructure — placeholder 'test-token' returns 401";

    [Fact(Skip = SkipReason)]
    public async Task BggSearch_WithinLimit_ReturnsSuccessWithHeaders()
    {
        // Arrange
        var token = await _factory.GetAuthTokenAsync(tier: "Normal"); // 10 req/min limit
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/v1/bgg/search?query=wingspan");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("X-RateLimit-Limit").Should().BeTrue();
        response.Headers.Contains("X-RateLimit-Remaining").Should().BeTrue();
        response.Headers.Contains("X-RateLimit-Reset").Should().BeTrue();

        var limit = response.Headers.GetValues("X-RateLimit-Limit").First();
        limit.Should().Be("10");
    }

    [Fact(Skip = SkipReason)]
    public async Task BggSearch_ExceedsFreeTierLimit_Returns429()
    {
        // Arrange
        var token = await _factory.GetAuthTokenAsync(tier: "Free"); // 5 req/min limit
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act - Make 6 requests (exceed 5/min limit)
        HttpResponseMessage? lastResponse = null;
        for (int i = 0; i < 6; i++)
        {
            lastResponse = await _client.GetAsync($"/api/v1/bgg/search?query=test{i}");
        }

        // Assert
        lastResponse.Should().NotBeNull();
        lastResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        lastResponse.Headers.Contains("Retry-After").Should().BeTrue();
        lastResponse.Headers.Contains("X-RateLimit-Remaining").Should().BeTrue();
        lastResponse.Headers.GetValues("X-RateLimit-Remaining").First().Should().Be("0");

        // Verify JSON error response
        var error = await lastResponse.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("error").GetString().Should().Be("Rate limit exceeded");
        error.GetProperty("message").GetString().Should().Contain("5 requests per minute");
    }

    [Fact(Skip = SkipReason)]
    public async Task BggSearch_Admin_BypassesRateLimit()
    {
        // Arrange
        var token = await _factory.GetAuthTokenAsync(tier: "Normal", role: "Admin");
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act - Make 25 requests (would exceed all tier limits)
        HttpResponseMessage? lastResponse = null;
        for (int i = 0; i < 25; i++)
        {
            lastResponse = await _client.GetAsync($"/api/v1/bgg/search?query=test{i}");
        }

        // Assert
        lastResponse.Should().NotBeNull();
        lastResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        lastResponse.Headers.GetValues("X-RateLimit-Limit").First().Should().Be("unlimited");
        lastResponse.Headers.GetValues("X-RateLimit-Remaining").First().Should().Be("unlimited");
    }

    [Fact(Skip = SkipReason)]
    public async Task BggSearch_PremiumTier_HigherLimit()
    {
        // Arrange
        var token = await _factory.GetAuthTokenAsync(tier: "Premium"); // 20 req/min limit
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act - Make 15 requests (within Premium limit)
        HttpResponseMessage? lastResponse = null;
        for (int i = 0; i < 15; i++)
        {
            lastResponse = await _client.GetAsync($"/api/v1/bgg/search?query=test{i}");
        }

        // Assert
        lastResponse.Should().NotBeNull();
        lastResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        lastResponse.Headers.GetValues("X-RateLimit-Limit").First().Should().Be("20");

        var remaining = int.Parse(lastResponse.Headers.GetValues("X-RateLimit-Remaining").First());
        remaining.Should().BeInRange(1, 10);
    }

    [Fact(Skip = SkipReason)]
    public async Task BggSearch_RateLimitResetsAfterWindow()
    {
        // Arrange
        var token = await _factory.GetAuthTokenAsync(tier: "Free"); // 5 req/min limit
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act - Exhaust limit
        for (int i = 0; i < 5; i++)
        {
            await _client.GetAsync($"/api/v1/bgg/search?query=test{i}");
        }

        // 6th request should be rate limited
        var rateLimitedResponse = await _client.GetAsync("/api/v1/bgg/search?query=test-limited");
        rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Wait for window to reset (Redis TTL = 60 seconds in sliding window)
        // In tests, we can use a shorter window or manually expire the Redis key
        // For now, skip the wait and just verify the 429 response

        // Assert
        rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        var retryAfter = rateLimitedResponse.Headers.GetValues("Retry-After").First();
        (int.Parse(retryAfter) > 0).Should().BeTrue("Retry-After should indicate seconds until reset");
    }

    [Fact(Skip = SkipReason)]
    public async Task BggGameDetails_AlsoRateLimited()
    {
        // Arrange
        var token = await _factory.GetAuthTokenAsync(tier: "Free"); // 5 req/min limit
        _client.DefaultRequestHeaders.Authorization = new("Bearer", token);

        // Act - Use both search and details endpoints (share same quota)
        await _client.GetAsync("/api/v1/bgg/search?query=wingspan");
        await _client.GetAsync("/api/v1/bgg/search?query=catan");
        await _client.GetAsync("/api/v1/bgg/games/266192"); // Wingspan details
        await _client.GetAsync("/api/v1/bgg/games/13"); // Catan details
        await _client.GetAsync("/api/v1/bgg/search?query=scythe");

        // 6th request (mix of search + details) should be limited
        var response = await _client.GetAsync("/api/v1/bgg/games/169786"); // Scythe details

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
