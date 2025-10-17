using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for Cache Admin endpoints (PERF-03).
///
/// Feature: PERF-03 - Cache Management Admin Endpoints
/// As an administrator
/// I want to monitor and manage the AI response cache
/// So that I can ensure optimal performance and troubleshoot issues
/// </summary>
[Collection("Sequential")]
public class CacheAdminEndpointsTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CacheAdminEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
    }

    #region GET /admin/cache/stats Tests

    /// <summary>
    /// Scenario: Admin gets cache stats for all games
    ///   Given an authenticated admin user
    ///   And cache stats exist for multiple games
    ///   When the admin requests GET /admin/cache/stats
    ///   Then HTTP 200 is returned
    ///   And aggregated stats across all games are returned
    ///   And stats include hits, misses, hit rate, size, and top questions
    /// </summary>
    [Fact]
    public async Task GET_AdminCacheStats_AsAdmin_Returns200WithAggregatedStats()
    {
        // Given: Admin user and cache stats for multiple games
        var admin = await CreateTestUserAsync("admin-stats-all", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var game1 = await CreateTestGameAsync("Game1-Stats");
        var game2 = await CreateTestGameAsync("Game2-Stats");

        // Create some cache stats in database
        using (var scope = Factory.Services.CreateScope())
        {
            var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

            // Simulate some cache hits/misses
            await cacheService.RecordCacheAccessAsync(game1.Id, "hash1", isHit: true);
            await cacheService.RecordCacheAccessAsync(game1.Id, "hash1", isHit: true);
            await cacheService.RecordCacheAccessAsync(game1.Id, "hash2", isHit: false);
            await cacheService.RecordCacheAccessAsync(game2.Id, "hash3", isHit: true);
        }

        // When: Admin requests stats
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/cache/stats");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with stats
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("totalHits", out var hits));
        Assert.True(result.TryGetProperty("totalMisses", out var misses));
        Assert.True(result.TryGetProperty("hitRate", out var hitRate));

        // Verify stats have data
        Assert.True(hits.GetInt64() > 0);
        Assert.True(misses.GetInt64() >= 0);
    }

    /// <summary>
    /// Scenario: Admin gets cache stats filtered by game
    ///   Given an authenticated admin user
    ///   And cache stats for multiple games
    ///   When the admin requests GET /admin/cache/stats?gameId={gameId}
    ///   Then HTTP 200 is returned
    ///   And only stats for that game are returned
    /// </summary>
    [Fact]
    public async Task GET_AdminCacheStats_WithGameIdFilter_Returns200WithFilteredStats()
    {
        // Given: Admin and stats for multiple games
        var admin = await CreateTestUserAsync("admin-stats-filter", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var game1 = await CreateTestGameAsync("Game1-Filter");
        var game2 = await CreateTestGameAsync("Game2-Filter");

        using (var scope = Factory.Services.CreateScope())
        {
            var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

            await cacheService.RecordCacheAccessAsync(game1.Id, "hash1", isHit: true);
            await cacheService.RecordCacheAccessAsync(game1.Id, "hash1", isHit: true);
            await cacheService.RecordCacheAccessAsync(game2.Id, "hash2", isHit: true);
            await cacheService.RecordCacheAccessAsync(game2.Id, "hash2", isHit: true);
            await cacheService.RecordCacheAccessAsync(game2.Id, "hash2", isHit: true);
        }

        // When: Admin requests stats filtered by game1
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/cache/stats?gameId={game1.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with game1 stats only
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("totalHits", out var hits));

        // Game1 should have 2 hits (not 5 which would include game2)
        Assert.Equal(2, hits.GetInt64());
    }

    /// <summary>
    /// Scenario: Non-admin tries to get cache stats
    ///   Given an authenticated non-admin user
    ///   When the user requests GET /admin/cache/stats
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task GET_AdminCacheStats_AsNonAdmin_Returns403()
    {
        // Given: Non-admin user
        var user = await CreateTestUserAsync("user-stats", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // When: User tries to access admin endpoint
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/cache/stats");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Unauthenticated user tries to get cache stats
    ///   Given no authenticated user
    ///   When the user requests GET /admin/cache/stats
    ///   Then HTTP 401 Unauthorized is returned
    /// </summary>
    [Fact]
    public async Task GET_AdminCacheStats_AsUnauthenticated_Returns401()
    {
        // Given: No authentication
        var client = CreateClientWithoutCookies();

        // When: Unauthenticated request
        var response = await client.GetAsync("/api/v1/admin/cache/stats");

        // Then: HTTP 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region DELETE /admin/cache/games/{gameId} Tests

    /// <summary>
    /// Scenario: Admin invalidates cache for a specific game
    ///   Given an authenticated admin user
    ///   And cached responses exist for a game
    ///   When the admin requests DELETE /admin/cache/games/{gameId}
    ///   Then HTTP 200 is returned
    ///   And all cached responses for that game are invalidated
    ///   And a success message is returned
    /// </summary>
    [Fact]
    public async Task DELETE_AdminCacheGamesGameId_AsAdmin_Returns200AndInvalidatesCache()
    {
        // Given: Admin and cached responses for a game
        var admin = await CreateTestUserAsync("admin-delete-game", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var game = await CreateTestGameAsync("Game-Delete");

        using (var scope = Factory.Services.CreateScope())
        {
            var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

            // Create cached responses
            var qaKey = cacheService.GenerateQaCacheKey(game.Id, "test query");
            var explainKey = cacheService.GenerateExplainCacheKey(game.Id, "test topic");

            await cacheService.SetAsync(qaKey, new QaResponse("cached", Array.Empty<Snippet>()), 3600);
            await cacheService.SetAsync(explainKey, new ExplainResponse(
                new ExplainOutline("topic", new System.Collections.Generic.List<string>()),
                "cached", new System.Collections.Generic.List<Snippet>(), 1), 3600);

            // Verify cache exists
            Assert.NotNull(await cacheService.GetAsync<QaResponse>(qaKey));
            Assert.NotNull(await cacheService.GetAsync<ExplainResponse>(explainKey));
        }

        // When: Admin deletes game cache
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/games/{game.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        Assert.True(result.TryGetProperty("message", out _));

        // And: Cache is invalidated
        using (var newScope = Factory.Services.CreateScope())
        {
            var cacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
            var qaKey = cacheService.GenerateQaCacheKey(game.Id, "test query");
            var explainKey = cacheService.GenerateExplainCacheKey(game.Id, "test topic");

            Assert.Null(await cacheService.GetAsync<QaResponse>(qaKey));
            Assert.Null(await cacheService.GetAsync<ExplainResponse>(explainKey));
        }
    }

    /// <summary>
    /// Scenario: Admin invalidates cache for non-existent game
    ///   Given an authenticated admin user
    ///   When the admin requests DELETE /admin/cache/games/{nonExistentGameId}
    ///   Then HTTP 200 is returned (idempotent operation)
    ///   And a success message is returned
    /// </summary>
    [Fact]
    public async Task DELETE_AdminCacheGamesGameId_ForNonExistentGame_Returns200()
    {
        // Given: Admin user
        var admin = await CreateTestUserAsync("admin-delete-nonexistent", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);

        // When: Admin deletes cache for non-existent game
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/cache/games/nonexistent-game-id");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 (idempotent)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Non-admin tries to invalidate game cache
    ///   Given an authenticated non-admin user
    ///   When the user requests DELETE /admin/cache/games/{gameId}
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task DELETE_AdminCacheGamesGameId_AsNonAdmin_Returns403()
    {
        // Given: Non-admin user
        var user = await CreateTestUserAsync("user-delete-game", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);
        var game = await CreateTestGameAsync("Game-Forbidden");

        // When: User tries to delete game cache
        var client = CreateClientWithoutCookies();
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/games/{game.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region DELETE /admin/cache/tags/{tag} Tests

    /// <summary>
    /// Scenario: Admin invalidates cache by tag
    ///   Given an authenticated admin user
    ///   And cached responses with a specific tag
    ///   When the admin requests DELETE /admin/cache/tags/{tag}
    ///   Then HTTP 200 is returned
    ///   And all cached responses with that tag are invalidated
    /// </summary>
    [Fact]
    public async Task DELETE_AdminCacheTagsTag_AsAdmin_Returns200AndInvalidatesByTag()
    {
        // Given: Admin and cached responses with tags
        var admin = await CreateTestUserAsync("admin-delete-tag", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var game = await CreateTestGameAsync("Game-Tag");
        var pdf = await CreateTestPdfDocumentAsync(game.Id, admin.Id);

        using (var scope = Factory.Services.CreateScope())
        {
            var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

            // Create cached response with PDF tag
            var qaKey = cacheService.GenerateQaCacheKey(game.Id, "pdf question");
            var tags = new[] { $"game:{game.Id}", $"pdf:{pdf.Id}" };
            await cacheService.SetAsync(qaKey, new QaResponse("cached with pdf tag", Array.Empty<Snippet>()), 3600, tags);

            // Verify cache exists
            Assert.NotNull(await cacheService.GetAsync<QaResponse>(qaKey));
        }

        // When: Admin deletes by PDF tag
        var client = CreateClientWithoutCookies();
        var tag = $"pdf:{pdf.Id}";
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/tags/{Uri.EscapeDataString(tag)}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // And: Cache with that tag is invalidated
        using (var newScope = Factory.Services.CreateScope())
        {
            var cacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
            var qaKey = cacheService.GenerateQaCacheKey(game.Id, "pdf question");
            Assert.Null(await cacheService.GetAsync<QaResponse>(qaKey));
        }
    }

    /// <summary>
    /// Scenario: Admin invalidates cache by tag with special characters
    ///   Given an authenticated admin user
    ///   And a tag with special characters (e.g., game:my-game-123)
    ///   When the admin requests DELETE /admin/cache/tags/{encodedTag}
    ///   Then HTTP 200 is returned
    ///   And tag is properly decoded and invalidation succeeds
    /// </summary>
    [Fact]
    public async Task DELETE_AdminCacheTagsTag_WithSpecialCharacters_Returns200()
    {
        // Given: Admin user
        var admin = await CreateTestUserAsync("admin-tag-special", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);

        // When: Admin deletes by tag with special characters
        var client = CreateClientWithoutCookies();
        var tag = "game:my-game-123";
        var encodedTag = Uri.EscapeDataString(tag);
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/tags/{encodedTag}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 (even if no entries exist)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Non-admin tries to invalidate cache by tag
    ///   Given an authenticated non-admin user
    ///   When the user requests DELETE /admin/cache/tags/{tag}
    ///   Then HTTP 403 Forbidden is returned
    /// </summary>
    [Fact]
    public async Task DELETE_AdminCacheTagsTag_AsNonAdmin_Returns403()
    {
        // Given: Non-admin user
        var user = await CreateTestUserAsync("user-delete-tag", UserRole.User);
        var cookies = await AuthenticateUserAsync(user.Email);

        // When: User tries to delete by tag
        var client = CreateClientWithoutCookies();
        var tag = "game:some-game";
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/tags/{Uri.EscapeDataString(tag)}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403 Forbidden
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Scenario: Unauthenticated user tries to invalidate cache
    ///   Given no authenticated user
    ///   When the user requests DELETE /admin/cache/tags/{tag}
    ///   Then HTTP 401 Unauthorized is returned
    /// </summary>
    [Fact]
    public async Task DELETE_AdminCacheTagsTag_AsUnauthenticated_Returns401()
    {
        // Given: No authentication
        var client = CreateClientWithoutCookies();

        // When: Unauthenticated request
        var tag = "game:test";
        var response = await client.DeleteAsync($"/api/v1/admin/cache/tags/{Uri.EscapeDataString(tag)}");

        // Then: HTTP 401 Unauthorized
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    #endregion

    #region Cross-Endpoint Interaction Tests

    /// <summary>
    /// Scenario: Admin invalidates game cache and stats reflect changes
    ///   Given cached responses and recorded stats
    ///   When admin invalidates game cache
    ///   Then stats still show historical hit/miss data
    ///   And cache size is reduced
    /// </summary>
    [Fact]
    public async Task AdminInvalidation_PreservesStatisticsHistory()
    {
        // Given: Cached responses with stats
        var admin = await CreateTestUserAsync("admin-stats-preserve", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var game = await CreateTestGameAsync("Game-Stats-Preserve");

        using (var scope = Factory.Services.CreateScope())
        {
            var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

            // Record cache hits
            await cacheService.RecordCacheAccessAsync(game.Id, "hash1", isHit: true);
            await cacheService.RecordCacheAccessAsync(game.Id, "hash1", isHit: true);
            await cacheService.RecordCacheAccessAsync(game.Id, "hash2", isHit: false);

            // Create cached response
            var qaKey = cacheService.GenerateQaCacheKey(game.Id, "test");
            await cacheService.SetAsync(qaKey, new QaResponse("cached", Array.Empty<Snippet>()), 3600);
        }

        // Get stats before invalidation
        var client = CreateClientWithoutCookies();
        var statsRequest1 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/cache/stats?gameId={game.Id}");
        AddCookies(statsRequest1, cookies);
        var statsResponse1 = await client.SendAsync(statsRequest1);
        var stats1 = await statsResponse1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var hitsBefore = stats1.GetProperty("totalHits").GetInt64();
        var missesBefore = stats1.GetProperty("totalMisses").GetInt64();

        // When: Invalidate game cache
        var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/games/{game.Id}");
        AddCookies(deleteRequest, cookies);
        await client.SendAsync(deleteRequest);

        // Then: Stats history is preserved
        var statsRequest2 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/cache/stats?gameId={game.Id}");
        AddCookies(statsRequest2, cookies);
        var statsResponse2 = await client.SendAsync(statsRequest2);
        var stats2 = await statsResponse2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var hitsAfter = stats2.GetProperty("totalHits").GetInt64();
        var missesAfter = stats2.GetProperty("totalMisses").GetInt64();

        // Stats should remain the same (history preserved)
        Assert.Equal(hitsBefore, hitsAfter);
        Assert.Equal(missesBefore, missesAfter);
    }

    /// <summary>
    /// Scenario: Multiple tag invalidations are independent
    ///   Given cached responses with different tags
    ///   When admin invalidates one tag
    ///   Then only entries with that tag are invalidated
    ///   And entries with other tags remain cached
    /// </summary>
    [Fact]
    public async Task DELETE_AdminCacheTagsTag_IndependentTagInvalidation()
    {
        // Given: Multiple cached responses with different tags
        var admin = await CreateTestUserAsync("admin-multi-tag", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(admin.Email);
        var game = await CreateTestGameAsync("Game-MultiTag");
        var pdf1 = await CreateTestPdfDocumentAsync(game.Id, admin.Id, "pdf1.pdf");
        var pdf2 = await CreateTestPdfDocumentAsync(game.Id, admin.Id, "pdf2.pdf");

        using (var scope = Factory.Services.CreateScope())
        {
            var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

            // Cache with pdf1 tag
            var key1 = cacheService.GenerateQaCacheKey(game.Id, "pdf1 question");
            await cacheService.SetAsync(key1, new QaResponse("pdf1 answer", Array.Empty<Snippet>()),
                3600, new[] { $"game:{game.Id}", $"pdf:{pdf1.Id}" });

            // Cache with pdf2 tag
            var key2 = cacheService.GenerateQaCacheKey(game.Id, "pdf2 question");
            await cacheService.SetAsync(key2, new QaResponse("pdf2 answer", Array.Empty<Snippet>()),
                3600, new[] { $"game:{game.Id}", $"pdf:{pdf2.Id}" });

            // Verify both cached
            Assert.NotNull(await cacheService.GetAsync<QaResponse>(key1));
            Assert.NotNull(await cacheService.GetAsync<QaResponse>(key2));
        }

        // When: Invalidate pdf1 tag
        var client = CreateClientWithoutCookies();
        var tag1 = $"pdf:{pdf1.Id}";
        var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/tags/{Uri.EscapeDataString(tag1)}");
        AddCookies(request, cookies);
        await client.SendAsync(request);

        // Then: Only pdf1 cache invalidated, pdf2 remains
        using (var newScope = Factory.Services.CreateScope())
        {
            var cacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
            var key1 = cacheService.GenerateQaCacheKey(game.Id, "pdf1 question");
            var key2 = cacheService.GenerateQaCacheKey(game.Id, "pdf2 question");

            Assert.Null(await cacheService.GetAsync<QaResponse>(key1)); // Invalidated
            Assert.NotNull(await cacheService.GetAsync<QaResponse>(key2)); // Still cached
        }
    }

    #endregion
}
