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
/// BDD-style integration tests for RuleSpec Update endpoint (PUT /api/v1/games/{gameId}/rulespec).
///
/// Feature: RuleSpec Update Functionality (EDIT-01)
/// As an Admin or Editor
/// I want to update RuleSpecs with validation and authorization
/// So that I can manage game rules with version control and audit trail
/// </summary>
public class RuleSpecUpdateEndpointTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RuleSpecUpdateEndpointTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    #region Authorization Tests

    /// <summary>
    /// Scenario: Unauthenticated user attempts to update RuleSpec
    ///   Given an unauthenticated request
    ///   When attempting to update a RuleSpec
    ///   Then the request is rejected with 401 Unauthorized
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        // Arrange: Create game and rule spec
        var gameId = $"unauth-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        var ruleSpec = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom>
            {
                new RuleAtom("r1", "Test rule", "Setup", "1", "1")
            });

        // Act: Attempt update without authentication
        using var client = Factory.CreateHttpsClient();
        var response = await client.PutAsJsonAsync($"/api/v1/games/{gameId}/rulespec", ruleSpec);

        // Assert: Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Non-privileged user attempts to update RuleSpec
    ///   Given a user with User role (not Admin/Editor)
    ///   When attempting to update a RuleSpec
    ///   Then the request is rejected with 403 Forbidden
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_ReturnsForbidden_ForNonPrivilegedUser()
    {
        // Arrange: Create game and authenticate as User
        var gameId = $"forbidden-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var viewer = await CreateAuthenticatedClientAsync("User");

        var ruleSpec = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom>
            {
                new RuleAtom("r1", "Forbidden rule", "Setup", "1", "1")
            });

        // Act: Attempt update as User
        var response = await SendWithCookiesAsync(
            viewer.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            viewer.Cookies,
            ruleSpec);

        // Assert: Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Editor successfully updates RuleSpec
    ///   Given a user with Editor role
    ///   When updating a RuleSpec
    ///   Then the update succeeds and returns the new version
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_Succeeds_ForEditor()
    {
        // Arrange
        var gameId = $"editor-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var ruleSpec = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom>
            {
                new RuleAtom("r1", "Editor's rule", "Setup", "1", "1")
            });

        // Act
        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            editor.Cookies,
            ruleSpec);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await DeserializeAsync<RuleSpec>(response);
        Assert.NotNull(result);
        Assert.Equal(gameId, result!.gameId);
        Assert.Equal("v1", result.version);
        Assert.Single(result.rules);
        Assert.Equal("Editor's rule", result.rules[0].text);
    }

    /// <summary>
    /// Scenario: Admin successfully updates RuleSpec
    ///   Given a user with Admin role
    ///   When updating a RuleSpec
    ///   Then the update succeeds and returns the new version
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_Succeeds_ForAdmin()
    {
        // Arrange
        var gameId = $"admin-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var admin = await CreateAuthenticatedClientAsync("Admin");

        var ruleSpec = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom>
            {
                new RuleAtom("r1", "Admin's rule", "Setup", "1", "1")
            });

        // Act
        var response = await SendWithCookiesAsync(
            admin.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            admin.Cookies,
            ruleSpec);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await DeserializeAsync<RuleSpec>(response);
        Assert.NotNull(result);
        Assert.Equal(gameId, result!.gameId);
        Assert.Equal("v1", result.version);
    }

    #endregion

    #region Validation Tests

    /// <summary>
    /// Scenario: Update with missing gameId
    ///   Given a request with non-matching gameId
    ///   When attempting to update
    ///   Then validation fails with 400 Bad Request
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_ReturnsBadRequest_WhenGameIdMismatch()
    {
        // Arrange
        var gameId = $"mismatch-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        // RuleSpec with different gameId
        var ruleSpec = new RuleSpec(
            "different-game",
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom>
            {
                new RuleAtom("r1", "Rule", "Setup", "1", "1")
            });

        // Act
        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            editor.Cookies,
            ruleSpec);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Update for non-existent game
    ///   Given a gameId that doesn't exist
    ///   When attempting to update RuleSpec
    ///   Then returns 400 Bad Request with error message
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_ReturnsBadRequest_WhenGameDoesNotExist()
    {
        // Arrange
        var gameId = $"nonexistent-{Guid.NewGuid():N}";

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var ruleSpec = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom>
            {
                new RuleAtom("r1", "Rule", "Setup", "1", "1")
            });

        // Act
        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            editor.Cookies,
            ruleSpec);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Update with empty rules array
    ///   Given a RuleSpec with no rules
    ///   When attempting to update
    ///   Then the update succeeds (empty rule sets are allowed)
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_Succeeds_WithEmptyRules()
    {
        // Arrange
        var gameId = $"empty-rules-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var ruleSpec = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom>());

        // Act
        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            editor.Cookies,
            ruleSpec);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await DeserializeAsync<RuleSpec>(response);
        Assert.NotNull(result);
        Assert.Empty(result!.rules);
    }

    #endregion

    #region Version Management Tests

    /// <summary>
    /// Scenario: Auto-generate version when not provided
    ///   Given a RuleSpec without explicit version
    ///   When updating
    ///   Then system generates sequential version number
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_GeneratesSequentialVersion_WhenVersionNotProvided()
    {
        // Arrange
        var gameId = $"autover-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        // First version (v1 explicitly)
        var v1 = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r1", "First", "Setup", "1", "1") });

        await SendWithCookiesAsync(editor.Client, HttpMethod.Put, $"/api/v1/games/{gameId}/rulespec", editor.Cookies, v1);

        // Second version (empty version string)
        var v2 = new RuleSpec(
            gameId,
            "",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r2", "Second", "Setup", "1", "2") });

        // Act
        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            editor.Cookies,
            v2);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await DeserializeAsync<RuleSpec>(response);
        Assert.NotNull(result);
        Assert.Equal("v2", result!.version); // Should auto-increment to v2
    }

    /// <summary>
    /// Scenario: Prevent duplicate versions
    ///   Given a version that already exists
    ///   When attempting to create same version again
    ///   Then returns 400 Bad Request with error message
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_ReturnsBadRequest_WhenVersionAlreadyExists()
    {
        // Arrange
        var gameId = $"duplicate-ver-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var v1First = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r1", "First v1", "Setup", "1", "1") });

        // Create first v1
        await SendWithCookiesAsync(editor.Client, HttpMethod.Put, $"/api/v1/games/{gameId}/rulespec", editor.Cookies, v1First);

        // Attempt to create another v1
        var v1Duplicate = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r2", "Duplicate v1", "Setup", "1", "2") });

        // Act
        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            editor.Cookies,
            v1Duplicate);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("error", json);
    }

    /// <summary>
    /// Scenario: Create multiple versions with history
    ///   Given multiple sequential updates
    ///   When creating versions v1, v2, v3
    ///   Then all versions are stored and accessible
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_CreatesVersionHistory_WithMultipleUpdates()
    {
        // Arrange
        var gameId = $"history-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        // Act: Create 3 versions
        var v1 = new RuleSpec(gameId, "v1", DateTime.UtcNow, new List<RuleAtom> { new RuleAtom("r1", "V1 rule", "Setup", "1", "1") });
        var v2 = new RuleSpec(gameId, "v2", DateTime.UtcNow, new List<RuleAtom> { new RuleAtom("r2", "V2 rule", "Setup", "1", "2") });
        var v3 = new RuleSpec(gameId, "v3", DateTime.UtcNow, new List<RuleAtom> { new RuleAtom("r3", "V3 rule", "Setup", "1", "3") });

        await SendWithCookiesAsync(editor.Client, HttpMethod.Put, $"/api/v1/games/{gameId}/rulespec", editor.Cookies, v1);
        await SendWithCookiesAsync(editor.Client, HttpMethod.Put, $"/api/v1/games/{gameId}/rulespec", editor.Cookies, v2);
        await SendWithCookiesAsync(editor.Client, HttpMethod.Put, $"/api/v1/games/{gameId}/rulespec", editor.Cookies, v3);

        // Assert: Verify history contains all versions
        var historyResponse = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Get,
            $"/api/v1/games/{gameId}/rulespec/history",
            editor.Cookies);

        Assert.Equal(HttpStatusCode.OK, historyResponse.StatusCode);

        var history = await DeserializeAsync<RuleSpecHistory>(historyResponse);
        Assert.NotNull(history);
        Assert.Equal(3, history!.TotalVersions);
        Assert.Contains(history.Versions, v => v.Version == "v1");
        Assert.Contains(history.Versions, v => v.Version == "v2");
        Assert.Contains(history.Versions, v => v.Version == "v3");
    }

    #endregion

    #region Complex Rule Tests

    /// <summary>
    /// Scenario: Update with many rules and optional fields
    ///   Given a RuleSpec with multiple rules and varying optional fields
    ///   When updating
    ///   Then all rules and fields are preserved correctly
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_PreservesAllFields_WithComplexRules()
    {
        // Arrange
        var gameId = $"complex-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var ruleSpec = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom>
            {
                new RuleAtom("r1", "Rule with all fields", "Setup", "1", "5"),
                new RuleAtom("r2", "Rule with no section", null, "2", "10"),
                new RuleAtom("r3", "Rule with no page/line", "Gameplay", null, null),
                new RuleAtom("r4", "Minimal rule", null, null, null)
            });

        // Act
        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            editor.Cookies,
            ruleSpec);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await DeserializeAsync<RuleSpec>(response);
        Assert.NotNull(result);
        Assert.Equal(4, result!.rules.Count);

        // Verify each rule
        var r1 = result.rules.FirstOrDefault(r => r.id == "r1");
        Assert.NotNull(r1);
        Assert.Equal("Rule with all fields", r1!.text);
        Assert.Equal("Setup", r1.section);
        Assert.Equal("1", r1.page);
        Assert.Equal("5", r1.line);

        var r2 = result.rules.FirstOrDefault(r => r.id == "r2");
        Assert.NotNull(r2);
        Assert.Null(r2!.section);
        Assert.Equal("2", r2.page);

        var r3 = result.rules.FirstOrDefault(r => r.id == "r3");
        Assert.NotNull(r3);
        Assert.Equal("Gameplay", r3!.section);
        Assert.Null(r3.page);
        Assert.Null(r3.line);

        var r4 = result.rules.FirstOrDefault(r => r.id == "r4");
        Assert.NotNull(r4);
        Assert.Null(r4!.section);
        Assert.Null(r4.page);
        Assert.Null(r4.line);
    }

    /// <summary>
    /// Scenario: Update maintains correct sort order
    ///   Given rules in specific order
    ///   When saved and retrieved
    ///   Then order is preserved
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_PreservesSortOrder_ForRules()
    {
        // Arrange
        var gameId = $"sort-order-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var ruleSpec = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom>
            {
                new RuleAtom("z-last", "Should be last", "Setup", "5", "5"),
                new RuleAtom("a-first", "Should be first", "Setup", "1", "1"),
                new RuleAtom("m-middle", "Should be middle", "Setup", "3", "3")
            });

        // Act
        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Put,
            $"/api/v1/games/{gameId}/rulespec",
            editor.Cookies,
            ruleSpec);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await DeserializeAsync<RuleSpec>(response);
        Assert.NotNull(result);

        // Rules should be in the order they were provided
        Assert.Equal("z-last", result!.rules[0].id);
        Assert.Equal("a-first", result.rules[1].id);
        Assert.Equal("m-middle", result.rules[2].id);
    }

    #endregion

    #region Audit and Metadata Tests

    /// <summary>
    /// Scenario: Update tracks author information
    ///   Given an authenticated editor
    ///   When creating a RuleSpec version
    ///   Then author is tracked in version history
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_TracksAuthor_InVersionHistory()
    {
        // Arrange
        var gameId = $"author-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var ruleSpec = new RuleSpec(
            gameId,
            "v1",
            DateTime.UtcNow,
            new List<RuleAtom> { new RuleAtom("r1", "Test", "Setup", "1", "1") });

        // Act
        await SendWithCookiesAsync(editor.Client, HttpMethod.Put, $"/api/v1/games/{gameId}/rulespec", editor.Cookies, ruleSpec);

        // Assert: Check history shows author
        var historyResponse = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Get,
            $"/api/v1/games/{gameId}/rulespec/history",
            editor.Cookies);

        var history = await DeserializeAsync<RuleSpecHistory>(historyResponse);
        Assert.NotNull(history);
        Assert.Single(history!.Versions);

        var version = history.Versions[0];
        Assert.Equal("v1", version.Version);
        Assert.NotNull(version.CreatedBy);
        Assert.Contains("Editor User", version.CreatedBy!); // User display name or email
    }

    /// <summary>
    /// Scenario: Update invalidates cache
    ///   Given a RuleSpec update
    ///   When the update completes
    ///   Then cache for that game is invalidated
    ///   (This is tested indirectly by verifying subsequent reads get new data)
    /// </summary>
    [Fact]
    public async Task UpdateRuleSpec_InvalidatesCache_AfterUpdate()
    {
        // Arrange
        var gameId = $"cache-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var v1 = new RuleSpec(gameId, "v1", DateTime.UtcNow, new List<RuleAtom> { new RuleAtom("r1", "V1", "Setup", "1", "1") });
        await SendWithCookiesAsync(editor.Client, HttpMethod.Put, $"/api/v1/games/{gameId}/rulespec", editor.Cookies, v1);

        // Act: Update to v2
        var v2 = new RuleSpec(gameId, "v2", DateTime.UtcNow, new List<RuleAtom> { new RuleAtom("r2", "V2", "Setup", "1", "2") });
        await SendWithCookiesAsync(editor.Client, HttpMethod.Put, $"/api/v1/games/{gameId}/rulespec", editor.Cookies, v2);

        // Assert: Get latest version should return v2
        var getResponse = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Get,
            $"/api/v1/games/{gameId}/rulespec",
            editor.Cookies);

        var latest = await DeserializeAsync<RuleSpec>(getResponse);
        Assert.NotNull(latest);
        Assert.Equal("v2", latest!.version);
        Assert.Single(latest.rules);
        Assert.Equal("V2", latest.rules[0].text);
    }

    #endregion

    #region Helper Methods

    private async Task CreateGameAsync(string gameId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        if (!await db.Games.AnyAsync(g => g.Id == gameId))
        {
            db.Games.Add(new GameEntity
            {
                Id = gameId,
                Name = $"Test Game {gameId}",
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            TrackGameId(gameId);
        }
    }

    private async Task<AuthenticatedClient> CreateAuthenticatedClientAsync(string role)
    {
        var client = Factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-{TestRunId}-{Guid.NewGuid():N}@example.com";
        var payload = new RegisterPayload(Email: email, Password: "Password123!", DisplayName: $"{role} User", Role: null);
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", payload);
        response.EnsureSuccessStatusCode();
        var cookies = ExtractCookies(response);

        if (!string.Equals(role, UserRole.User.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var parsedRole = Enum.Parse<UserRole>(role, true);
            await PromoteUserAsync(email, parsedRole);
        }

        var userId = await GetUserIdByEmailAsync(email);
        TrackUserId(userId);
        return new AuthenticatedClient(client, cookies, userId);
    }

    private async Task<string> GetUserIdByEmailAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        return user.Id;
    }

    private async Task PromoteUserAsync(string email, UserRole role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.Role = role;
        await db.SaveChangesAsync();
    }

    private static List<string> ExtractCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookie))
        {
            return new List<string>();
        }

        return setCookie
            .Select(cookie => cookie.Split(';')[0])
            .ToList();
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static async Task<HttpResponseMessage> SendWithCookiesAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        IReadOnlyList<string> cookies,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, url);

        foreach (var cookie in cookies)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        if (body != null)
        {
            request.Content = JsonContent.Create(body, null, JsonOptions);
        }

        return await client.SendAsync(request);
    }

    private sealed class AuthenticatedClient : IAsyncDisposable, IDisposable
    {
        public AuthenticatedClient(HttpClient client, List<string> cookies, string userId)
        {
            Client = client;
            Cookies = cookies;
            UserId = userId;
        }

        public HttpClient Client { get; }
        public List<string> Cookies { get; }
        public string UserId { get; }

        public ValueTask DisposeAsync()
        {
            Client.Dispose();
            return ValueTask.CompletedTask;
        }

        public void Dispose()
        {
            Client.Dispose();
        }
    }

    #endregion
}
