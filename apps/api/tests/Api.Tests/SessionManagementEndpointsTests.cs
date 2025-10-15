using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for Session Management endpoints (AUTH-03).
///
/// Feature: AUTH-03 - Session Management Service with Auto-Revocation
/// As an administrator
/// I want to manage user sessions via API endpoints
/// So that I can monitor, revoke, and control user access to the system
/// </summary>
public class SessionManagementEndpointsTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public SessionManagementEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    /// <summary>
    /// Scenario: Admin lists all sessions
    ///   Given an authenticated admin user
    ///   And multiple users with sessions exist
    ///   When the admin requests GET /admin/sessions
    ///   Then HTTP 200 is returned
    ///   And all sessions are listed
    /// </summary>
    [Fact]
    public async Task GET_AdminSessions_AsAdmin_Returns200WithAllSessions()
    {
        // Given: An authenticated admin
        var admin = await CreateTestUserAsync("admin-sessions-1", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: Multiple users with sessions
        var user1 = await CreateTestUserAsync("user1-sessions", UserRole.User);
        var user2 = await CreateTestUserAsync("user2-sessions", UserRole.User);
        var cookies1 = await AuthenticateUserAsync(user1.Email);
        var cookies2 = await AuthenticateUserAsync(user2.Email);

        // When: Admin requests all sessions
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/sessions");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with sessions list
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.ValueKind == JsonValueKind.Array);

        var sessions = result.EnumerateArray().ToList();
        Assert.True(sessions.Count >= 3); // At least admin + 2 users

        // Verify session structure
        var firstSession = sessions[0];
        Assert.True(firstSession.TryGetProperty("id", out _));
        Assert.True(firstSession.TryGetProperty("userId", out _));
        Assert.True(firstSession.TryGetProperty("userEmail", out _));
        Assert.True(firstSession.TryGetProperty("createdAt", out _));
    }

    /// <summary>
    /// Scenario: Admin lists sessions filtered by userId
    ///   Given an authenticated admin user
    ///   And a specific user with multiple sessions
    ///   When the admin requests GET /admin/sessions?userId={userId}
    ///   Then HTTP 200 is returned
    ///   And only sessions for that user are listed
    /// </summary>
    [Fact]
    public async Task GET_AdminSessions_WithUserIdFilter_Returns200WithFilteredSessions()
    {
        // Given: An authenticated admin
        var admin = await CreateTestUserAsync("admin-sessions-2", UserRole.Admin);
        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A user with sessions
        var user = await CreateTestUserAsync("user-filter-test", UserRole.User);
        var cookies1 = await AuthenticateUserAsync(user.Email);
        var cookies2 = await AuthenticateUserAsync(user.Email); // Create another session

        // When: Admin requests sessions filtered by userId
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/sessions?userId={user.Id}");
        AddCookies(request, adminCookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with filtered sessions
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var sessions = result.EnumerateArray().ToList();

        Assert.True(sessions.Count >= 2); // At least 2 sessions for this user
        Assert.All(sessions, session =>
        {
            session.TryGetProperty("userId", out var userId);
            Assert.Equal(user.Id, userId.GetString());
        });
    }

    /// <summary>
    /// Scenario: Admin lists sessions with limit
    ///   Given an authenticated admin user
    ///   And many sessions exist
    ///   When the admin requests GET /admin/sessions?limit=5
    ///   Then HTTP 200 is returned
    ///   And only 5 sessions are returned
    /// </summary>
    [Fact]
    public async Task GET_AdminSessions_WithLimit_Returns200WithLimitedResults()
    {
        // Given: An authenticated admin
        var admin = await CreateTestUserAsync("admin-sessions-3", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin requests sessions with limit
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/sessions?limit=5");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with limited results
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var sessions = result.EnumerateArray().ToList();

        Assert.True(sessions.Count <= 5);
    }

    /// <summary>
    /// Scenario: Non-admin tries to list sessions
    ///   Given an authenticated non-admin user
    ///   When the user requests GET /admin/sessions
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task GET_AdminSessions_AsNonAdmin_Returns403()
    {
        // Given: An authenticated regular user
        var user = await CreateTestUserAsync("non-admin-sessions", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User tries to access admin endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/sessions");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Unauthenticated user tries to list sessions
    ///   Given no authenticated user
    ///   When the user requests GET /admin/sessions
    ///   Then HTTP 401 Unauthorized is returned
    /// </summary>
    [Fact]
    public async Task GET_AdminSessions_AsUnauthenticated_Returns401()
    {
        // Given: No authentication
        var client = CreateClientWithoutCookies();

        // When: Unauthenticated request
        var response = await client.GetAsync("/api/v1/admin/sessions");

        // Then: HTTP 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Admin revokes a specific session
    ///   Given an authenticated admin user
    ///   And an active user session exists
    ///   When the admin requests DELETE /admin/sessions/{sessionId}
    ///   Then HTTP 200 is returned
    ///   And the session is revoked
    ///   And the user can no longer use that session
    /// </summary>
    [Fact]
    public async Task DELETE_AdminSessionId_AsAdmin_Returns200AndRevokesSession()
    {
        // Given: An authenticated admin
        var admin = await CreateTestUserAsync("admin-revoke-1", UserRole.Admin);
        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: An active user session
        var user = await CreateTestUserAsync("user-revoke-test", UserRole.User);
        var userCookies = await AuthenticateUserAsync(user.Email);

        // Get the session ID from the database
        string? sessionId;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            sessionId = await db.UserSessions
                .Where(s => s.UserId == user.Id && s.RevokedAt == null)
                .Select(s => s.Id)
                .FirstOrDefaultAsync();
        }

        Assert.NotNull(sessionId);

        // When: Admin revokes the session
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/sessions/{sessionId}");
        AddCookies(request, adminCookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Session is revoked in database
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var session = await db.UserSessions.FindAsync(sessionId);
            Assert.NotNull(session!.RevokedAt);
        }

        // And: User can no longer use that session
        var userRequest = new HttpRequestMessage(HttpMethod.Get, "/users/me/sessions");
        AddCookies(userRequest, userCookies);
        var userResponse = await client.SendAsync(userRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, userResponse.StatusCode);
    }

    /// <summary>
    /// Scenario: Admin tries to revoke non-existent session
    ///   Given an authenticated admin user
    ///   When the admin requests DELETE /admin/sessions/{nonExistentId}
    ///   Then HTTP 404 Not Found is returned
    /// </summary>
    [Fact]
    public async Task DELETE_AdminSessionId_WithNonExistentSession_Returns404()
    {
        // Given: An authenticated admin
        var admin = await CreateTestUserAsync("admin-revoke-2", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin tries to revoke non-existent session
        var nonExistentId = "non-existent-session-id";
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/sessions/{nonExistentId}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 404 Not Found
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Non-admin tries to revoke a session
    ///   Given an authenticated non-admin user
    ///   When the user requests DELETE /admin/sessions/{sessionId}
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task DELETE_AdminSessionId_AsNonAdmin_Returns403()
    {
        // Given: An authenticated regular user
        var user = await CreateTestUserAsync("non-admin-revoke", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User tries to revoke a session
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/sessions/some-session-id");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Admin revokes all sessions for a user
    ///   Given an authenticated admin user
    ///   And a user with multiple active sessions
    ///   When the admin requests DELETE /admin/users/{userId}/sessions
    ///   Then HTTP 200 is returned
    ///   And all user sessions are revoked
    ///   And the user cannot use any previous sessions
    /// </summary>
    [Fact]
    public async Task DELETE_AdminUsersUserIdSessions_AsAdmin_Returns200AndRevokesAllSessions()
    {
        // Given: An authenticated admin
        var admin = await CreateTestUserAsync("admin-revoke-all-1", UserRole.Admin);
        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A user with multiple sessions
        var user = await CreateTestUserAsync("user-revoke-all-test", UserRole.User);
        var cookies1 = await AuthenticateUserAsync(user.Email);
        var cookies2 = await AuthenticateUserAsync(user.Email);

        // Verify user has 2 active sessions
        int sessionCountBefore;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            sessionCountBefore = await db.UserSessions
                .Where(s => s.UserId == user.Id && s.RevokedAt == null)
                .CountAsync();
        }

        Assert.True(sessionCountBefore >= 2);

        // When: Admin revokes all user sessions
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/users/{user.Id}/sessions");
        AddCookies(request, adminCookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("revokedCount", out var revokedCount));
        Assert.True(revokedCount.GetInt32() >= 2);

        // And: All sessions are revoked in database
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var activeSessions = await db.UserSessions
                .Where(s => s.UserId == user.Id && s.RevokedAt == null)
                .CountAsync();
            Assert.Equal(0, activeSessions);
        }

        // And: User cannot use previous sessions
        var userRequest1 = new HttpRequestMessage(HttpMethod.Get, "/users/me/sessions");
        AddCookies(userRequest1, cookies1);
        var userResponse1 = await client.SendAsync(userRequest1);
        Assert.Equal(HttpStatusCode.Unauthorized, userResponse1.StatusCode);
    }

    /// <summary>
    /// Scenario: Admin revokes all sessions for user with no active sessions
    ///   Given an authenticated admin user
    ///   And a user with no active sessions
    ///   When the admin requests DELETE /admin/users/{userId}/sessions
    ///   Then HTTP 200 is returned
    ///   And revokedCount is 0
    /// </summary>
    [Fact]
    public async Task DELETE_AdminUsersUserIdSessions_WithNoActiveSessions_Returns200WithZeroCount()
    {
        // Given: An authenticated admin
        var admin = await CreateTestUserAsync("admin-revoke-all-2", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // And: A user with no active sessions (just created, no login)
        var user = await CreateTestUserAsync("user-no-sessions", UserRole.User);

        // When: Admin tries to revoke all sessions
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/users/{user.Id}/sessions");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with zero count
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("revokedCount", out var revokedCount));
        Assert.Equal(0, revokedCount.GetInt32());
    }

    /// <summary>
    /// Scenario: Non-admin tries to revoke all user sessions
    ///   Given an authenticated non-admin user
    ///   When the user requests DELETE /admin/users/{userId}/sessions
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task DELETE_AdminUsersUserIdSessions_AsNonAdmin_Returns403()
    {
        // Given: An authenticated regular user
        var user = await CreateTestUserAsync("non-admin-revoke-all", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User tries to revoke all sessions for another user
        var anotherUser = await CreateTestUserAsync("another-user", UserRole.User);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/users/{anotherUser.Id}/sessions");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User views their own active sessions
    ///   Given an authenticated user with multiple sessions
    ///   When the user requests GET /users/me/sessions
    ///   Then HTTP 200 is returned
    ///   And only their active sessions are listed
    /// </summary>
    [Fact]
    public async Task GET_UsersMeSessions_AsAuthenticatedUser_Returns200WithOwnSessions()
    {
        // Given: An authenticated user with multiple sessions
        var user = await CreateTestUserAsync("user-own-sessions", UserRole.User);
        var cookies1 = await AuthenticateUserAsync(user.Email);
        var cookies2 = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User requests their own sessions
        var request = new HttpRequestMessage(HttpMethod.Get, "/users/me/sessions");
        AddCookies(request, cookies1);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with sessions list
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.ValueKind == JsonValueKind.Array);

        var sessions = result.EnumerateArray().ToList();
        Assert.True(sessions.Count >= 2); // At least 2 sessions

        // Verify all sessions belong to the user
        Assert.All(sessions, session =>
        {
            session.TryGetProperty("userId", out var userId);
            Assert.Equal(user.Id, userId.GetString());
        });

        // Verify session structure
        var firstSession = sessions[0];
        Assert.True(firstSession.TryGetProperty("id", out _));
        Assert.True(firstSession.TryGetProperty("createdAt", out _));
        Assert.True(firstSession.TryGetProperty("lastSeenAt", out _));
        Assert.False(firstSession.TryGetProperty("revokedAt", out var revokedAt) && revokedAt.ValueKind != JsonValueKind.Null);
    }

    /// <summary>
    /// Scenario: Unauthenticated user tries to view sessions
    ///   Given no authenticated user
    ///   When the user requests GET /users/me/sessions
    ///   Then HTTP 401 Unauthorized is returned
    /// </summary>
    [Fact]
    public async Task GET_UsersMeSessions_AsUnauthenticated_Returns401()
    {
        // Given: No authentication
        var client = CreateClientWithoutCookies();

        // When: Unauthenticated request
        var response = await client.GetAsync("/users/me/sessions");

        // Then: HTTP 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
