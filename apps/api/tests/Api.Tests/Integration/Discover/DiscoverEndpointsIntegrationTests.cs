using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

namespace Api.Tests.Integration.Discover;

/// <summary>
/// Integration tests for the Wave 3 Phase 1 /discover quick-win endpoints
/// (Issue #805 / PR #732 §4.3.2-4.3.5):
///
/// <list type="number">
///   <item><c>GET /api/v1/catalog/games/new</c> — SharedGameCatalog BC, 1h cache</item>
///   <item><c>GET /api/v1/agents/popular</c> — GameManagement (KnowledgeBase) BC, 15min cache</item>
///   <item><c>GET /api/v1/kb-docs/recent</c> — KnowledgeBase BC, 5min cache</item>
/// </list>
///
/// All three follow the per-PR-#732-§3.4 empty-state contract: 200 with
/// <c>{ items: [] }</c> rather than 404. <c>limit</c> is clamped to <c>[1, 50]</c>
/// at the endpoint layer; the validator on the query enforces the same range
/// for the CQRS handler.
///
/// What is intentionally NOT verified here:
///   - Cache TTL / hit-vs-miss behaviour (the integration factory swaps in a
///     pass-through <c>TestHybridCacheService</c> that always invokes the
///     factory). Cache-key correctness is exercised in handler-level unit tests.
///   - Rate limiting (the integration factory disables the <c>BggSearch</c>
///     -family rate limiters to keep the suite fast and deterministic).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Wave", "3-Phase-1")]
public sealed class DiscoverEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public DiscoverEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"discover_{Guid.NewGuid():N}";
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

    // ──────────────────────────────────────────────────────────────────────
    //  GET /api/v1/catalog/games/new (PR #732 §4.3.2)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task NewGames_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/catalog/games/new?limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task NewGames_WithEmptyCatalog_Returns200WithEmptyItems()
    {
        // PR #732 §3.4 — empty state is 200 with { items: [] }, not 404.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/catalog/games/new?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task NewGames_SortedByCreatedAtDescending()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        // Seed 3 games with explicit, distinct CreatedAt timestamps so the test is
        // independent of insertion order or backing database tiebreak rules.
        var oldest = DateTime.UtcNow.AddDays(-30);
        var middle = DateTime.UtcNow.AddDays(-7);
        var newest = DateTime.UtcNow.AddDays(-1);
        await SeedSharedGameAsync(dbContext, "Old Game", createdAt: oldest);
        await SeedSharedGameAsync(dbContext, "Middle Game", createdAt: middle);
        await SeedSharedGameAsync(dbContext, "New Game", createdAt: newest);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/catalog/games/new?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(3);
        items[0].GetProperty("name").GetString().Should().Be("New Game");
        items[1].GetProperty("name").GetString().Should().Be("Middle Game");
        items[2].GetProperty("name").GetString().Should().Be("Old Game");
    }

    [Fact]
    public async Task NewGames_ExcludesSoftDeletedRows()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await SeedSharedGameAsync(dbContext, "Visible", createdAt: DateTime.UtcNow.AddDays(-1));
        await SeedSharedGameAsync(
            dbContext,
            "Deleted",
            createdAt: DateTime.UtcNow,
            isDeleted: true);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/catalog/games/new?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("name").GetString().Should().Be("Visible");
    }

    [Fact]
    public async Task NewGames_RespectsLimitParameter()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        for (var i = 0; i < 5; i++)
        {
            await SeedSharedGameAsync(
                dbContext,
                $"Game {i}",
                createdAt: DateTime.UtcNow.AddMinutes(-i));
        }

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/catalog/games/new?limit=2",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task NewGames_ClampsExcessiveLimitTo50()
    {
        // PR #732 §3.3 caps page size at 50; the endpoint silently clamps a
        // larger request rather than returning 400 (UI-friendly behaviour).
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await SeedSharedGameAsync(dbContext, "Solo", createdAt: DateTime.UtcNow);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/catalog/games/new?limit=200",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        // Only 1 game exists, but the request wouldn't 400 with limit=200.
        body.GetProperty("items").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public async Task NewGames_ProjectsExpectedFieldShape()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await SeedSharedGameAsync(
            dbContext,
            "Wingspan",
            createdAt: DateTime.UtcNow.AddDays(-2),
            yearPublished: 2019,
            imageUrl: "https://example.com/wingspan.jpg");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/catalog/games/new?limit=1",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items")[0];
        item.GetProperty("id").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("name").GetString().Should().Be("Wingspan");
        item.GetProperty("year").GetInt32().Should().Be(2019);
        item.GetProperty("imageUrl").GetString().Should().Be("https://example.com/wingspan.jpg");
        item.GetProperty("createdAt").GetDateTime().Should().BeCloseTo(
            DateTime.UtcNow.AddDays(-2),
            TimeSpan.FromMinutes(1));
        // Publisher is nullable in the contract — no publishers seeded here.
        item.TryGetProperty("publisher", out var publisher).Should().BeTrue();
        publisher.ValueKind.Should().Be(JsonValueKind.Null);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  GET /api/v1/agents/popular (PR #732 §4.3.3)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PopularAgents_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/agents/popular?limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PopularAgents_WithEmptyCatalog_Returns200WithEmptyItems()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents/popular?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task PopularAgents_OnlyReturnsActiveAgents()
    {
        // The handler reads from GetAllActiveAsync — inactive agents must not surface.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 2, inactiveCount: 3);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents/popular?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task PopularAgents_DtoExposesInstallCountZeroV1()
    {
        // Schema reality v1 carryover (Gate B): there is no AgentInstallation
        // entity, so installCount must always be 0 in the wire response.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 1, inactiveCount: 0);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents/popular?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items")[0];
        item.GetProperty("installCount").GetInt32().Should().Be(0);
        item.GetProperty("invocationCount").GetInt32().Should().Be(0);
        item.GetProperty("name").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task PopularAgents_PopulatesGameNameWhenLinkedToGame()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, "Catan");
        await TestSessionHelper.SeedAgentDefinitionsAsync(
            dbContext,
            activeCount: 1,
            inactiveCount: 0,
            gameId: gameId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents/popular?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items")[0];
        item.GetProperty("gameId").GetGuid().Should().Be(gameId);
        item.GetProperty("gameName").GetString().Should().Be("Catan");
    }

    [Fact]
    public async Task PopularAgents_RespectsLimitParameter()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        await TestSessionHelper.SeedAgentDefinitionsAsync(dbContext, activeCount: 5, inactiveCount: 0);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/agents/popular?limit=2",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  GET /api/v1/kb-docs/recent (PR #732 §4.3.5)
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RecentKbDocs_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/v1/kb-docs/recent?limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RecentKbDocs_WithEmptyCatalog_Returns200WithEmptyItems()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs/recent?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task RecentKbDocs_OnlyReturnsReadyDocs()
    {
        // PR #732 §4.3.5 spec — exclude in-flight ingests
        // (ProcessingState != Ready: Pending, Uploading, Extracting, Failed, etc.)
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "ready.pdf",
            processingState: "Ready",
            processedAt: DateTime.UtcNow.AddHours(-1));
        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "extracting.pdf",
            processingState: "Extracting",
            processedAt: null);
        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "failed.pdf",
            processingState: "Failed",
            processedAt: null);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs/recent?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(1);
        items[0].GetProperty("title").GetString().Should().Be("ready");
    }

    [Fact]
    public async Task RecentKbDocs_SortedByLastIngestedAtDescending()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "oldest.pdf",
            processingState: "Ready",
            processedAt: DateTime.UtcNow.AddDays(-7));
        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "newest.pdf",
            processingState: "Ready",
            processedAt: DateTime.UtcNow.AddHours(-1));
        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "middle.pdf",
            processingState: "Ready",
            processedAt: DateTime.UtcNow.AddDays(-2));

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs/recent?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        items.GetArrayLength().Should().Be(3);
        items[0].GetProperty("title").GetString().Should().Be("newest");
        items[1].GetProperty("title").GetString().Should().Be("middle");
        items[2].GetProperty("title").GetString().Should().Be("oldest");
    }

    [Fact]
    public async Task RecentKbDocs_MapsDocumentCategoryToWireDocType()
    {
        // Rulebook → "rulebook", Errata → "errata", anything else → "guide"
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "rules.pdf",
            processingState: "Ready",
            processedAt: DateTime.UtcNow.AddMinutes(-10),
            documentCategory: "Rulebook");
        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "errata.pdf",
            processingState: "Ready",
            processedAt: DateTime.UtcNow.AddMinutes(-5),
            documentCategory: "Errata");
        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "expansion.pdf",
            processingState: "Ready",
            processedAt: DateTime.UtcNow.AddMinutes(-1),
            documentCategory: "Expansion");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs/recent?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var items = body.GetProperty("items");
        // Newest first: expansion → errata → rules.
        items[0].GetProperty("docType").GetString().Should().Be("guide");
        items[1].GetProperty("docType").GetString().Should().Be("errata");
        items[2].GetProperty("docType").GetString().Should().Be("rulebook");
    }

    [Fact]
    public async Task RecentKbDocs_StripsTrailingPdfFromTitle()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "Catan Rulebook.pdf",
            processingState: "Ready",
            processedAt: DateTime.UtcNow.AddMinutes(-1));

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs/recent?limit=1",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items")[0].GetProperty("title").GetString().Should().Be("Catan Rulebook");
    }

    [Fact]
    public async Task RecentKbDocs_RespectsLimitParameter()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        for (var i = 0; i < 5; i++)
        {
            await SeedPdfDocumentAsync(
                dbContext,
                userId,
                fileName: $"doc-{i}.pdf",
                processingState: "Ready",
                processedAt: DateTime.UtcNow.AddMinutes(-i));
        }

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs/recent?limit=2",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task RecentKbDocs_PopulatesGameNameAndChunkCount()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(dbContext, "Wingspan");
        var pdfId = await SeedPdfDocumentAsync(
            dbContext,
            userId,
            fileName: "wingspan-rules.pdf",
            processingState: "Ready",
            processedAt: DateTime.UtcNow.AddMinutes(-10),
            sharedGameId: gameId);

        // VectorDocumentEntity drives the chunkCount projection.
        dbContext.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            ChunkCount = 47,
            IndexingStatus = "Completed",
            IndexedAt = DateTime.UtcNow.AddMinutes(-5),
            SharedGameId = gameId
        });
        await dbContext.SaveChangesAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            "/api/v1/kb-docs/recent?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = body.GetProperty("items")[0];
        item.GetProperty("gameId").GetGuid().Should().Be(gameId);
        item.GetProperty("gameName").GetString().Should().Be("Wingspan");
        item.GetProperty("chunkCount").GetInt32().Should().Be(47);
    }

    // ──────────────────────────────────────────────────────────────────────
    //  Test helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Test-scope SharedGame seeder that lets the caller specify <c>CreatedAt</c>
    /// (the canonical <see cref="TestSessionHelper.SeedSharedGameAsync"/> hardcodes
    /// <c>DateTime.UtcNow</c>, which is unsuitable for sort-order assertions).
    /// </summary>
    private static async Task<Guid> SeedSharedGameAsync(
        MeepleAiDbContext dbContext,
        string title,
        DateTime createdAt,
        int yearPublished = 0,
        string? imageUrl = null,
        bool isDeleted = false)
    {
        var id = Guid.NewGuid();
        dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = id,
            Title = title,
            Description = "Integration test game",
            ImageUrl = imageUrl ?? string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = yearPublished,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = createdAt,
            IsDeleted = isDeleted
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return id;
    }

    private static async Task<Guid> SeedPdfDocumentAsync(
        MeepleAiDbContext dbContext,
        Guid uploadedByUserId,
        string fileName,
        string processingState,
        DateTime? processedAt,
        string documentCategory = "Rulebook",
        Guid? sharedGameId = null)
    {
        var id = Guid.NewGuid();
        dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = id,
            FileName = fileName,
            FilePath = $"/uploads/{id}.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = (processedAt ?? DateTime.UtcNow).AddMinutes(-1),
            ProcessingState = processingState,
            ProcessedAt = processedAt,
            DocumentCategory = documentCategory,
            Language = "en",
            IsActiveForRag = true
        });
        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();
        return id;
    }
}
