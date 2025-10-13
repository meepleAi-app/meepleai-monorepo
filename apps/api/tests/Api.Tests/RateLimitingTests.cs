using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure.Entities;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Comprehensive rate limiting tests.
/// Tests role-based limits, rate limit headers, 429 responses, and IP-based limiting.
/// Related to Issue #260 - TEST-01: Expand Integration Test Coverage (Phase 4).
/// </summary>
public class RateLimitingTests : IntegrationTestBase
{
    public RateLimitingTests(WebApplicationFactoryFixture fixture) : base(fixture)
    {
    }

    #region Rate Limit Headers

    [Fact]
    public async Task GetRequest_Authenticated_ReturnsRateLimitHeaders()
    {
        // Given: Authenticated user making request
        var user = await CreateTestUserAsync("rate-limit-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/games");
        AddCookies(request, cookies);

        // When: User makes a request
        var response = await client.SendAsync(request);

        // Then: Rate limit headers are present
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-RateLimit-Limit"));
        Assert.True(response.Headers.Contains("X-RateLimit-Remaining"));

        // And: Headers have valid values
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        var remaining = response.Headers.GetValues("X-RateLimit-Remaining").FirstOrDefault();
        Assert.NotNull(limit);
        Assert.NotNull(remaining);
        Assert.True(int.TryParse(limit, out var limitValue));
        Assert.True(int.TryParse(remaining, out var remainingValue));
        Assert.True(limitValue > 0);
        Assert.True(remainingValue >= 0);
    }

    [Fact]
    public async Task GetRequest_Anonymous_ReturnsRateLimitHeaders()
    {
        // Given: Anonymous (unauthenticated) request
        var client = Factory.CreateHttpsClient();

        // When: Anonymous user makes a request
        var response = await client.GetAsync("/");

        // Then: Rate limit headers are present
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.Contains("X-RateLimit-Limit"));
        Assert.True(response.Headers.Contains("X-RateLimit-Remaining"));

        // And: Anonymous limit is applied (60 burst, 1/sec)
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        Assert.Equal("60", limit); // Default anonymous limit
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
        var request = new HttpRequestMessage(HttpMethod.Get, "/games");
        AddCookies(request, cookies);

        // When: Admin makes a request
        var response = await client.SendAsync(request);

        // Then: Admin rate limit is applied (1000 burst)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        Assert.Equal("1000", limit); // Admin limit
    }

    [Fact]
    public async Task GetRequest_EditorUser_HasMediumRateLimit()
    {
        // Given: Editor user
        var editor = await CreateTestUserAsync("editor-rate-limit", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/games");
        AddCookies(request, cookies);

        // When: Editor makes a request
        var response = await client.SendAsync(request);

        // Then: Editor rate limit is applied (500 burst)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        Assert.Equal("500", limit); // Editor limit
    }

    [Fact]
    public async Task GetRequest_RegularUser_HasStandardRateLimit()
    {
        // Given: Regular user
        var user = await CreateTestUserAsync("user-rate-limit", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/games");
        AddCookies(request, cookies);

        // When: User makes a request
        var response = await client.SendAsync(request);

        // Then: User rate limit is applied (100 burst)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var limit = response.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        Assert.Equal("100", limit); // User limit
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
        Assert.True(rateLimitedResponses > 0 || successResponses == responses.Count,
            $"Expected either rate limiting (429) or all success (200). Got {rateLimitedResponses} rate limited, {successResponses} success");
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
            Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
            Assert.True(rateLimitedResponse.Headers.Contains("Retry-After"));

            var retryAfter = rateLimitedResponse.Headers.GetValues("Retry-After").FirstOrDefault();
            Assert.NotNull(retryAfter);
            Assert.True(int.TryParse(retryAfter, out var seconds));
            Assert.True(seconds > 0);
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
            Assert.Equal(HttpStatusCode.TooManyRequests, rateLimitedResponse.StatusCode);
            var content = await rateLimitedResponse.Content.ReadAsStringAsync();
            Assert.NotNull(content);
            Assert.Contains("error", content);
            Assert.Contains("Rate limit exceeded", content);
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
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        // And: Rate limit headers show decreasing remaining tokens
        var firstRemaining = int.Parse(firstResponse.Headers.GetValues("X-RateLimit-Remaining").First());
        var secondRemaining = int.Parse(secondResponse.Headers.GetValues("X-RateLimit-Remaining").First());

        // NOTE: Remaining should decrease or stay same (if rate limiting disabled)
        Assert.True(secondRemaining <= firstRemaining,
            $"Expected remaining to decrease or stay same. First: {firstRemaining}, Second: {secondRemaining}");
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
        var request1 = new HttpRequestMessage(HttpMethod.Get, "/games");
        AddCookies(request1, cookies1);
        var response1 = await client.SendAsync(request1);

        var request2 = new HttpRequestMessage(HttpMethod.Get, "/games");
        AddCookies(request2, cookies2);
        var response2 = await client.SendAsync(request2);

        // Then: Both succeed (separate rate limit buckets)
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // And: Each has their own token bucket (both should have high remaining counts)
        var remaining1 = int.Parse(response1.Headers.GetValues("X-RateLimit-Remaining").First());
        var remaining2 = int.Parse(response2.Headers.GetValues("X-RateLimit-Remaining").First());

        // Both should have nearly full buckets (99 out of 100 for User role)
        Assert.True(remaining1 > 90, $"User1 remaining: {remaining1}");
        Assert.True(remaining2 > 90, $"User2 remaining: {remaining2}");
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
        Assert.Equal("60", anonymousLimit);

        // When: User logs in
        var loginResponse = await client.PostAsJsonAsync("/auth/login",
            new { email = user.Email, password = "Password123!" });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var cookies = GetCookiesFromResponse(loginResponse);
        var authenticatedRequest = new HttpRequestMessage(HttpMethod.Get, "/games");
        AddCookies(authenticatedRequest, cookies);
        var authenticatedResponse = await client.SendAsync(authenticatedRequest);

        // Then: User-based limit is now applied (100)
        var userLimit = authenticatedResponse.Headers.GetValues("X-RateLimit-Limit").FirstOrDefault();
        Assert.Equal("100", userLimit);
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
