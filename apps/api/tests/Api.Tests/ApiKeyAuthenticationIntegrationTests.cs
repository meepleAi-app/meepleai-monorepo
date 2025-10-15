using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Api.Infrastructure.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Integration tests for API key authentication via X-API-Key header.
/// Tests the complete authentication flow through middleware and endpoints.
/// Related to Issue #370 - API-01: API Foundation and Authentication Infrastructure.
/// </summary>
public class ApiKeyAuthenticationIntegrationTests : IntegrationTestBase
{
    public ApiKeyAuthenticationIntegrationTests(WebApplicationFactoryFixture fixture) : base(fixture)
    {
    }

    #region Successful Authentication Tests

    [Fact]
    public async Task GetAuthMe_WithValidApiKey_ReturnsUserInfo()
    {
        // Given: A user with a valid API key
        var user = await CreateTestUserAsync("apikey-user", UserRole.Editor);
        var (plaintextKey, _) = await CreateTestApiKeyAsync(
            user.Id,
            "Test Key",
            new[] { "read", "write" });

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", plaintextKey);

        // When: User makes request with API key
        var response = await client.SendAsync(request);

        // Then: User info is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authResponse = await response.Content.ReadFromJsonAsync<Api.Models.AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.Equal(user.Email, authResponse.User.Email);
        Assert.Equal(user.DisplayName, authResponse.User.DisplayName);
        Assert.Equal("Editor", authResponse.User.Role);
    }

    [Fact]
    public async Task GetGames_WithValidApiKey_ReturnsGames()
    {
        // Given: A user with valid API key and a game exists
        var user = await CreateTestUserAsync("games-user", UserRole.User);
        var game = await CreateTestGameAsync("TestGame");
        var (plaintextKey, _) = await CreateTestApiKeyAsync(
            user.Id,
            "Games Key",
            new[] { "read" });

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
        request.Headers.Add("X-API-Key", plaintextKey);

        // When: User requests games list with API key
        var response = await client.SendAsync(request);

        // Then: Games are returned successfully
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("live")]
    [InlineData("test")]
    public async Task ApiKeyAuthentication_WithDifferentEnvironments_WorksCorrectly(string environment)
    {
        // Given: API key for specific environment
        var user = await CreateTestUserAsync($"env-{environment}-user", UserRole.User);
        var (plaintextKey, _) = await CreateTestApiKeyAsync(
            user.Id,
            $"{environment} Key",
            new[] { "read" },
            environment: environment);

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", plaintextKey);

        // When: Request is made with environment-specific key
        var response = await client.SendAsync(request);

        // Then: Authentication succeeds
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Failed Authentication Tests

    [Fact]
    public async Task GetAuthMe_WithInvalidApiKey_ReturnsUnauthorized()
    {
        // Given: Invalid API key
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", "mpl_test_invalid_key_does_not_exist");

        // When: User makes request with invalid key
        var response = await client.SendAsync(request);

        // Then: Request is unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthMe_WithoutApiKeyOrCookie_ReturnsUnauthorized()
    {
        // Given: No authentication credentials
        var client = Factory.CreateHttpsClient();

        // When: User makes request without any authentication
        var response = await client.GetAsync("/api/v1/auth/me");

        // Then: Request is unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthMe_WithExpiredApiKey_ReturnsUnauthorized()
    {
        // Given: User with expired API key
        var user = await CreateTestUserAsync("expired-key-user", UserRole.User);
        var expiresAt = DateTime.UtcNow.AddDays(-1); // Expired yesterday
        var (plaintextKey, _) = await CreateTestApiKeyAsync(
            user.Id,
            "Expired Key",
            new[] { "read" },
            expiresAt: expiresAt);

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", plaintextKey);

        // When: User makes request with expired key
        var response = await client.SendAsync(request);

        // Then: Request is unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthMe_WithRevokedApiKey_ReturnsUnauthorized()
    {
        // Given: User with revoked API key
        var user = await CreateTestUserAsync("revoked-key-user", UserRole.User);
        var (plaintextKey, apiKeyEntity) = await CreateTestApiKeyAsync(
            user.Id,
            "Revoked Key",
            new[] { "read" });

        // Revoke the key
        using (var scope = Factory.Services.CreateScope())
        {
            var apiKeyService = scope.ServiceProvider.GetRequiredService<Services.ApiKeyAuthenticationService>();
            await apiKeyService.RevokeApiKeyAsync(apiKeyEntity.Id, user.Id);
        }

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", plaintextKey);

        // When: User makes request with revoked key
        var response = await client.SendAsync(request);

        // Then: Request is unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthMe_WithInactiveApiKey_ReturnsUnauthorized()
    {
        // Given: User with inactive API key
        var user = await CreateTestUserAsync("inactive-key-user", UserRole.User);
        var (plaintextKey, apiKeyEntity) = await CreateTestApiKeyAsync(
            user.Id,
            "Inactive Key",
            new[] { "read" });

        // Mark key as inactive
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Infrastructure.MeepleAiDbContext>();
            var key = await db.ApiKeys.FindAsync(apiKeyEntity.Id);
            if (key != null)
            {
                key.IsActive = false;
                await db.SaveChangesAsync();
            }
        }

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", plaintextKey);

        // When: User makes request with inactive key
        var response = await client.SendAsync(request);

        // Then: Request is unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetAuthMe_WithEmptyApiKeyHeader_FallsThroughToCookieAuth()
    {
        // Given: Empty X-API-Key header (should fall through to cookie auth)
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", "");

        // When: User makes request with empty API key
        var response = await client.SendAsync(request);

        // Then: Falls through to cookie auth (which will fail with Unauthorized)
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Dual Authentication Tests

    [Fact]
    public async Task GetAuthMe_WithBothApiKeyAndCookie_PrefersApiKey()
    {
        // Given: Two different users - one with API key, one with cookie
        var apiKeyUser = await CreateTestUserAsync("apikey-dual-user", UserRole.Admin);
        var cookieUser = await CreateTestUserAsync("cookie-dual-user", UserRole.User);

        var (plaintextKey, _) = await CreateTestApiKeyAsync(
            apiKeyUser.Id,
            "Dual Auth Key",
            new[] { "read" });

        var cookies = await AuthenticateUserAsync(cookieUser.Email);

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", plaintextKey);
        AddCookies(request, cookies);

        // When: Request has both API key and cookie
        var response = await client.SendAsync(request);

        // Then: API key takes precedence (returns API key user, not cookie user)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authResponse = await response.Content.ReadFromJsonAsync<Api.Models.AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.Equal(apiKeyUser.Email, authResponse.User.Email);
        Assert.Equal("Admin", authResponse.User.Role); // API key user is Admin
    }

    [Fact]
    public async Task GetAuthMe_WithCookieOnly_UsesCookieAuthentication()
    {
        // Given: User authenticated with cookie (no API key)
        var user = await CreateTestUserAsync("cookie-only-user", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(user.Email);

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        AddCookies(request, cookies);

        // When: Request uses only cookie auth
        var response = await client.SendAsync(request);

        // Then: Cookie authentication works
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var authResponse = await response.Content.ReadFromJsonAsync<Api.Models.AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.Equal(user.Email, authResponse.User.Email);
        Assert.Equal("Editor", authResponse.User.Role);
    }

    #endregion

    #region API Key Lifecycle Tests

    [Fact]
    public async Task ApiKey_AfterRevocation_CannotBeUsedAgain()
    {
        // Given: User with active API key
        var user = await CreateTestUserAsync("lifecycle-user", UserRole.User);
        var (plaintextKey, apiKeyEntity) = await CreateTestApiKeyAsync(
            user.Id,
            "Lifecycle Key",
            new[] { "read" });

        var client = Factory.CreateHttpsClient();

        // When: First request succeeds
        var request1 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request1.Headers.Add("X-API-Key", plaintextKey);
        var response1 = await client.SendAsync(request1);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        // And: Key is revoked
        using (var scope = Factory.Services.CreateScope())
        {
            var apiKeyService = scope.ServiceProvider.GetRequiredService<Services.ApiKeyAuthenticationService>();
            await apiKeyService.RevokeApiKeyAsync(apiKeyEntity.Id, user.Id);
        }

        // And: Second request with same key
        var request2 = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request2.Headers.Add("X-API-Key", plaintextKey);
        var response2 = await client.SendAsync(request2);

        // Then: Second request fails
        Assert.Equal(HttpStatusCode.Unauthorized, response2.StatusCode);
    }

    [Fact]
    public async Task ApiKey_NearExpiration_StillWorks()
    {
        // Given: API key expiring in 1 hour (still valid)
        var user = await CreateTestUserAsync("near-expiry-user", UserRole.User);
        var expiresAt = DateTime.UtcNow.AddHours(1);
        var (plaintextKey, _) = await CreateTestApiKeyAsync(
            user.Id,
            "Near Expiry Key",
            new[] { "read" },
            expiresAt: expiresAt);

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", plaintextKey);

        // When: Request is made with soon-to-expire key
        var response = await client.SendAsync(request);

        // Then: Request succeeds (key still valid)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Non-API Path Tests

    [Fact]
    public async Task HealthCheck_WithoutApiKey_WorksCorrectly()
    {
        // Given: Health check endpoint (not under /api/* path)
        var client = Factory.CreateHttpsClient();

        // When: Health check is requested without API key
        var response = await client.GetAsync("/health/ready");

        // Then: Health check works (API key middleware doesn't process it)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RootEndpoint_WithoutApiKey_WorksCorrectly()
    {
        // Given: Root endpoint (not under /api/* path)
        var client = Factory.CreateHttpsClient();

        // When: Root is requested without API key
        var response = await client.GetAsync("/");

        // Then: Root endpoint works
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Error Response Format Tests

    [Fact]
    public async Task InvalidApiKey_ReturnsJsonErrorResponse()
    {
        // Given: Invalid API key
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        request.Headers.Add("X-API-Key", "mpl_test_totally_invalid");

        // When: Request is made with invalid key
        var response = await client.SendAsync(request);

        // Then: Response is JSON with error details
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(errorResponse);
        Assert.Equal("invalid_api_key", errorResponse.error);
        Assert.NotNull(errorResponse.message);
        Assert.NotNull(errorResponse.correlationId);
    }

    #endregion

    #region Helper Classes

    private class ErrorResponse
    {
        public string error { get; set; } = string.Empty;
        public string message { get; set; } = string.Empty;
        public string? correlationId { get; set; }
    }

    #endregion
}
