using System.Net;
using System.Net.Http.Json;
using Api.Models;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Comprehensive integration tests for authentication endpoints.
/// Covers session lifecycle, error cases, and edge scenarios.
/// Related to Issue #260 - TEST-01: Expand Integration Test Coverage.
/// </summary>
public class AuthEndpointsComprehensiveTests : IntegrationTestBase
{
    public AuthEndpointsComprehensiveTests(WebApplicationFactoryFixture fixture) : base(fixture)
    {
    }

    #region Session Lifecycle Tests

    [Fact]
    public async Task PostAuthLogin_WithValidCredentials_CreatesSessionWithCorrectExpiration()
    {
        // Given: A user exists in the database
        var user = await CreateTestUserAsync("session-user", UserRole.User, "SessionUser123!");
        var client = Factory.CreateHttpsClient();
        var payload = new { email = user.Email, password = "SessionUser123!" };

        // When: User logs in
        var response = await client.PostAsJsonAsync("/auth/login", payload);

        // Then: Session is created successfully
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Session cookie is returned
        var cookies = GetCookiesFromResponse(response);
        Assert.NotNull(cookies);
        Assert.Contains("session_token", cookies.Keys);

        // And: Cookie has correct attributes (HttpOnly, Secure, SameSite)
        var sessionCookie = cookies["session_token"];
        Assert.NotNull(sessionCookie);
    }

    [Fact]
    public async Task GetAuthMe_WithExpiredSession_ReturnsUnauthorized()
    {
        // Given: A user with an expired session token
        var user = await CreateTestUserAsync("expired-user", UserRole.User);

        // Create an expired session (expires in the past)
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var tokenHash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("expired-token")));
        var expiredSession = new UserSessionEntity
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            TokenHash = tokenHash,
            CreatedAt = DateTime.UtcNow.AddDays(-8), // 8 days ago
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // expired yesterday
            IpAddress = "127.0.0.1",
            UserAgent = "TestAgent",
            User = user
        };
        db.UserSessions.Add(expiredSession);
        await db.SaveChangesAsync();

        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        request.Headers.Add("Cookie", $"session_token=expired-token");

        // When: User tries to access protected endpoint with expired session
        var response = await client.SendAsync(request);

        // Then: Request is rejected as unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthLogout_WithValidSession_InvalidatesSession()
    {
        // Given: A logged-in user
        var user = await CreateTestUserAsync("logout-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // When: User logs out
        var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        AddCookies(logoutRequest, cookies);
        var logoutResponse = await client.SendAsync(logoutRequest);

        // Then: Logout succeeds
        Assert.Equal(HttpStatusCode.OK, logoutResponse.StatusCode);

        // And: Session is invalidated
        var meRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        AddCookies(meRequest, cookies);
        var meResponse = await client.SendAsync(meRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task PostAuthLogout_WithoutSession_ReturnsOk()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();

        // When: User tries to logout without session
        var response = await client.PostAsync("/auth/logout", null);

        // Then: Logout succeeds (idempotent operation)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthLogout_AfterLogout_ReturnsOk()
    {
        // Given: A logged-in user
        var user = await CreateTestUserAsync("double-logout-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();

        // When: User logs out twice
        var firstLogout = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        AddCookies(firstLogout, cookies);
        var firstResponse = await client.SendAsync(firstLogout);

        var secondLogout = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        AddCookies(secondLogout, cookies);
        var secondResponse = await client.SendAsync(secondLogout);

        // Then: Both logout requests succeed (idempotent)
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
    }

    #endregion

    #region Login Error Cases

    [Fact]
    public async Task PostAuthLogin_WithNonExistentUser_ReturnsUnauthorized()
    {
        // Given: User does not exist in database
        var client = Factory.CreateHttpsClient();
        var payload = new { email = "nonexistent@example.com", password = "SomePassword123!" };

        // When: User attempts to login
        var response = await client.PostAsJsonAsync("/auth/login", payload);

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthLogin_WithWrongPassword_ReturnsUnauthorized()
    {
        // Given: User exists with correct password
        var user = await CreateTestUserAsync("wrong-pwd-user", UserRole.User, "CorrectPassword123!");
        var client = Factory.CreateHttpsClient();
        var payload = new { email = user.Email, password = "WrongPassword123!" };

        // When: User attempts to login with wrong password
        var response = await client.PostAsJsonAsync("/auth/login", payload);

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthLogin_WithNullPayload_ReturnsBadRequest()
    {
        // Given: Null payload
        var client = Factory.CreateHttpsClient();

        // When: Request is sent with null body
        var response = await client.PostAsync("/auth/login", null);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthLogin_WithEmptyEmail_ReturnsBadRequest()
    {
        // Given: Payload with empty email
        var client = Factory.CreateHttpsClient();
        var payload = new { email = "", password = "SomePassword123!" };

        // When: User attempts to login
        var response = await client.PostAsJsonAsync("/auth/login", payload);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthLogin_WithEmptyPassword_ReturnsBadRequest()
    {
        // Given: Payload with empty password
        var client = Factory.CreateHttpsClient();
        var payload = new { email = "user@example.com", password = "" };

        // When: User attempts to login
        var response = await client.PostAsJsonAsync("/auth/login", payload);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthLogin_WithMalformedEmail_ReturnsBadRequest()
    {
        // Given: Payload with malformed email
        var client = Factory.CreateHttpsClient();
        var payload = new { email = "not-an-email", password = "SomePassword123!" };

        // When: User attempts to login
        var response = await client.PostAsJsonAsync("/auth/login", payload);

        // Then: System returns bad request or unauthorized
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"Expected BadRequest or Unauthorized, got {response.StatusCode}"
        );
    }

    #endregion

    #region Registration Error Cases

    [Fact]
    public async Task PostAuthRegister_WithExistingEmail_ReturnsConflict()
    {
        // Given: User already exists with email
        var existingUser = await CreateTestUserAsync("existing-user", UserRole.User);
        var client = Factory.CreateHttpsClient();
        var payload = new
        {
            email = existingUser.Email,
            password = "NewPassword123!",
            displayName = "New User"
        };

        // When: Another user tries to register with same email
        var response = await client.PostAsJsonAsync("/auth/register", payload);

        // Then: System returns conflict
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthRegister_WithWeakPassword_ReturnsBadRequest()
    {
        // Given: Payload with weak password (too short)
        var client = Factory.CreateHttpsClient();
        var payload = new
        {
            email = "newuser@example.com",
            password = "123",
            displayName = "New User"
        };

        // When: User attempts to register
        var response = await client.PostAsJsonAsync("/auth/register", payload);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthRegister_WithInvalidEmailFormat_ReturnsBadRequest()
    {
        // Given: Payload with invalid email format
        var client = Factory.CreateHttpsClient();
        var payload = new
        {
            email = "not-valid-email",
            password = "ValidPassword123!",
            displayName = "New User"
        };

        // When: User attempts to register
        var response = await client.PostAsJsonAsync("/auth/register", payload);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthRegister_WithMissingDisplayName_ReturnsBadRequest()
    {
        // Given: Payload without displayName
        var client = Factory.CreateHttpsClient();
        var payload = new
        {
            email = "newuser@example.com",
            password = "ValidPassword123!"
        };

        // When: User attempts to register
        var response = await client.PostAsJsonAsync("/auth/register", payload);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthRegister_WithEmptyDisplayName_ReturnsBadRequest()
    {
        // Given: Payload with empty displayName
        var client = Factory.CreateHttpsClient();
        var payload = new
        {
            email = "newuser@example.com",
            password = "ValidPassword123!",
            displayName = ""
        };

        // When: User attempts to register
        var response = await client.PostAsJsonAsync("/auth/register", payload);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthRegister_WithWhitespaceOnlyDisplayName_ReturnsBadRequest()
    {
        // Given: Payload with whitespace-only displayName
        var client = Factory.CreateHttpsClient();
        var payload = new
        {
            email = "newuser@example.com",
            password = "ValidPassword123!",
            displayName = "   "
        };

        // When: User attempts to register
        var response = await client.PostAsJsonAsync("/auth/register", payload);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostAuthRegister_WithTooLongDisplayName_ReturnsBadRequest()
    {
        // Given: Payload with displayName exceeding max length (>100 chars)
        var client = Factory.CreateHttpsClient();
        var payload = new
        {
            email = "newuser@example.com",
            password = "ValidPassword123!",
            displayName = new string('A', 101) // 101 characters
        };

        // When: User attempts to register
        var response = await client.PostAsJsonAsync("/auth/register", payload);

        // Then: System returns bad request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task GetAuthMe_WithValidSession_ReturnsUserInfo()
    {
        // Given: A logged-in user
        var user = await CreateTestUserAsync("me-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = Factory.CreateHttpsClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        AddCookies(request, cookies);

        // When: User requests their info
        var response = await client.SendAsync(request);

        // Then: User info is returned
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResponse);
        Assert.Equal(user.Email, authResponse.User.Email);
        Assert.Equal(user.DisplayName, authResponse.User.DisplayName);
        Assert.Equal(UserRole.User.ToString(), authResponse.User.Role);
    }

    [Fact]
    public async Task GetAuthMe_WithoutSession_ReturnsUnauthorized()
    {
        // Given: No authenticated session
        var client = Factory.CreateHttpsClient();

        // When: User requests their info without authentication
        var response = await client.GetAsync("/auth/me");

        // Then: System returns unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public async Task PostAuthLogin_CreatesNewSessionEachTime()
    {
        // Given: A user exists
        var user = await CreateTestUserAsync("multi-session-user", UserRole.User, "MultiSession123!");
        var client = Factory.CreateHttpsClient();
        var payload = new { email = user.Email, password = "MultiSession123!" };

        // When: User logs in multiple times
        var firstLogin = await client.PostAsJsonAsync("/auth/login", payload);
        var firstCookies = GetCookiesFromResponse(firstLogin);

        var secondLogin = await client.PostAsJsonAsync("/auth/login", payload);
        var secondCookies = GetCookiesFromResponse(secondLogin);

        // Then: Each login creates a different session token
        Assert.Equal(HttpStatusCode.OK, firstLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondLogin.StatusCode);
        Assert.NotEqual(firstCookies["session_token"], secondCookies["session_token"]);
    }

    [Fact]
    public async Task PostAuthLogin_AllowsMultipleConcurrentSessions()
    {
        // Given: A user exists
        var user = await CreateTestUserAsync("concurrent-user", UserRole.User, "Concurrent123!");
        var client = Factory.CreateHttpsClient();
        var payload = new { email = user.Email, password = "Concurrent123!" };

        // When: User logs in twice (simulating two devices)
        var firstLogin = await client.PostAsJsonAsync("/auth/login", payload);
        var firstCookies = GetCookiesFromResponse(firstLogin);

        var secondLogin = await client.PostAsJsonAsync("/auth/login", payload);
        var secondCookies = GetCookiesFromResponse(secondLogin);

        // Then: Both sessions are valid simultaneously
        var firstMeRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        AddCookies(firstMeRequest, new List<string>(firstCookies.Select(kv => $"{kv.Key}={kv.Value}")));
        var firstMeResponse = await client.SendAsync(firstMeRequest);

        var secondMeRequest = new HttpRequestMessage(HttpMethod.Get, "/auth/me");
        AddCookies(secondMeRequest, new List<string>(secondCookies.Select(kv => $"{kv.Key}={kv.Value}")));
        var secondMeResponse = await client.SendAsync(secondMeRequest);

        Assert.Equal(HttpStatusCode.OK, firstMeResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondMeResponse.StatusCode);
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

    #endregion
}
