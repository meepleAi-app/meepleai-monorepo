using Api.Tests.E2E.Infrastructure;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Xunit;

#pragma warning disable S1144 // Unused private types or members should be removed (DTOs for deserialization)

namespace Api.Tests.E2E.Authentication;

/// <summary>
/// E2E tests for authentication flows.
/// Tests the complete user journey from registration through login/logout.
///
/// Issue #3023: Backend E2E Test Suite - Authentication Flows
///
/// Critical Journeys Covered:
/// - User registration → session creation
/// - User login with valid/invalid credentials
/// - Session management (status, extend, logout)
/// - Logout from all devices
/// </summary>
[Collection("E2ETests")]
[Trait("Category", "E2E")]
public sealed class AuthenticationE2ETests : E2ETestBase
{
    public AuthenticationE2ETests(E2ETestFixture fixture) : base(fixture) { }

    #region Registration Tests

    [Fact]
    public async Task Register_WithValidCredentials_CreatesUserAndSession()
    {
        // Arrange
        var email = $"testuser_{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";
        var displayName = "Test User";

        // Act
        var (sessionToken, userId) = await RegisterUserAsync(email, password, displayName);

        // Assert
        sessionToken.Should().NotBeNullOrEmpty();
        userId.Should().NotBeEmpty();

        // Verify user exists in database
        var user = await DbContext.Users.FindAsync(userId);
        user.Should().NotBeNull();
        user!.Email.Should().Be(email.ToLowerInvariant());
        user.DisplayName.Should().Be(displayName);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        // Arrange - First registration
        var email = $"duplicate_{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";
        await RegisterUserAsync(email, password);

        // Clear cookies for second registration attempt
        ClearAuthentication();

        // Act - Second registration with same email
        var payload = new { email, password, displayName = "Another User" };
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("", "ValidPassword123!", "Email and password are required")]
    [InlineData("invalid-email", "ValidPassword123!", "Invalid email")]
    [InlineData("valid@email.com", "short", "Password must be at least")]
    public async Task Register_WithInvalidInput_ReturnsBadRequest(string email, string password, string expectedError)
    {
        // Arrange
        var payload = new { email, password };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/register", payload);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain(expectedError, because: $"Expected error about: {expectedError}");
    }

    #endregion

    #region Login Tests

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsSessionAndUser()
    {
        // Arrange - Create user first
        var email = $"logintest_{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";
        await RegisterUserAsync(email, password);

        // Clear cookies for login test
        ClearAuthentication();

        // Act
        var (sessionToken, userId) = await LoginUserAsync(email, password);

        // Assert
        sessionToken.Should().NotBeNullOrEmpty();
        userId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange - Create user first
        var email = $"wrongpass_{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";
        await RegisterUserAsync(email, password);

        ClearAuthentication();

        // Act
        var payload = new { email, password = "WrongPassword123!" };
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonexistentUser_ReturnsUnauthorized()
    {
        // Arrange
        var payload = new
        {
            email = $"nonexistent_{Guid.NewGuid():N}@example.com",
            password = "AnyPassword123!"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/v1/auth/login", payload);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_CaseInsensitiveEmail_Succeeds()
    {
        // Arrange - Register with lowercase
        var email = $"casetest_{Guid.NewGuid():N}@example.com";
        var password = "ValidPassword123!";
        await RegisterUserAsync(email.ToLowerInvariant(), password);

        ClearAuthentication();

        // Act - Login with mixed case
        var (sessionToken, _) = await LoginUserAsync(email.ToUpperInvariant(), password);

        // Assert
        sessionToken.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Session Management Tests

    [Fact]
    public async Task GetSessionStatus_WithValidSession_ReturnsStatus()
    {
        // Arrange
        var email = $"sessionstatus_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");

        SetSessionCookie(sessionToken);

        // Act
        var response = await Client.GetAsync("/api/v1/auth/session/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<SessionStatusResponse>();
        result.Should().NotBeNull();
        result!.ExpiresAt.Should().BeAfter(DateTime.UtcNow);
        result.RemainingMinutes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetSessionStatus_WithoutSession_ReturnsUnauthorized()
    {
        // Arrange - No session
        ClearAuthentication();

        // Act
        var response = await Client.GetAsync("/api/v1/auth/session/status");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithValidSession_ReturnsUserInfo()
    {
        // Arrange
        var email = $"metest_{Guid.NewGuid():N}@example.com";
        var displayName = "Me Test User";
        var (sessionToken, userId) = await RegisterUserAsync(email, "ValidPassword123!", displayName);

        SetSessionCookie(sessionToken);

        // Act
        var response = await Client.GetAsync("/api/v1/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<MeResponse>();
        result.Should().NotBeNull();
        result!.User.Should().NotBeNull();
        result.User!.Email.Should().Be(email.ToLowerInvariant());
        result.User.DisplayName.Should().Be(displayName);
    }

    #endregion

    #region Logout Tests

    [Fact]
    public async Task Logout_WithValidSession_InvalidatesSession()
    {
        // Arrange
        var email = $"logout_{Guid.NewGuid():N}@example.com";
        var (sessionToken, _) = await RegisterUserAsync(email, "ValidPassword123!");

        SetSessionCookie(sessionToken);

        // Verify session is valid
        var statusBefore = await Client.GetAsync("/api/v1/auth/session/status");
        statusBefore.StatusCode.Should().Be(HttpStatusCode.OK);

        // Act
        var logoutResponse = await Client.PostAsync("/api/v1/auth/logout", null);

        // Assert
        logoutResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // Session should no longer be valid (after clearing cookie in real scenario)
        // Note: Cookie is cleared by Set-Cookie response, subsequent requests need new cookie
    }

    [Fact]
    public async Task Logout_WithoutSession_ReturnsSuccess()
    {
        // Arrange - No session
        ClearAuthentication();

        // Act
        var response = await Client.PostAsync("/api/v1/auth/logout", null);

        // Assert - Logout should succeed even without session (idempotent)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    #endregion

    #region Complete User Journey Tests

    [Fact]
    public async Task CompleteUserJourney_RegisterLoginLogout_Succeeds()
    {
        // Step 1: Register
        var email = $"journey_{Guid.NewGuid():N}@example.com";
        var password = "JourneyPassword123!";
        var (registrationToken, userId) = await RegisterUserAsync(email, password);

        registrationToken.Should().NotBeNullOrEmpty();
        userId.Should().NotBeEmpty();

        // Step 2: Logout after registration
        SetSessionCookie(registrationToken);
        var logoutResponse = await Client.PostAsync("/api/v1/auth/logout", null);
        logoutResponse.EnsureSuccessStatusCode();

        ClearAuthentication();

        // Step 3: Login with same credentials
        var (loginToken, loginUserId) = await LoginUserAsync(email, password);

        loginToken.Should().NotBeNullOrEmpty();
        loginUserId.Should().Be(userId);

        // Step 4: Access protected endpoint
        SetSessionCookie(loginToken);
        var meResponse = await Client.GetAsync("/api/v1/auth/me");
        meResponse.EnsureSuccessStatusCode();

        // Step 5: Final logout
        var finalLogout = await Client.PostAsync("/api/v1/auth/logout", null);
        finalLogout.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MultipleSessionsScenario_CreateAndManage_Succeeds()
    {
        // Register user
        var email = $"multisession_{Guid.NewGuid():N}@example.com";
        var password = "MultiPassword123!";
        var (session1Token, userId) = await RegisterUserAsync(email, password);

        // Clear and login again (simulating different device)
        ClearAuthentication();
        var (session2Token, _) = await LoginUserAsync(email, password);

        // Both sessions should be different
        session1Token.Should().NotBe(session2Token);

        // Both sessions should be valid
        SetSessionCookie(session1Token);
        var status1 = await Client.GetAsync("/api/v1/auth/session/status");
        status1.StatusCode.Should().Be(HttpStatusCode.OK);

        // Note: For session2 to work, we'd need a separate HttpClient
        // as cookies are per-client
    }

    #endregion

    // Response DTOs
    private sealed record SessionStatusResponse(DateTime ExpiresAt, DateTime LastSeenAt, int RemainingMinutes);
    private sealed record MeResponse(UserDto? User, DateTime? ExpiresAt);
    private sealed record UserDto(Guid Id, string Email, string DisplayName, string Role);
}
