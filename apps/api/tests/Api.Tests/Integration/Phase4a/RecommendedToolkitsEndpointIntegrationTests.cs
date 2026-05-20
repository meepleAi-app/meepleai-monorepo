using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Phase4a;

/// <summary>
/// Integration tests for <c>GET /api/v1/toolkits/recommended</c>
/// (Wave 3 Phase 4a, PR #732 §4.3.4 / Issue #805).
///
/// <para>
/// Powers the SP4 /discover route's "Recommended toolkits" rail. Sort:
/// rating-weighted Bayesian score with createdAt DESC fallback (effective
/// in v1 because rating/install entities are deferred to Phase 4b).
/// </para>
///
/// <para>
/// Visibility: only published + approved toolkits surface. Drafts, pending,
/// and rejected are filtered out (PR #732 §5.2 security boundary).
/// </para>
///
/// <para>
/// Empty-state contract per PR #732 §3.4: 200 with <c>{ items: [] }</c>
/// rather than 404 / 204.
/// </para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameToolkit")]
[Trait("Wave", "3-Phase-4a")]
public sealed class RecommendedToolkitsEndpointIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public RecommendedToolkitsEndpointIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"recommended_toolkits_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }
        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task RecommendedToolkits_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/toolkits/recommended?limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecommendedToolkits_WithEmptyCatalog_Returns200WithEmptyItems()
    {
        // PR #732 §3.4 — empty state is 200 with { items: [] }, not 404.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/toolkits/recommended?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task RecommendedToolkits_OnlyReturnsPublishedAndApproved()
    {
        // Drafts, pending, rejected, and yanked must NOT surface on the public
        // discover rail (PR #732 §5.2 security boundary).
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Approved + published → SHOULD surface.
        await SeedToolkitAsync(dbContext, authorId, "Approved+Published",
            isPublished: true, templateStatus: TemplateStatus.Approved,
            createdAt: DateTime.UtcNow.AddDays(-1));
        // Draft → must NOT surface.
        await SeedToolkitAsync(dbContext, authorId, "Hidden Draft",
            isPublished: false, templateStatus: TemplateStatus.Draft,
            createdAt: DateTime.UtcNow.AddHours(-1));
        // Approved but unpublished → must NOT surface (combined gate).
        await SeedToolkitAsync(dbContext, authorId, "Approved Unpublished",
            isPublished: false, templateStatus: TemplateStatus.Approved,
            createdAt: DateTime.UtcNow.AddHours(-2));
        // Published but pending → must NOT surface.
        await SeedToolkitAsync(dbContext, authorId, "Pending Published",
            isPublished: true, templateStatus: TemplateStatus.PendingReview,
            createdAt: DateTime.UtcNow.AddHours(-3));

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/toolkits/recommended?limit=10",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("name").GetString().Should().Be("Approved+Published");
    }

    [Fact]
    public async Task RecommendedToolkits_SortedByCreatedAtDescendingV1Fallback()
    {
        // Schema reality v1 (Gate B): rating + install metrics are 0/null,
        // so the Bayesian score collapses to createdAt DESC.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await SeedToolkitAsync(dbContext, authorId, "Oldest", true, TemplateStatus.Approved,
            createdAt: DateTime.UtcNow.AddDays(-30));
        await SeedToolkitAsync(dbContext, authorId, "Middle", true, TemplateStatus.Approved,
            createdAt: DateTime.UtcNow.AddDays(-7));
        await SeedToolkitAsync(dbContext, authorId, "Newest", true, TemplateStatus.Approved,
            createdAt: DateTime.UtcNow.AddDays(-1));

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/toolkits/recommended?limit=10",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(3);
        items[0].GetProperty("name").GetString().Should().Be("Newest");
        items[1].GetProperty("name").GetString().Should().Be("Middle");
        items[2].GetProperty("name").GetString().Should().Be("Oldest");
    }

    [Fact]
    public async Task RecommendedToolkits_DtoExposesV1StubFields()
    {
        // Schema reality v1 (Gate B): installCount=0, ratingAverage=null,
        // ratingCount=0, coverImageUrl=null.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await SeedToolkitAsync(dbContext, authorId, "Wingspan Toolkit", true,
            TemplateStatus.Approved, createdAt: DateTime.UtcNow.AddHours(-1));

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/toolkits/recommended?limit=10",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items")[0];
        item.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("name").GetString().Should().Be("Wingspan Toolkit");
        // Author name resolved from UserEntity (DisplayName fallback to email "Test User").
        item.GetProperty("authorName").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("installCount").GetInt32().Should().Be(0);
        item.GetProperty("ratingAverage").ValueKind.Should().Be(JsonValueKind.Null);
        item.GetProperty("ratingCount").GetInt32().Should().Be(0);
        item.GetProperty("coverImageUrl").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task RecommendedToolkits_RespectsLimitParameter()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        for (var i = 0; i < 5; i++)
        {
            await SeedToolkitAsync(dbContext, authorId, $"Toolkit {i}",
                true, TemplateStatus.Approved,
                createdAt: DateTime.UtcNow.AddMinutes(-i));
        }

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/toolkits/recommended?limit=2",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task RecommendedToolkits_ClampsExcessiveLimitTo50()
    {
        // PR #732 §3.3: silent UI-friendly clamp rather than 400.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (authorId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (_, viewerToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await SeedToolkitAsync(dbContext, authorId, "Solo", true, TemplateStatus.Approved,
            createdAt: DateTime.UtcNow);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/toolkits/recommended?limit=200",
            viewerToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // Only 1 toolkit exists, but the request wouldn't 400 with limit=200.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    /// <summary>
    /// Seeds an authored GameToolkit with explicit createdAt. Mirrors the raw
    /// SQL pattern from <c>ToolkitMarketplaceEndpointsIntegrationTests</c> —
    /// the row-version + JSONB columns require a raw INSERT to bypass EF's
    /// store-generated stripping of <c>RowVersion</c>.
    /// </summary>
    private static async Task SeedToolkitAsync(
        MeepleAiDbContext dbContext,
        Guid authorId,
        string name,
        bool isPublished,
        TemplateStatus templateStatus,
        DateTime createdAt)
    {
        // GameToolkitEntity.GameId references the "games" table (GameEntity).
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(),
            Title = $"Game for {name}",
            CreatedAt = DateTime.UtcNow,
        };
        dbContext.SharedGames.Add(game);
        await dbContext.SaveChangesAsync();

        var toolkitId = Guid.NewGuid();
        var updatedAt = createdAt;
        var templateStatusInt = (int)templateStatus;
        var isTemplate = templateStatus == TemplateStatus.Approved;

        const string sql = """
            INSERT INTO "GameToolkits" (
                "Id", "GameId", "PrivateGameId", "Name", "Version",
                "CreatedByUserId", "IsPublished",
                "OverridesTurnOrder", "OverridesScoreboard", "OverridesDiceSet",
                "CreatedAt", "UpdatedAt",
                "DiceToolsJson", "CardToolsJson", "TimerToolsJson",
                "CounterToolsJson", "UserDicePresetsJson",
                "ScoringTemplateJson", "TurnTemplateJson", "StateTemplate",
                "AgentConfig",
                "TemplateStatus", "IsTemplate",
                "ReviewNotes", "ReviewedByUserId", "ReviewedAt",
                "RowVersion"
            ) VALUES (
                {0}, {1}, NULL, {2}, 1,
                {3}, {4},
                FALSE, FALSE, FALSE,
                {5}, {6},
                NULL, NULL, NULL,
                NULL, NULL,
                NULL, NULL, NULL,
                NULL,
                {7}, {8},
                NULL, NULL, NULL,
                E'\\x01'
            )
            """;

        await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            toolkitId,
            game.Id,
            name,
            authorId,
            isPublished,
            createdAt,
            updatedAt,
            templateStatusInt,
            isTemplate);
    }
}
