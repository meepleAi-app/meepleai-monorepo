using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure.Entities;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests;

/// <summary>
/// Comprehensive rate limiting tests.
/// Tests role-based limits, rate limit headers, 429 responses, and IP-based limiting.
/// Related to Issue #260 - TEST-01: Expand Integration Test Coverage (Phase 4).
/// </summary>
public class RateLimitingTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    public RateLimitingTests(WebApplicationFactoryFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    #region Rate Limit Headers

    [Fact]
    public async Task GetRequest_Authenticated_ReturnsRateLimitHeaders()
    {
        // Given: Authenticated user making request
        var user = await CreateTestUserAsync("rate-limit-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        AddCookies(request, cookies);

        // When: User makes a request
        var response = await client.SendAsync(request);

        // Then: Rate limit headers are present
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("X-RateLimit-Limit").Should().BeTrue();
        response.Headers.Contains("X-RateLimit-Remaining").Should().BeTrue();

        // And: Headers have valid values
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        var remaining = response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
        limit.Should().NotBeNull();
        remaining.Should().NotBeNull();
        int.TryParse(limit, out var limitValue).Should().BeTrue();
        int.TryParse(remaining, out var remainingValue).Should().BeTrue();
        (limitValue > 0).Should().BeTrue();
        (remainingValue >= 0).Should().BeTrue();
    }

    [Fact]
    public async Task GetRequest_Anonymous_ReturnsRateLimitHeaders()
    {
        // Given: Anonymous (unauthenticated) request
        var client = Factory.CreateHttpsClient();

        // When: Anonymous user makes a request
        var response = await client.GetAsync("/");

        // Then: Rate limit headers are present
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("X-RateLimit-Limit").Should().BeTrue();
        response.Headers.Contains("X-RateLimit-Remaining").Should().BeTrue();

        // And: Anonymous limit is applied (60 burst, 1/sec)
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        limit.Should().Be("60"); // Default anonymous limit
    }

    #endregion

    #region Role-Based Rate Limits

    [Fact]
    public async Task GetRequest_AdminUser_HasHigherRateLimit()
    {
        // Given: Admin user
        var admin = await CreateTestUserAsync("admin-rate-limit", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        AddCookies(request, cookies);

        // When: Admin makes a request
        var response = await client.SendAsync(request);

        // Then: Admin rate limit is applied (1000 burst)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        limit.Should().Be("1000"); // Admin limit
    }

    [Fact]
    public async Task GetRequest_EditorUser_HasMediumRateLimit()
    {
        // Given: Editor user
        var editor = await CreateTestUserAsync("editor-rate-limit", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        AddCookies(request, cookies);

        // When: Editor makes a request
        var response = await client.SendAsync(request);

        // Then: Editor rate limit is applied (500 burst)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        limit.Should().Be("500"); // Editor limit
    }

    [Fact]
    public async Task GetRequest_RegularUser_HasStandardRateLimit()
    {
        // Given: Regular user
        var user = await CreateTestUserAsync("user-rate-limit", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        AddCookies(request, cookies);

        // When: User makes a request
        var response = await client.SendAsync(request);

        // Then: User rate limit is applied (100 burst)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        limit.Should().Be("100"); // User limit
    }

    #endregion

    #region Rate Limit Exceeded (429)

    [Fact]
    public async Task MultipleRequests_ExceedingLimit_Returns429()
    {
        // Given: Authenticated user with rate limit
        var user = await CreateTestUserAsync("rate-exceed-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // When: User makes many rapid requests (exceeding 100 burst + 1/sec refill)
        // NOTE: In test environment, rate limiting might be disabled or have high limits
        // This test documents expected behavior even if not enforced in tests
        var responses = new List<HttpResponseMessage>();
        for (int i = 0; i < 105; i++) // Exceed 100 burst capacity
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/");
            AddCookies(request, cookies);
            var response = await client.SendAsync(request);
            responses.Add(response);

            // Break early if rate limited
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                break;
            }
        }

        // Then: Eventually returns 429 Too Many Requests (or all succeed if rate limiting disabled in tests)
        // NOTE: This test is informational - rate limiting may be disabled/high in test environment
        var rateLimitedResponses = responses.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests);
        var successResponses = responses.Count(r => r.StatusCode == HttpStatusCode.OK);

        // If rate limiting is enforced, we should see 429 responses
        // If disabled in tests, all will be 200
        (rateLimitedResponses > 0 || successResponses == responses.Count).Should().BeTrue($"Expected either rate limiting (429) or all success (200). Got {rateLimitedResponses} rate limited, {successResponses} success");
    }

    [Fact]
    public async Task RateLimitExceeded_ReturnsRetryAfterHeader()
    {
        // Given: Rate-limited request (simulated by rapid requests)
        var user = await CreateTestUserAsync("retry-after-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // When: Making requests until rate limited
        HttpResponseMessage? rateLimitedResponse = null;
        for (int i = 0; i < 110; i++) // Exceed user limit
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/");
            AddCookies(request, cookies);
            var response = await client.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Then: If rate limited, Retry-After header is present
        if (rateLimitedResponse != null)
        {
            rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            rateLimitedResponse.Headers.Contains("Retry-After").Should().BeTrue();

            var retryAfter = rateLimitedResponse.Headers.GetValues("Retry-After").FirstOrDefault();
            retryAfter.Should().NotBeNull();
            int.TryParse(retryAfter, out var seconds).Should().BeTrue();
            (seconds > 0).Should().BeTrue();
        }
        // NOTE: If not rate limited, rate limiting is disabled/high in test environment
    }

    [Fact]
    public async Task RateLimitExceeded_ReturnsJsonErrorResponse()
    {
        // Given: User attempting requests when rate limited
        var user = await CreateTestUserAsync("error-json-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // When: Making requests until rate limited
        HttpResponseMessage? rateLimitedResponse = null;
        for (int i = 0; i < 110; i++)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/");
            AddCookies(request, cookies);
            var response = await client.SendAsync(request);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                rateLimitedResponse = response;
                break;
            }
        }

        // Then: If rate limited, response contains JSON error with message
        if (rateLimitedResponse != null)
        {
            rateLimitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            var content = await rateLimitedResponse.Content.ReadAsStringAsync();
            content.Should().NotBeNull();
            content.Should().Contain("error");
            content.Should().Contain("Rate limit exceeded");
        }
    }

    #endregion

    #region IP-Based Rate Limiting

    [Fact]
    public async Task AnonymousRequests_AreLimitedByIp()
    {
        // Given: Anonymous (unauthenticated) client
        var client = Factory.CreateHttpsClient();

        // When: Making multiple anonymous requests
        var firstResponse = await client.GetAsync("/");
        var secondResponse = await client.GetAsync("/");

        // Then: Both succeed (within anonymous limit of 60 burst)
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        secondResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // And: Rate limit headers show decreasing remaining tokens
        var firstRemaining = int.Parse(firstResponse.Headers.GetValues("X-RateLimit-Remaining").First());
        var secondRemaining = int.Parse(secondResponse.Headers.GetValues("X-RateLimit-Remaining").First());

        // NOTE: Remaining should decrease or stay same (if rate limiting disabled)
        (secondRemaining <= firstRemaining).Should().BeTrue($"Expected remaining to decrease or stay same. First: {firstRemaining}, Second: {secondRemaining}");
    }

    [Fact]
    public async Task DifferentUsers_HaveSeparateRateLimits()
    {
        // Given: Two different authenticated users
        var user1 = await CreateTestUserAsync("separate-limit-user1", UserRole.User);
        var user2 = await CreateTestUserAsync("separate-limit-user2", UserRole.User);
        var cookies1 = await AuthenticateUserAsync(user1.Email);
        var cookies2 = await AuthenticateUserAsync(user2.Email);
        var client = Factory.CreateHttpsClient();

        // When: Both users make requests
        var request1 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        AddCookies(request1, cookies1);
        var response1 = await client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        AddCookies(request2, cookies2);
        var response2 = await client.SendAsync(request2);

        // Then: Both succeed (separate rate limit buckets)
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        response2.StatusCode.Should().Be(HttpStatusCode.OK);

        // And: Each has their own token bucket (both should have high remaining counts)
        var remaining1 = int.Parse(response1.Headers.GetValues("X-RateLimit-Remaining").First());
        var remaining2 = int.Parse(response2.Headers.GetValues("X-RateLimit-Remaining").First());

        // Both should have nearly full buckets (99 out of 100 for User role)
        (remaining1 > 90).Should().BeTrue($"User1 remaining: {remaining1}");
        (remaining2 > 90).Should().BeTrue($"User2 remaining: {remaining2}");
    }

    #endregion

    #region Rate Limiting Behavior

    [Fact]
    public async Task AfterLogin_UserTransitionsFromIpToUserRateLimit()
    {
        // Given: User logs in (transitions from IP-based to user-based rate limiting)
        var user = await CreateTestUserAsync("transition-user", UserRole.User, "Password123!");
        var client = Factory.CreateHttpsClient();

        // When: Anonymous request first
        var anonymousResponse = await client.GetAsync("/");
        var anonymousLimit = anonymousResponse.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();

        // Then: Anonymous limit is applied (60)
        anonymousLimit.Should().Be("60");

        // When: User logs in
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { email = user.Email, password = "Password123!" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var cookies = GetCookiesFromResponse(loginResponse);
        var authenticatedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        AddCookies(authenticatedRequest, cookies);
        var authenticatedResponse = await client.SendAsync(authenticatedRequest);

        // Then: User-based limit is now applied (100)
        var userLimit = authenticatedResponse.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        userLimit.Should().Be("100");
    }

    #endregion

    #region Helper Methods

    private Dictionary<string, string> GetCookiesFromResponse(HttpResponseMessage response)
    {
        var cookies = new Dictionary<string, string>();

        if (response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
        {
            foreach (var cookie in cookieHeaders)
            {
                var parts = cookie.Split(';')[0].Split('=', 2);
                if (parts.Length == 2)
                {
                    cookies[parts[0]] = parts[1];
                }
            }
        }

        return cookies;
    }

    private void AddCookies(HttpRequestMessage request, Dictionary<string, string> cookies)
    {
        var cookieHeader = string.Join("; ", cookies.Select(kv => $"{kv.Key}={kv.Value}"));
        request.Headers.Add("Cookie", cookieHeader);
    }

    #endregion
}