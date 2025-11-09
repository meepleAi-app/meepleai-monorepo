using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
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
[Collection("Postgres Integration Tests")]
public class CacheAdminEndpointsTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public CacheAdminEndpointsTests(PostgresCollectionFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
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
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/cache/stats");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with stats
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("totalHits", out var hits).Should().BeTrue();
        result.TryGetProperty("totalMisses", out var misses).Should().BeTrue();
        result.TryGetProperty("hitRate", out var hitRate).Should().BeTrue();

        // Verify stats have data
        hits.GetInt64().Should().BeGreaterThan(0);
        misses.GetInt64().Should().BeGreaterThanOrEqualTo(0);
    }

    /// <summary>
    /// Scenario: Admin gets cache stats filtered by game
    ///   Given an authenticated admin user
    ///   And cache stats for multiple games
    ///   When the admin requests GET /admin/cache/stats?gameId={gameId}
    ///   Then HTTP 200 is returned
    ///   And only stats for that game are returned
    /// </summary>
    [Fact(Skip = "Feature not implemented: Per-game stats filtering. GetCacheStatsAsync returns global stats only, ignores gameId parameter. RecordCacheAccessAsync is a no-op. See issue #711.")]
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
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/cache/stats?gameId={game1.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 with game1 stats only
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("totalHits", out var hits).Should().BeTrue();

        // Game1 should have 2 hits (not 5 which would include game2)
        hits.GetInt64().Should().Be(2);
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
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/cache/stats");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
            (await cacheService.GetAsync<QaResponse>(qaKey)).Should().NotBeNull();
            (await cacheService.GetAsync<ExplainResponse>(explainKey)).Should().NotBeNull();
        }

        // When: Admin deletes game cache
        var client = CreateClientWithoutCookies();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/games/{game.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        result.TryGetProperty("message", out _).Should().BeTrue();

        // And: Cache is invalidated
        using (var newScope = Factory.Services.CreateScope())
        {
            var cacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
            var qaKey = cacheService.GenerateQaCacheKey(game.Id, "test query");
            var explainKey = cacheService.GenerateExplainCacheKey(game.Id, "test topic");

            (await cacheService.GetAsync<QaResponse>(qaKey)).Should().BeNull();
            (await cacheService.GetAsync<ExplainResponse>(explainKey)).Should().BeNull();
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
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/v1/admin/cache/games/nonexistent-game-id");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 (idempotent)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/games/{game.Id}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        await CreateTestPdfDocumentAsync(game.Id, admin.Id); // Create PDF for test setup

        using (var scope = Factory.Services.CreateScope())
        {
            var cacheService = scope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();

            // Create cached response with PDF tag
            var qaKey = cacheService.GenerateQaCacheKey(game.Id, "pdf question");
            await cacheService.SetAsync(qaKey, new QaResponse("cached with pdf tag", Array.Empty<Snippet>()), 3600);

            // Verify cache exists
            (await cacheService.GetAsync<QaResponse>(qaKey)).Should().NotBeNull();
        }

        // When: Admin deletes by game tag (cache entries are tagged with game:{gameId}, not pdf:{pdfId})
        var client = CreateClientWithoutCookies();
        var tag = $"game:{game.Id}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/tags/{Uri.EscapeDataString(tag)}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // And: Cache with that tag is invalidated
        using (var newScope = Factory.Services.CreateScope())
        {
            var cacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
            var qaKey = cacheService.GenerateQaCacheKey(game.Id, "pdf question");
            (await cacheService.GetAsync<QaResponse>(qaKey)).Should().BeNull();
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
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/tags/{encodedTag}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 200 (even if no entries exist)
        response.StatusCode.Should().Be(HttpStatusCode.OK);
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
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/tags/{Uri.EscapeDataString(tag)}");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: HTTP 403 Forbidden
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
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
    [Fact(Skip = "Feature not implemented: Stats preservation. RecordCacheAccessAsync is a no-op, stats are global not per-invalidation. See issue #711.")]
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
        using var statsRequest1 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/cache/stats?gameId={game.Id}");
        AddCookies(statsRequest1, cookies);
        var statsResponse1 = await client.SendAsync(statsRequest1);
        var stats1 = await statsResponse1.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var hitsBefore = stats1.GetProperty("totalHits").GetInt64();
        var missesBefore = stats1.GetProperty("totalMisses").GetInt64();

        // When: Invalidate game cache
        using var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/games/{game.Id}");
        AddCookies(deleteRequest, cookies);
        await client.SendAsync(deleteRequest);

        // Then: Stats history is preserved
        using var statsRequest2 = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/admin/cache/stats?gameId={game.Id}");
        AddCookies(statsRequest2, cookies);
        var statsResponse2 = await client.SendAsync(statsRequest2);
        var stats2 = await statsResponse2.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
        var hitsAfter = stats2.GetProperty("totalHits").GetInt64();
        var missesAfter = stats2.GetProperty("totalMisses").GetInt64();

        // Stats should remain the same (history preserved)
        hitsAfter.Should().Be(hitsBefore);
        missesAfter.Should().Be(missesBefore);
    }

    /// <summary>
    /// Scenario: Multiple tag invalidations are independent
    ///   Given cached responses with different tags
    ///   When admin invalidates one tag
    ///   Then only entries with that tag are invalidated
    ///   And entries with other tags remain cached
    /// </summary>
    [Fact(Skip = "Feature not implemented: PDF-specific tags. Current implementation only supports game:{gameId} and endpoint:{type} tags. See issue #711.")]
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
                3600);

            // Cache with pdf2 tag
            var key2 = cacheService.GenerateQaCacheKey(game.Id, "pdf2 question");
            await cacheService.SetAsync(key2, new QaResponse("pdf2 answer", Array.Empty<Snippet>()),
                3600);

            // Verify both cached
            (await cacheService.GetAsync<QaResponse>(key1)).Should().NotBeNull();
            (await cacheService.GetAsync<QaResponse>(key2)).Should().NotBeNull();
        }

        // When: Invalidate pdf1 tag
        var client = CreateClientWithoutCookies();
        var tag1 = $"pdf:{pdf1.Id}";
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/admin/cache/tags/{Uri.EscapeDataString(tag1)}");
        AddCookies(request, cookies);
        await client.SendAsync(request);

        // Then: Only pdf1 cache invalidated, pdf2 remains
        using (var newScope = Factory.Services.CreateScope())
        {
            var cacheService = newScope.ServiceProvider.GetRequiredService<IAiResponseCacheService>();
            var key1 = cacheService.GenerateQaCacheKey(game.Id, "pdf1 question");
            var key2 = cacheService.GenerateQaCacheKey(game.Id, "pdf2 question");

            (await cacheService.GetAsync<QaResponse>(key1)).Should().BeNull(); // Invalidated
            (await cacheService.GetAsync<QaResponse>(key2)).Should().NotBeNull(); // Still cached
        }
    }

    #endregion
}