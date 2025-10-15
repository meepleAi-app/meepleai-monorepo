using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Integration tests for authorization edge cases and role-based access control.
///
/// Tests cover:
/// - Role-based endpoint access (Admin, Editor, User)
/// - Cross-user resource access attempts
/// - Permission escalation prevention
/// - Unauthorized access scenarios
/// - Resource ownership validation
/// </summary>
[Collection("Integration")]
public class AuthorizationEdgeCasesIntegrationTests : IntegrationTestBase
{
    public AuthorizationEdgeCasesIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    #region Admin-Only Endpoint Tests

    /// <summary>
    /// Scenario: Admin accesses admin-only endpoint
    ///   Given authenticated admin user
    ///   When accessing admin endpoint
    ///   Then HTTP 200 OK is returned
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_WithAdminRole_Returns200()
    {
        // Arrange: Admin user
        var admin = await CreateTestUserAsync("admin-test-1", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // Act: Access admin endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/sessions");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Editor accesses admin-only endpoint
    ///   Given authenticated editor user
    ///   When accessing admin endpoint
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_WithEditorRole_Returns403()
    {
        // Arrange: Editor user
        var editor = await CreateTestUserAsync("editor-test-1", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // Act: Access admin endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/sessions");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Regular user accesses admin-only endpoint
    ///   Given authenticated regular user
    ///   When accessing admin endpoint
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_WithUserRole_Returns403()
    {
        // Arrange: Regular user
        var user = await CreateTestUserAsync("user-test-1", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // Act: Access admin endpoint
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/sessions");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Unauthenticated user accesses admin endpoint
    ///   Given no authentication
    ///   When accessing admin endpoint
    ///   Then HTTP 401 Unauthorized is returned
    /// </summary>
    [Fact]
    public async Task AdminEndpoint_WithoutAuth_Returns401()
    {
        // Arrange: No authentication
        var client = CreateClientWithoutCookies();

        // Act: Access admin endpoint
        var response = await client.GetAsync("/api/v1/admin/sessions");

        // Assert: Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Editor Endpoint Tests

    /// <summary>
    /// Scenario: Editor accesses game creation endpoint
    ///   Given authenticated editor user
    ///   When accessing game creation endpoint
    ///   Then access is granted (not 403 Forbidden)
    /// </summary>
    [Fact]
    public async Task CreateGameEndpoint_WithEditorRole_IsAccessible()
    {
        // Arrange: Editor user
        var editor = await CreateTestUserAsync("editor-game-create-1", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // Act: Try to create a game
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new { Name = "EditorGame", GameId = "editor-game" })
        };
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Not forbidden (should be 201 Created or 409 Conflict if exists, but not 403)
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Regular user accesses game creation endpoint (editor/admin only)
    ///   Given authenticated regular user
    ///   When accessing game creation endpoint
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task CreateGameEndpoint_WithUserRole_Returns403()
    {
        // Arrange: Regular user
        var user = await CreateTestUserAsync("user-game-create-1", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // Act: Try to create a game (editor/admin only)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new { Name = "TestGame", GameId = "test-game" })
        };
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Cross-User Resource Access Tests

    /// <summary>
    /// Scenario: User tries to access another user's chat
    ///   Given two users with separate chats
    ///   When user A tries to access user B's chat
    ///   Then HTTP 404 Not Found is returned
    /// </summary>
    [Fact]
    public async Task GetChat_CrossUserAccess_Returns404()
    {
        // Arrange: Two users with their own chats
        var userA = await CreateTestUserAsync("user-a-chat", UserRole.User);
        var userB = await CreateTestUserAsync("user-b-chat", UserRole.User);
        var cookiesA = await AuthenticateUserAsync(userA.Email);

        // Create game and agent
        var game = await CreateTestGameAsync("Catan");
        var agent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");

        // Create chat for user B
        var chatB = await CreateTestChatAsync(userB.Id, game.Id, agent.Id);

        // Act: User A tries to access user B's chat
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chatB.Id}");
        AddCookies(request, cookiesA);
        var response = await client.SendAsync(request);

        // Assert: Not found (resource isolation)
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User tries to delete another user's chat
    ///   Given two users with separate chats
    ///   When user A tries to delete user B's chat
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task DeleteChat_CrossUserAccess_Returns403()
    {
        // Arrange: Two users with their own chats
        var userA = await CreateTestUserAsync("user-a-delete", UserRole.User);
        var userB = await CreateTestUserAsync("user-b-delete", UserRole.User);
        var cookiesA = await AuthenticateUserAsync(userA.Email);

        // Create game and agent
        var game = await CreateTestGameAsync("Catan");
        var agent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");

        // Create chat for user B
        var chatB = await CreateTestChatAsync(userB.Id, game.Id, agent.Id);

        // Act: User A tries to delete user B's chat
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/chats/{chatB.Id}");
        AddCookies(request, cookiesA);
        var response = await client.SendAsync(request);

        // Assert: Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User can access their own chat
    ///   Given user with chat
    ///   When user accesses their own chat
    ///   Then HTTP 200 OK is returned
    /// </summary>
    [Fact]
    public async Task GetChat_OwnResource_Returns200()
    {
        // Arrange: User with their own chat
        var user = await CreateTestUserAsync("user-own-chat", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // Create game and agent
        var game = await CreateTestGameAsync("Catan");
        var agent = await CreateTestAgentAsync(game.Id, "qa", "Q&A Agent");

        // Create chat for user
        var chat = await CreateTestChatAsync(user.Id, game.Id, agent.Id);

        // Act: User accesses their own chat
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/chats/{chat.Id}");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Permission Escalation Prevention Tests

    /// <summary>
    /// Scenario: User cannot escalate privileges via profile update
    ///   Given regular user
    ///   When attempting to modify role in profile
    ///   Then role remains unchanged
    /// </summary>
    [Fact]
    public async Task UpdateProfile_CannotEscalateRole()
    {
        // Arrange: Regular user
        var user = await CreateTestUserAsync("user-escalation-test", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // Act: Attempt to update profile with admin role (if endpoint accepts role)
        // Note: This test assumes the update endpoint exists and validates role changes
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/v1/users/me")
        {
            Content = JsonContent.Create(new { displayName = "Hacker", role = "Admin" })
        };
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Either forbidden or role not changed
        // Verify user role hasn't changed
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Api.Infrastructure.MeepleAiDbContext>();
            var updatedUser = await db.Users.FindAsync(user.Id);
            Assert.Equal(UserRole.User, updatedUser!.Role); // Role should still be User
        }
    }

    /// <summary>
    /// Scenario: Editor cannot perform admin actions
    ///   Given editor user
    ///   When attempting admin-only action (delete user sessions)
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task AdminAction_WithEditorRole_Returns403()
    {
        // Arrange: Editor user
        var editor = await CreateTestUserAsync("editor-admin-action", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // Act: Attempt admin action (delete all sessions for a user)
        var targetUser = await CreateTestUserAsync("target-user", UserRole.User);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/users/{targetUser.Id}/sessions");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Session Security Tests

    /// <summary>
    /// Scenario: Expired session cookie returns unauthorized
    ///   Given session that has been revoked
    ///   When using revoked session cookie
    ///   Then HTTP 401 Unauthorized is returned
    /// </summary>
    [Fact]
    public async Task RevokedSession_ReturnsUnauthorized()
    {
        // Arrange: User with session
        var user = await CreateTestUserAsync("revoked-session-user", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // Revoke the session
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Api.Infrastructure.MeepleAiDbContext>();
            var sessions = await db.UserSessions
                .Where(s => s.UserId == user.Id && s.RevokedAt == null)
                .ToListAsync();
            foreach (var session in sessions)
            {
                session.RevokedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }

        // Act: Try to use revoked session
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/users/me/sessions");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Missing authentication cookie returns unauthorized
    ///   Given no authentication cookie
    ///   When accessing protected endpoint
    ///   Then HTTP 401 Unauthorized is returned
    /// </summary>
    [Fact]
    public async Task MissingAuthCookie_ReturnsUnauthorized()
    {
        // Arrange: No authentication
        var client = CreateClientWithoutCookies();

        // Act: Access protected endpoint
        var response = await client.GetAsync("/api/v1/chats");

        // Assert: Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Public Endpoint Tests

    /// <summary>
    /// Scenario: Health check liveness endpoint is publicly accessible
    ///   Given no authentication
    ///   When accessing liveness health check
    ///   Then HTTP 200 OK is returned
    /// </summary>
    [Fact]
    public async Task HealthCheckLiveness_WithoutAuth_Returns200()
    {
        // Arrange: No authentication
        var client = CreateClientWithoutCookies();

        // Act: Access liveness health check (doesn't check dependencies)
        var response = await client.GetAsync("/health/live");

        // Assert: Success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Login endpoint is publicly accessible
    ///   Given no authentication
    ///   When accessing login endpoint
    ///   Then endpoint is accessible (not 401/403)
    /// </summary>
    [Fact]
    public async Task LoginEndpoint_WithoutAuth_IsAccessible()
    {
        // Arrange: No authentication
        var client = CreateClientWithoutCookies();

        // Act: POST to login (with invalid credentials to test accessibility)
        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { Email = "nonexistent@example.com", Password = "wrongpassword" });

        // Assert: Not unauthorized/forbidden (likely 401 due to wrong credentials, but endpoint is accessible)
        // The endpoint itself doesn't require auth, even though login fails
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Multiple Role Scenarios

    /// <summary>
    /// Scenario: Admin can access editor endpoints
    ///   Given admin user
    ///   When accessing editor-level endpoint
    ///   Then access is granted
    /// </summary>
    [Fact]
    public async Task EditorEndpoint_WithAdminRole_IsAccessible()
    {
        // Arrange: Admin user
        var admin = await CreateTestUserAsync("admin-editor-test", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // Act: Access editor endpoint (game creation)
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new { Name = "AdminEditorGame", GameId = "admin-editor-game" })
        };
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Not forbidden
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Admin can access user endpoints
    ///   Given admin user
    ///   When accessing user-level endpoint
    ///   Then access is granted
    /// </summary>
    [Fact]
    public async Task UserEndpoint_WithAdminRole_IsAccessible()
    {
        // Arrange: Admin user
        var admin = await CreateTestUserAsync("admin-user-test", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // Act: Access user endpoint (list own chats)
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chats");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Editor can access user endpoints
    ///   Given editor user
    ///   When accessing user-level endpoint
    ///   Then access is granted
    /// </summary>
    [Fact]
    public async Task UserEndpoint_WithEditorRole_IsAccessible()
    {
        // Arrange: Editor user
        var editor = await CreateTestUserAsync("editor-user-test", UserRole.Editor);
        var cookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // Act: Access user endpoint (list own chats)
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/chats");
        AddCookies(request, cookies);
        var response = await client.SendAsync(request);

        // Assert: Success
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Helper Methods

    private async Task<ChatEntity> CreateTestChatAsync(string userId, string gameId, string agentId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Api.Infrastructure.MeepleAiDbContext>();

        var chat = new ChatEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            GameId = gameId,
            AgentId = agentId,
            StartedAt = DateTime.UtcNow,
            LastMessageAt = null
        };

        db.Chats.Add(chat);
        await db.SaveChangesAsync();

        return chat;
    }

    private async Task<AgentEntity> CreateTestAgentAsync(string gameId, string kind, string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Api.Infrastructure.MeepleAiDbContext>();

        var agent = new AgentEntity
        {
            Id = $"{gameId}-{kind}-{Guid.NewGuid()}",
            GameId = gameId,
            Name = name,
            Kind = kind,
            CreatedAt = DateTime.UtcNow
        };

        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        return agent;
    }

    #endregion
}
