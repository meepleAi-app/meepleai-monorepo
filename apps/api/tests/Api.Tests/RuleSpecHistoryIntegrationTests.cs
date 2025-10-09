using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for RuleSpec history and versioning endpoints.
///
/// Feature: RuleSpec version history and diff functionality
/// As an editor or admin
/// I want to view RuleSpec version history and compare versions
/// So that I can track changes to game rules over time
/// </summary>
public class RuleSpecHistoryIntegrationTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public RuleSpecHistoryIntegrationTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    [Fact]
    public async Task GetHistory_ReturnsVersionsForEditor()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"history-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "History Test Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Get,
            $"/games/{gameId}/rulespec/history",
            editor.Cookies);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await DeserializeAsync<RuleSpecHistory>(response);
        Assert.NotNull(history);
        Assert.Equal(gameId, history!.GameId);
        Assert.Equal(3, history.TotalVersions);
        Assert.Equal(3, history.Versions.Count);
        Assert.Contains(history.Versions, v => v.Version == "v1");
        Assert.Contains(history.Versions, v => v.Version == "v2");
        Assert.Contains(history.Versions, v => v.Version == "v3");
    }

    [Fact]
    public async Task GetHistory_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"history-unauth-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "History Unauthorized Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        using var client = Factory.CreateHttpsClient();
        var response = await client.GetAsync($"/games/{gameId}/rulespec/history");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetHistory_ReturnsForbidden_ForNonPrivilegedUser()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"history-forbidden-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "History Forbidden Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        await using var viewer = await CreateAuthenticatedClientAsync("User");

        var response = await SendWithCookiesAsync(
            viewer.Client,
            HttpMethod.Get,
            $"/games/{gameId}/rulespec/history",
            viewer.Cookies);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetVersion_ReturnsRequestedRuleSpec()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"versions-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "Versions Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Get,
            $"/games/{gameId}/rulespec/versions/v2",
            editor.Cookies);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ruleSpec = await DeserializeAsync<RuleSpec>(response);
        Assert.NotNull(ruleSpec);
        Assert.Equal(gameId, ruleSpec!.gameId);
        Assert.Equal("v2", ruleSpec.version);
        Assert.Equal(2, ruleSpec.rules.Count);
    }

    [Fact]
    public async Task GetVersion_ReturnsNotFound_WhenVersionDoesNotExist()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"versions-missing-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "Versions Missing Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Get,
            $"/games/{gameId}/rulespec/versions/v99",
            editor.Cookies);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetVersion_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"versions-unauth-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "Versions Unauthorized Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        using var client = Factory.CreateHttpsClient();
        var response = await client.GetAsync($"/games/{gameId}/rulespec/versions/v1");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetVersion_ReturnsForbidden_ForNonPrivilegedUser()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"versions-forbidden-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "Versions Forbidden Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        await using var viewer = await CreateAuthenticatedClientAsync("User");

        var response = await SendWithCookiesAsync(
            viewer.Client,
            HttpMethod.Get,
            $"/games/{gameId}/rulespec/versions/v1",
            viewer.Cookies);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Diff_ReturnsBadRequest_WhenParametersMissing()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"diff-badrequest-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "Diff BadRequest Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Get,
            $"/games/{gameId}/rulespec/diff",
            editor.Cookies);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Diff_ReturnsNotFound_WhenVersionIsMissing()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"diff-notfound-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "Diff NotFound Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Get,
            $"/games/{gameId}/rulespec/diff?from=v1&to=v42",
            editor.Cookies);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Diff_ComputesSummaryAndChanges_ForValidVersions()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"diff-success-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "Diff Success Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        await using var editor = await CreateAuthenticatedClientAsync("Editor");

        var response = await SendWithCookiesAsync(
            editor.Client,
            HttpMethod.Get,
            $"/games/{gameId}/rulespec/diff?from=v1&to=v2",
            editor.Cookies);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var diff = await DeserializeAsync<RuleSpecDiff>(response);
        Assert.NotNull(diff);
        Assert.Equal(gameId, diff!.GameId);
        Assert.Equal("v1", diff.FromVersion);
        Assert.Equal("v2", diff.ToVersion);

        Assert.Equal(3, diff.Summary.TotalChanges);
        Assert.Equal(1, diff.Summary.Added);
        Assert.Equal(1, diff.Summary.Modified);
        Assert.Equal(1, diff.Summary.Deleted);

        Assert.Contains(diff.Changes, change =>
            change.Type == ChangeType.Added && change.NewAtom == "endgame");
        Assert.Contains(diff.Changes, change =>
            change.Type == ChangeType.Deleted && change.OldAtom == "scoring");
        Assert.Contains(diff.Changes, change =>
            change.Type == ChangeType.Modified &&
            change.OldAtom == "setup" &&
            change.FieldChanges != null &&
            change.FieldChanges.Any(field => field.FieldName == "text"));
    }

    [Fact]
    public async Task Diff_ReturnsUnauthorized_WhenNotAuthenticated()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"diff-unauth-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "Diff Unauthorized Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        using var client = Factory.CreateHttpsClient();
        var response = await client.GetAsync($"/games/{gameId}/rulespec/diff?from=v1&to=v2");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Diff_ReturnsForbidden_ForNonPrivilegedUser()
    {
        var adminUserId = await RegisterUserAsync("Admin");
        var gameId = $"diff-forbidden-{Guid.NewGuid():N}";
        await CreateGameAsync(gameId, "Diff Forbidden Game");
        await SeedRuleSpecVersionsAsync(gameId, adminUserId);

        await using var viewer = await CreateAuthenticatedClientAsync("User");

        var response = await SendWithCookiesAsync(
            viewer.Client,
            HttpMethod.Get,
            $"/games/{gameId}/rulespec/diff?from=v1&to=v2",
            viewer.Cookies);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task CreateGameAsync(string gameId, string name)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        if (!await db.Games.AnyAsync(g => g.Id == gameId))
        {
            db.Games.Add(new GameEntity
            {
                Id = gameId,
                Name = name,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
            TrackGameId(gameId);
        }
    }

    private async Task<IReadOnlyDictionary<string, RuleSpec>> SeedRuleSpecVersionsAsync(string gameId, string createdByUserId)
    {
        using var scope = Factory.Services.CreateScope();
        var ruleSpecService = scope.ServiceProvider.GetRequiredService<RuleSpecService>();

        var version1 = await ruleSpecService.UpdateRuleSpecAsync(
            gameId,
            new RuleSpec(
                gameId,
                "v1",
                DateTime.UtcNow,
                new List<RuleAtom>
                {
                    new("setup", "Place two workers", "Setup", "3", "12"),
                    new("scoring", "Gain 1 VP per coin", "Scoring", "8", "4")
                }),
            createdByUserId);

        var version2 = await ruleSpecService.UpdateRuleSpecAsync(
            gameId,
            new RuleSpec(
                gameId,
                "v2",
                DateTime.UtcNow.AddMinutes(1),
                new List<RuleAtom>
                {
                    new("setup", "Place three workers", "Setup", "3", "12"),
                    new("endgame", "Game ends after 8 rounds", "Endgame", "12", "1")
                }),
            createdByUserId);

        var version3 = await ruleSpecService.UpdateRuleSpecAsync(
            gameId,
            new RuleSpec(
                gameId,
                "v3",
                DateTime.UtcNow.AddMinutes(2),
                new List<RuleAtom>
                {
                    new("setup", "Place three workers", "Setup", "3", "12"),
                    new("endgame", "Game ends after 8 rounds", "Endgame", "12", "1"),
                    new("scoring", "Gain 2 VP per coin", "Scoring", "8", "5")
                }),
            createdByUserId);

        return new Dictionary<string, RuleSpec>
        {
            ["v1"] = version1,
            ["v2"] = version2,
            ["v3"] = version3
        };
    }

    private async Task<string> RegisterUserAsync(string role)
    {
        using var client = Factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-{TestRunId}-{Guid.NewGuid():N}@example.com";
        var payload = new RegisterPayload(email, "Password123!", $"{role} User", null);
        var response = await client.PostAsJsonAsync("/auth/register", payload);
        response.EnsureSuccessStatusCode();
        if (!string.Equals(role, UserRole.User.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var parsedRole = Enum.Parse<UserRole>(role, true);
            await PromoteUserAsync(email, parsedRole);
        }
        var userId = await GetUserIdByEmailAsync(email);
        TrackUserId(userId);
        return userId;
    }

    private async Task<AuthenticatedClient> CreateAuthenticatedClientAsync(string role)
    {
        var client = Factory.CreateHttpsClient();
        var email = $"{role.ToLowerInvariant()}-{TestRunId}-{Guid.NewGuid():N}@example.com";
        var payload = new RegisterPayload(email, "Password123!", $"{role} User", null);
        var response = await client.PostAsJsonAsync("/auth/register", payload);
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

    private async Task PromoteUserAsync(string email, UserRole role)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        user.Role = role;
        await db.SaveChangesAsync();
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static Task<HttpResponseMessage> SendWithCookiesAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        IReadOnlyList<string> cookies)
    {
        var request = new HttpRequestMessage(method, url);

        foreach (var cookie in cookies)
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        return client.SendAsync(request);
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
}
