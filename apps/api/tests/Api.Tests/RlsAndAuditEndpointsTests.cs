using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for Row-Level Security (RLS) and Audit logging.
///
/// Feature: SEC-02 - Row Level Security and Role Separation
/// As a system administrator
/// I want to ensure that users can only access resources they own or are authorized to access
/// So that data isolation and security policies are enforced
///
/// Roles Tested:
/// - Admin: Full access to all resources
/// - Editor: Can create/update own resources + read others
/// - User: Can only access own resources
/// </summary>
public class RlsAndAuditEndpointsTests : IntegrationTestBase
{
    public RlsAndAuditEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    #region Admin Role Tests

    /// <summary>
    /// Scenario: Admin accesses any user's RuleSpec
    ///   Given admin user is authenticated
    ///   And another user created a RuleSpec
    ///   When admin requests the RuleSpec
    ///   Then access is granted with HTTP 200
    ///   And RuleSpec data is returned
    /// </summary>
    [Fact]
    public async Task GetRuleSpec_AdminCanAccessAnyUsersResource()
    {
        // Given: Admin and another user exist
        var admin = await CreateTestUserAsync("admin", UserRole.Admin);
        var owner = await CreateTestUserAsync("owner", UserRole.User);
        var game = await CreateTestGameAsync("Catan");
        var ruleSpec = await CreateTestRuleSpecAsync(game.Id, owner.Id, "1.0.0");

        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin requests the RuleSpec
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/games/{game.Id}/rulespec");
        AddCookies(request, adminCookies);

        var response = await client.SendAsync(request);

        // Then: Access is granted
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<RuleSpec>();
        Assert.NotNull(result);
        Assert.Equal(ruleSpec.Version, result!.version);
    }

    /// <summary>
    /// Scenario: Admin can delete any user's PDF
    ///   Given admin user is authenticated
    ///   And another user uploaded a PDF
    ///   When admin attempts to delete the PDF
    ///   Then deletion succeeds with HTTP 204
    ///   And PDF is removed from database
    /// </summary>
    [Fact]
    public async Task DeletePdf_AdminCanDeleteAnyUsersPdf()
    {
        // Given: Admin and PDF owner exist
        var admin = await CreateTestUserAsync("admin", UserRole.Admin);
        var owner = await CreateTestUserAsync("pdfowner", UserRole.User);
        var game = await CreateTestGameAsync("Wingspan");
        var pdf = await CreateTestPdfDocumentAsync(game.Id, owner.Id, "rules.pdf");

        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin deletes the PDF
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/pdf/{pdf.Id}");
        AddCookies(request, adminCookies);

        var response = await client.SendAsync(request);

        // Then: Deletion succeeds
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // And: PDF is removed
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var exists = await db.PdfDocuments.AnyAsync(p => p.Id == pdf.Id);
        Assert.False(exists);
    }

    /// <summary>
    /// Scenario: Admin can create games
    ///   Given admin user is authenticated
    ///   When admin creates a game
    ///   Then game is created successfully
    /// </summary>
    [Fact]
    public async Task PostGame_AdminCanCreateGames()
    {
        // Given: Admin is authenticated
        var admin = await CreateTestUserAsync("admin", UserRole.Admin);
        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin creates a game
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new CreateGameRequest("Azul", "azul"))
        };
        AddCookies(request, adminCookies);

        var response = await client.SendAsync(request);

        // Then: Game is created
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        TrackGameId(game!.Id);
    }

    /// <summary>
    /// Scenario: Admin can access RuleSpec history (Admin/Editor only endpoint)
    ///   Given admin user is authenticated
    ///   And a game with RuleSpec versions exists
    ///   When admin requests RuleSpec history
    ///   Then access is granted with HTTP 200
    /// </summary>
    [Fact]
    public async Task GetRuleSpecHistory_AdminCanAccessHistory()
    {
        // Given: Admin and game with RuleSpec
        var admin = await CreateTestUserAsync("admin", UserRole.Admin);
        var game = await CreateTestGameAsync("Scythe");
        await CreateTestRuleSpecAsync(game.Id, admin.Id, "1.0.0");

        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var client = CreateClientWithoutCookies();

        // When: Admin requests RuleSpec history
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/games/{game.Id}/rulespec/history");
        AddCookies(request, adminCookies);

        var response = await client.SendAsync(request);

        // Then: Access is granted
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region Editor Role Tests

    /// <summary>
    /// Scenario: Editor can create games
    ///   Given editor user is authenticated
    ///   When editor creates a game
    ///   Then game is created successfully
    /// </summary>
    [Fact]
    public async Task PostGame_EditorCanCreateGames()
    {
        // Given: Editor is authenticated
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var editorCookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // When: Editor creates a game
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new CreateGameRequest("Root", "root"))
        };
        AddCookies(request, editorCookies);

        var response = await client.SendAsync(request);

        // Then: Game is created
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var game = await response.Content.ReadFromJsonAsync<GameResponse>();
        Assert.NotNull(game);
        TrackGameId(game!.Id);
    }

    /// <summary>
    /// Scenario: Editor can update RuleSpec
    ///   Given editor user is authenticated
    ///   And a game exists
    ///   When editor updates a RuleSpec
    ///   Then RuleSpec is updated successfully
    /// </summary>
    [Fact]
    public async Task PutRuleSpec_EditorCanUpdateRuleSpec()
    {
        // Given: Editor and game exist
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var game = await CreateTestGameAsync("Pandemic");
        await CreateTestRuleSpecAsync(game.Id, editor.Id, "1.0.0");

        var editorCookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // When: Editor updates the RuleSpec
        var updatedRuleSpec = new RuleSpec(
            game.Id,
            "2.0.0",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("atom-1", "New rule text") }
        );

        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/v1/games/{game.Id}/rulespec")
        {
            Content = JsonContent.Create(updatedRuleSpec)
        };
        AddCookies(request, editorCookies);

        var response = await client.SendAsync(request);

        // Then: RuleSpec is updated
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Editor can read but not delete another user's PDF
    ///   Given editor user is authenticated
    ///   And another user uploaded a PDF
    ///   When editor attempts to delete the PDF
    ///   Then access is denied with HTTP 403
    /// </summary>
    [Fact]
    public async Task DeletePdf_EditorCannotDeleteOtherUsersPdf()
    {
        // Given: Editor and PDF owner exist
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var owner = await CreateTestUserAsync("pdfowner", UserRole.Editor);
        var game = await CreateTestGameAsync("Gloomhaven");
        var pdf = await CreateTestPdfDocumentAsync(game.Id, owner.Id, "rules.pdf");

        var editorCookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // When: Editor attempts to delete owner's PDF
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/pdf/{pdf.Id}");
        AddCookies(request, editorCookies);

        var response = await client.SendAsync(request);

        // Then: Access is denied
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // And: PDF still exists
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var exists = await db.PdfDocuments.AnyAsync(p => p.Id == pdf.Id);
        Assert.True(exists);
    }

    /// <summary>
    /// Scenario: Editor can delete own PDF
    ///   Given editor created a PDF
    ///   When editor attempts to delete their own PDF
    ///   Then deletion succeeds with HTTP 204
    /// </summary>
    [Fact]
    public async Task DeletePdf_EditorCanDeleteOwnPdf()
    {
        // Given: Editor created a PDF
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var game = await CreateTestGameAsync("Dominion");
        var pdf = await CreateTestPdfDocumentAsync(game.Id, editor.Id, "my-rules.pdf");

        var editorCookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // When: Editor deletes own PDF
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/pdf/{pdf.Id}");
        AddCookies(request, editorCookies);

        var response = await client.SendAsync(request);

        // Then: Deletion succeeds
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Editor can access RuleSpec history
    ///   Given editor user is authenticated
    ///   And a game with RuleSpec exists
    ///   When editor requests RuleSpec history
    ///   Then access is granted with HTTP 200
    /// </summary>
    [Fact]
    public async Task GetRuleSpecHistory_EditorCanAccessHistory()
    {
        // Given: Editor and game with RuleSpec
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var game = await CreateTestGameAsync("Agricola");
        await CreateTestRuleSpecAsync(game.Id, editor.Id, "1.0.0");

        var editorCookies = await AuthenticateUserAsync(editor.Email);
        var client = CreateClientWithoutCookies();

        // When: Editor requests RuleSpec history
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/games/{game.Id}/rulespec/history");
        AddCookies(request, editorCookies);

        var response = await client.SendAsync(request);

        // Then: Access is granted
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    #endregion

    #region User Role Tests

    /// <summary>
    /// Scenario: User cannot access another user's PDF
    ///   Given regular user is authenticated
    ///   And another user uploaded a PDF
    ///   When user attempts to delete the PDF
    ///   Then access is denied with HTTP 403
    /// </summary>
    [Fact]
    public async Task DeletePdf_UserCannotDeleteOtherUsersPdf()
    {
        // Given: Two users and a PDF owned by user1
        var user1 = await CreateTestUserAsync("user1", UserRole.User);
        var user2 = await CreateTestUserAsync("user2", UserRole.User);
        var game = await CreateTestGameAsync("Ticket to Ride");
        var pdf = await CreateTestPdfDocumentAsync(game.Id, user1.Id, "rules.pdf");

        var user2Cookies = await AuthenticateUserAsync(user2.Email);
        var client = CreateClientWithoutCookies();

        // When: User2 attempts to delete user1's PDF
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/pdf/{pdf.Id}");
        AddCookies(request, user2Cookies);

        var response = await client.SendAsync(request);

        // Then: Access is denied
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // And: PDF still exists
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var exists = await db.PdfDocuments.AnyAsync(p => p.Id == pdf.Id);
        Assert.True(exists);
    }

    /// <summary>
    /// Scenario: User can delete own PDF
    ///   Given regular user uploaded a PDF
    ///   When user attempts to delete their own PDF
    ///   Then deletion succeeds with HTTP 204
    /// </summary>
    [Fact]
    public async Task DeletePdf_UserCanDeleteOwnPdf()
    {
        // Given: User uploaded a PDF
        var user = await CreateTestUserAsync("pdfuser", UserRole.User);
        var game = await CreateTestGameAsync("Codenames");
        var pdf = await CreateTestPdfDocumentAsync(game.Id, user.Id, "my-rules.pdf");

        var userCookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User deletes own PDF
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/pdf/{pdf.Id}");
        AddCookies(request, userCookies);

        var response = await client.SendAsync(request);

        // Then: Deletion succeeds
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User cannot create games (Editor+ only)
    ///   Given regular user is authenticated
    ///   When user attempts to create a game
    ///   Then access is denied with HTTP 403
    /// </summary>
    [Fact]
    public async Task PostGame_UserCannotCreateGames()
    {
        // Given: Regular user is authenticated
        var user = await CreateTestUserAsync("regularuser", UserRole.User);
        var userCookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User attempts to create a game
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/games")
        {
            Content = JsonContent.Create(new CreateGameRequest("Forbidden Game", "forbidden-game"))
        };
        AddCookies(request, userCookies);

        var response = await client.SendAsync(request);

        // Then: Access is denied
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User cannot access admin endpoints
    ///   Given regular user is authenticated
    ///   When user attempts to access admin stats endpoint
    ///   Then access is denied with HTTP 403
    /// </summary>
    [Fact]
    public async Task GetAdminStats_UserCannotAccessAdminEndpoints()
    {
        // Given: Regular user is authenticated
        var user = await CreateTestUserAsync("normaluser", UserRole.User);
        var userCookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User attempts to access admin stats
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/stats");
        AddCookies(request, userCookies);

        var response = await client.SendAsync(request);

        // Then: Access is denied
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: User cannot access RuleSpec history (Editor+ only)
    ///   Given regular user is authenticated
    ///   And a game with RuleSpec exists
    ///   When user requests RuleSpec history
    ///   Then access is denied with HTTP 403
    /// </summary>
    [Fact]
    public async Task GetRuleSpecHistory_UserCannotAccessHistory()
    {
        // Given: User and game with RuleSpec
        var user = await CreateTestUserAsync("user", UserRole.User);
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var game = await CreateTestGameAsync("Splendor");
        await CreateTestRuleSpecAsync(game.Id, editor.Id, "1.0.0");

        var userCookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // When: User requests RuleSpec history
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/games/{game.Id}/rulespec/history");
        AddCookies(request, userCookies);

        var response = await client.SendAsync(request);

        // Then: Access is denied
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region Audit Logging Tests

    /// <summary>
    /// Scenario: Access denied events are logged to audit
    ///   Given regular user is authenticated
    ///   When user attempts unauthorized action (delete other's PDF)
    ///   Then access is denied with HTTP 403
    ///   And audit log contains ACCESS_DENIED entry
    ///   And audit log includes user ID and resource details
    /// </summary>
    [Fact]
    public async Task AuditLog_AccessDeniedIsLogged()
    {
        // Given: Two users and a PDF
        var owner = await CreateTestUserAsync("pdfowner", UserRole.User);
        var attacker = await CreateTestUserAsync("attacker", UserRole.User);
        var game = await CreateTestGameAsync("7 Wonders");
        var pdf = await CreateTestPdfDocumentAsync(game.Id, owner.Id, "secure-rules.pdf");

        var attackerCookies = await AuthenticateUserAsync(attacker.Email);
        var client = CreateClientWithoutCookies();

        // When: Attacker attempts to delete owner's PDF
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/pdf/{pdf.Id}");
        AddCookies(request, attackerCookies);

        var response = await client.SendAsync(request);

        // Then: Access is denied
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // And: Audit log contains ACCESS_DENIED entry
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var auditLog = await db.AuditLogs
            .Where(a => a.UserId == attacker.Id)
            .Where(a => a.Action == "ACCESS_DENIED")
            .Where(a => a.Resource == "PdfDocument")
            .Where(a => a.ResourceId == pdf.Id)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        Assert.NotNull(auditLog);
        Assert.Equal(attacker.Id, auditLog!.UserId);
        Assert.Equal("Denied", auditLog.Result);
        Assert.Contains("scope", auditLog.Details ?? "", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Cross-Role Ownership Tests

    /// <summary>
    /// Scenario: Multiple users can read same game data
    ///   Given multiple users with different roles
    ///   When each user requests game list
    ///   Then all users receive the same game data
    ///   And HTTP 200 is returned for all
    /// </summary>
    [Fact]
    public async Task GetGames_AllAuthenticatedUsersCanReadGames()
    {
        // Given: Multiple users with different roles
        var admin = await CreateTestUserAsync("admin", UserRole.Admin);
        var editor = await CreateTestUserAsync("editor", UserRole.Editor);
        var user = await CreateTestUserAsync("user", UserRole.User);
        var game = await CreateTestGameAsync("Brass Birmingham");

        var adminCookies = await AuthenticateUserAsync(admin.Email);
        var editorCookies = await AuthenticateUserAsync(editor.Email);
        var userCookies = await AuthenticateUserAsync(user.Email);

        var client = CreateClientWithoutCookies();

        // When: Each user requests game list
        var requests = new[]
        {
            (cookies: adminCookies, role: "Admin"),
            (cookies: editorCookies, role: "Editor"),
            (cookies: userCookies, role: "User")
        };

        foreach (var (cookies, role) in requests)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/games");
            AddCookies(request, cookies);

            var response = await client.SendAsync(request);

            // Then: All users can access games
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var games = await response.Content.ReadFromJsonAsync<List<GameResponse>>();
            Assert.NotNull(games);
            Assert.Contains(games!, g => g.Id == game.Id);
        }
    }

    #endregion

    #region Unauthenticated Access Tests

    /// <summary>
    /// Scenario: Unauthenticated user cannot access protected resources
    ///   Given no user is authenticated
    ///   When request is made to protected endpoint
    ///   Then access is denied with HTTP 401
    /// </summary>
    [Fact]
    public async Task ProtectedEndpoint_UnauthenticatedUserReceives401()
    {
        // Given: No authentication
        var client = CreateClientWithoutCookies();

        // When: Request to protected endpoint
        var response = await client.GetAsync("/api/v1/games");

        // Then: Access is denied
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion
}
