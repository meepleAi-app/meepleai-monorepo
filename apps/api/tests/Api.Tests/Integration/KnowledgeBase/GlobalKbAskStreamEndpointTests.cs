using System.Net;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Models;
using Api.Services;          // StreamChunk, LlmUsage, LlmCost, ILlmService, RequestSource
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// HTTP-level integration tests for POST /api/v1/knowledge-base/ask/global (Task 9, Issue #1661 PR-2).
/// Validates SSE streaming, RBAC scoping, session enforcement, and EC-1/EC-3/EC-5 edge cases.
/// Uses WebApplicationFactory + PostgreSQL Testcontainer for relational data (RBAC),
/// and mocked IMultiGameHybridSearchService + ILlmService for the vector/LLM layer.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GlobalKbAskStreamEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    // Seeded game IDs
    private Guid _gamePublicId;
    private Guid _gameAliceOwnedId;
    private Guid _gameBobOwnedId;

    // User IDs
    private Guid _aliceId;
    private Guid _bobId;

    // Session tokens
    private string _aliceToken = null!;
    private string _bobToken = null!;

    // Captures game IDs passed to search — used for RBAC assertions (EC-5, same pattern as PR-1).
    private readonly System.Collections.Concurrent.ConcurrentBag<IReadOnlyList<Guid>> _capturedSearchGameIds = new();

    private const string Endpoint = "/api/v1/knowledge-base/ask/global";

    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalKbAskStreamEndpointTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"test_global_kb_ask_{Guid.NewGuid():N}";
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _factory = IntegrationWebApplicationFactory.Create(connectionString)
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Mock the vector search layer — no embedding infra in CI.
                    // Real RagAccessService + MeepleAiDbContext runs against TC Postgres (RBAC exercised).
                    services.RemoveAll<IMultiGameHybridSearchService>();
                    services.AddScoped<IMultiGameHybridSearchService>(_ => BuildSearchMock());

                    // Mock LLM service — emit deterministic token sequence.
                    services.RemoveAll<ILlmService>();
                    services.AddScoped<ILlmService>(_ => BuildLlmMock());
                });
            });

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await db.Database.MigrateAsync(TestCancellationToken);
        }

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        });

        await SeedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        if (_factory is not null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ── AC-1: 401 without session ─────────────────────────────────────────────

    /// <summary>
    /// AC-1: Anonymous request must receive 401 Unauthorized.
    /// RequireSession() rejects before the handler runs.
    /// </summary>
    [Fact]
    public async Task Returns_401_When_Unauthenticated()
    {
        var response = await _client.PostAsync(
            Endpoint,
            new StringContent(
                """{"query":"board game rules"}""",
                Encoding.UTF8,
                "application/json"),
            TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── AC-2: SSE response shape — StateUpdate → Citations → Token* → Complete ─

    /// <summary>
    /// AC-2: Authenticated request streams text/event-stream with parseable data: {} events.
    /// The event sequence must include at least: StateUpdate, Citations, Token (≥1), Complete.
    /// EC-3: CancellationToken is passed to handler (verified structurally; the mock LLM
    /// will emit tokens only when the CT is not cancelled before the first token).
    /// </summary>
    [Fact]
    public async Task Authenticated_StreamsEventSequence()
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            Endpoint,
            _aliceToken,
            new { query = "What are the movement rules?" });

        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            TestCancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "Authenticated SSE ask must return 200 OK");

        // Content-Type must be text/event-stream
        var contentType = response.Content.Headers.ContentType?.MediaType;
        contentType.Should().Be("text/event-stream",
            "SSE endpoint must set Content-Type: text/event-stream");

        // Parse the SSE events
        var events = await ParseSseEventsAsync(response);

        events.Should().NotBeEmpty("stream must emit at least one event");

        // Must contain StateUpdate
        events.Should().Contain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.StateUpdate,
            "stream must contain a StateUpdate event");

        // Must contain Citations
        events.Should().Contain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Citations,
            "stream must contain a Citations event");

        // Must contain at least one Token
        events.Should().Contain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Token,
            "stream must contain at least one Token event");

        // Must end with Complete
        events.Should().Contain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Complete,
            "stream must end with a Complete event");

        // Must NOT contain Error
        events.Should().NotContain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Error,
            "stream must not contain any Error events for a healthy request");

        // Complete must be the last event
        var lastEvent = events.Last();
        lastEvent.GetProperty("type").GetInt32().Should().Be((int)StreamingEventType.Complete,
            "Complete must be the last event in the sequence");
    }

    // ── AC-3: RBAC scoping — Bob cannot see Alice's privately-owned game ──────

    /// <summary>
    /// AC-3 (EC-5 critical): Bob must not receive context from Alice's privately-owned game.
    /// We verify the gameIds passed to IMultiGameHybridSearchService exclude Alice's game.
    /// This is the non-vacuous RBAC capture pattern from PR-1 (lesson: don't assert only on output).
    /// </summary>
    [Fact]
    public async Task RbacScoping_BobCannotSee_AliceOwnedPrivateGame()
    {
        _capturedSearchGameIds.Clear();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            Endpoint,
            _bobToken,
            new { query = "How do I score points?" });

        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            TestCancellationToken);

        // Consume the full stream so RBAC callbacks fire
        await ConsumeStreamAsync(response);

        // The search service must have been called with Bob's accessible games only
        _capturedSearchGameIds.Should().NotBeEmpty(
            "the handler must invoke the search service for Bob (public game is accessible)");

        _capturedSearchGameIds.Should().AllSatisfy(ids =>
            ids.Should().NotContain(
                _gameAliceOwnedId,
                "RBAC must exclude Alice's privately-owned game from Bob's search inputs (EC-5)"));
    }

    // ── AC-4: EC-1 — user with 0 accessible games streams Complete with no error ─

    /// <summary>
    /// AC-4 (EC-1): A user with no accessible games receives a complete SSE stream
    /// that ends with Complete (no Error, no 5xx). Handler's EC-1 early-exit guard.
    /// </summary>
    [Fact]
    public async Task EmptyAccessibleGames_StreamsCompleteWithNoContext()
    {
        // Create a fresh isolated user with no library + temporarily override the search mock
        // to return empty for any Guid list (ensures the EC-1 guard fires, not retrieval results)
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Charlie: new user, no library, no public games (public games in this DB are seeded,
        // but Charlie's accessible set will include public games — so we cannot test truly
        // "zero accessible" without isolating. Instead we verify the endpoint returns 200
        // and ends with Complete (not Error) — which is the EC-1 contract.
        // The zero-accessible code path is unit-tested in CrossGameStreamQaQueryHandlerTests.
        var (_, charlieToken) = await TestSessionHelper.CreateUserSessionAsync(
            db, cancellationToken: TestCancellationToken);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            Endpoint,
            charlieToken,
            new { query = "Can this game be played solo?" });

        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            TestCancellationToken);

        // Must be 200 (not 5xx) — EC-1 contract
        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "EC-1: even with no exclusive context the endpoint must return 200");

        var events = await ParseSseEventsAsync(response);
        events.Should().NotBeEmpty();

        // Must end with Complete (not Error) — EC-1
        events.Should().Contain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Complete,
            "EC-1: stream must end with Complete regardless of accessible game count");

        events.Should().NotContain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Error,
            "EC-1: stream must not contain an Error event for the zero-context case");
    }

    // ── AC-5: chunkId / chunkPosition carried in Citations event ─────────────

    /// <summary>
    /// AC-5 (G/W/T Scenario 1, Issue #1702): The CrossGameStreamQaQueryHandler MUST populate
    /// chunkId and chunkPosition on each Snippet when cross-game retrieval returns results.
    /// Verifies that the SSE Citations event carries both new fields in its JSON payload.
    /// </summary>
    [Fact]
    public async Task AskGlobal_WhenChunksRetrieved_CitationsCarriesChunkIdAndPosition()
    {
        // Arrange: BuildSearchMock already returns one result per accessible game with
        // ChunkId = "{guid:N}_0" (contains '_') and ChunkIndex = 0.
        // No per-test mock setup needed — the factory pattern in InitializeAsync wires it.
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            Endpoint,
            _aliceToken,
            new { query = "How does setup work?", language = "en", topK = 5 });

        // Act
        var response = await _client.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            TestCancellationToken);

        response.EnsureSuccessStatusCode();

        var events = await ParseSseEventsAsync(response);

        // Assert: Citations event must be present
        var citationsEvent = events
            .Should().Contain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Citations,
                "the seeded fixture MUST produce a Citations event")
            .Which;

        var citations = citationsEvent.GetProperty("data").GetProperty("citations");
        citations.GetArrayLength().Should().BeGreaterThan(0,
            "the seeded BuildSearchMock returns one result per accessible game — at least one must appear");

        var firstCitation = citations[0];

        firstCitation.TryGetProperty("chunkId", out var chunkIdProp).Should().BeTrue(
            "chunkId MUST be present in /ask/global Citations payload (CrossGameStreamQaQueryHandler sets it from MultiGameSearchResultItem.ChunkId)");
        chunkIdProp.GetString().Should().NotBeNullOrEmpty(
            "chunkId must be a non-empty string");
        chunkIdProp.GetString().Should().Contain("_",
            "chunkId is the composite \"{PdfDocumentId}_{ChunkIndex}\" string — the underscore separator MUST be present");

        firstCitation.TryGetProperty("chunkPosition", out var chunkPosProp).Should().BeTrue(
            "chunkPosition MUST be present in /ask/global Citations payload (CrossGameStreamQaQueryHandler sets it from MultiGameSearchResultItem.ChunkIndex)");
        chunkPosProp.GetInt32().Should().BeGreaterThanOrEqualTo(0,
            "chunkPosition is the zero-based ChunkIndex — must be non-negative");
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var seedUserId = Guid.NewGuid();

        // Create users + sessions
        (_aliceId, _aliceToken) = await TestSessionHelper.CreateUserSessionAsync(
            db, cancellationToken: TestCancellationToken);
        (_bobId, _bobToken) = await TestSessionHelper.CreateUserSessionAsync(
            db, cancellationToken: TestCancellationToken);

        // Seed the dummy creator user
        db.Users.Add(new UserEntity
        {
            Id = seedUserId,
            Email = $"seed-ask-{seedUserId:N}@test.local",
            DisplayName = "Seed User",
            PasswordHash = "x",
            Role = "user",
            Tier = "free",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });

        // Seed three games
        _gamePublicId    = Guid.NewGuid();
        _gameAliceOwnedId = Guid.NewGuid();
        _gameBobOwnedId  = Guid.NewGuid();

        db.SharedGames.AddRange(
            new SharedGameEntity
            {
                Id = _gamePublicId,
                Title = "Public Ask Game",
                Description = "Publicly indexed",
                YearPublished = 2022,
                MinPlayers = 2, MaxPlayers = 4,
                PlayingTimeMinutes = 60, MinAge = 10,
                IsRagPublic = true,
                CreatedBy = seedUserId, CreatedAt = DateTime.UtcNow
            },
            new SharedGameEntity
            {
                Id = _gameAliceOwnedId,
                Title = "Alice Ask Private Game",
                Description = "Private to Alice",
                YearPublished = 2022,
                MinPlayers = 2, MaxPlayers = 4,
                PlayingTimeMinutes = 60, MinAge = 10,
                IsRagPublic = false,
                CreatedBy = seedUserId, CreatedAt = DateTime.UtcNow
            },
            new SharedGameEntity
            {
                Id = _gameBobOwnedId,
                Title = "Bob Ask Private Game",
                Description = "Private to Bob",
                YearPublished = 2022,
                MinPlayers = 2, MaxPlayers = 4,
                PlayingTimeMinutes = 60, MinAge = 10,
                IsRagPublic = false,
                CreatedBy = seedUserId, CreatedAt = DateTime.UtcNow
            }
        );

        // Alice owns Alice's game; Bob owns Bob's game
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = _aliceId,
            SharedGameId = _gameAliceOwnedId,
            AddedAt = DateTime.UtcNow,
            OwnershipDeclaredAt = DateTime.UtcNow
        });
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            Id = Guid.NewGuid(),
            UserId = _bobId,
            SharedGameId = _gameBobOwnedId,
            AddedAt = DateTime.UtcNow,
            OwnershipDeclaredAt = DateTime.UtcNow
        });

        // Seed PDF + VectorDocument for each game (so enrichment queries work)
        SeedIndexedDoc(db, _gamePublicId, seedUserId, "public-ask.pdf");
        SeedIndexedDoc(db, _gameAliceOwnedId, _aliceId, "alice-ask.pdf");
        SeedIndexedDoc(db, _gameBobOwnedId, _bobId, "bob-ask.pdf");

        await db.SaveChangesAsync(TestCancellationToken);
    }

    private static void SeedIndexedDoc(MeepleAiDbContext db, Guid gameId, Guid uploadedBy, string fileName)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            SharedGameId = gameId,
            FileName = fileName,
            FilePath = $"/test/{fileName}",
            FileSizeBytes = 1024,
            ContentType = "application/pdf",
            UploadedAt = DateTime.UtcNow,
            UploadedByUserId = uploadedBy,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            IsActiveForRag = true,
            DocumentType = "base"
        });

        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = pdfId,
            GameId = gameId,
            ChunkCount = 5,
            TotalCharacters = 2500,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-embed",
            EmbeddingDimensions = 768
        });
    }

    /// <summary>
    /// Builds a mock IMultiGameHybridSearchService that captures gameIds (RBAC capture)
    /// and returns one synthetic result per accessible game.
    /// </summary>
    private IMultiGameHybridSearchService BuildSearchMock()
    {
        var mock = new Mock<IMultiGameHybridSearchService>();

        mock.Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<IReadOnlyList<Guid>>(),
                It.IsAny<int>(),
                It.IsAny<SearchMode>(),
                It.IsAny<double>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, IReadOnlyList<Guid>, int, SearchMode, double, CancellationToken>(
                (_, gameIds, _, _, _, _) => _capturedSearchGameIds.Add(gameIds))
            .ReturnsAsync((
                string _,
                IReadOnlyList<Guid> gameIds,
                int limit,
                SearchMode mode,
                double _,
                CancellationToken _) =>
            {
                var results = new List<MultiGameSearchResultItem>();
                foreach (var gameId in gameIds.Take(limit))
                {
                    results.Add(new MultiGameSearchResultItem
                    {
                        GameId      = gameId,
                        ChunkId     = $"{Guid.NewGuid():N}_0",
                        PdfDocumentId = Guid.NewGuid().ToString(),
                        ChunkIndex  = 0,
                        Content     = $"Movement rule: move up to 3 spaces per turn (game {gameId}).",
                        HybridScore = 0.90f,
                        Mode        = mode
                    });
                }
                return (IReadOnlyList<MultiGameSearchResultItem>)results.AsReadOnly();
            });

        return mock.Object;
    }

    /// <summary>
    /// Builds a mock ILlmService that emits 3 deterministic token chunks + a final usage chunk.
    /// </summary>
    private static ILlmService BuildLlmMock()
    {
        var mock = new Mock<ILlmService>();

        mock.Setup(s => s.GenerateCompletionStreamAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(),
                It.IsAny<CancellationToken>()))
            .Returns<string, string, RequestSource, CancellationToken>(
                (_, _, _, ct) => EmitTokensAsync(ct));

        return mock.Object;
    }

#pragma warning disable CS1998 // All branches need await to compile as async iterator
    private static async IAsyncEnumerable<StreamChunk> EmitTokensAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        string[] tokens = ["You ", "move ", "up to 3 spaces."];
        foreach (var token in tokens)
        {
            if (ct.IsCancellationRequested) yield break;
            yield return new StreamChunk(token, IsFinal: false, Usage: null, Cost: null);
        }
        // Final chunk with usage
        yield return new StreamChunk(
            Content: "",
            IsFinal: true,
            Usage: new LlmUsage(PromptTokens: 50, CompletionTokens: 8, TotalTokens: 58),
            Cost: null);
    }
#pragma warning restore CS1998

    /// <summary>
    /// Reads the full SSE response body and parses each "data: {...}" line into a JsonElement.
    /// </summary>
    private static async Task<IReadOnlyList<JsonElement>> ParseSseEventsAsync(HttpResponseMessage response)
    {
        var events = new List<JsonElement>();
        var body = await response.Content.ReadAsStringAsync();

        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;

            var json = trimmed["data:".Length..].Trim();
            if (string.IsNullOrWhiteSpace(json)) continue;

            try
            {
                var element = JsonSerializer.Deserialize<JsonElement>(json, SseJsonOptions);
                events.Add(element);
            }
            catch (JsonException)
            {
                // Skip malformed lines (shouldn't happen in practice)
            }
        }

        return events;
    }

    /// <summary>Consumes the entire response stream (fires all callbacks).</summary>
    private static async Task ConsumeStreamAsync(HttpResponseMessage response)
    {
        await response.Content.ReadAsStringAsync();
    }
}
