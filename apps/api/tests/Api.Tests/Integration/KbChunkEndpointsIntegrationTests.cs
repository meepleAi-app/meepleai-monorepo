using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration;

/// <summary>
/// Integration tests for KB chunk/document endpoints (Issue #730).
/// Tests: GET /api/v1/kb-docs/{id} (G4 single doc metadata),
///        GET /api/v1/kb-docs/{id}/chunks (G1 chunk list + heading paths).
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class KbChunkEndpointsIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    public KbChunkEndpointsIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"kb_chunk_endpoints_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<Api.Infrastructure.MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        _client = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ========================================
    // GET /api/v1/kb-docs/{id}  (G4)
    // ========================================

    [Fact]
    public async Task GetKbDocument_WhenNotAuthenticated_Returns401()
    {
        // A fresh client without a session cookie hitting a RequireSession() endpoint
        // should be rejected before the handler even attempts to find the document.
        var response = await _client.GetAsync($"/api/v1/kb-docs/{Guid.NewGuid()}");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ========================================
    // GET /api/v1/kb-docs/{id}/chunks  (G1)
    // ========================================

    /// <summary>
    /// Verifies that the recursive CTE in <c>GetKbChunksHandler.LoadHeadingPathsAsync</c>
    /// correctly walks the parent chain and returns headings ordered from root to leaf.
    ///
    /// Seed hierarchy (ChunkIndex order):
    ///   chunk0 (level 0, heading "Setup",        no parent)
    ///   chunk1 (level 1, heading "Players",      parent = chunk0)
    ///   chunk2 (level 2, heading "Distribution", parent = chunk1)
    ///
    /// Expected headingPath for leaf (chunk2): ["Setup", "Players", "Distribution"]
    /// </summary>
    /// <remarks>
    /// Skipped until Task 8 wires the G1 endpoint (GET /api/v1/kb-docs/{id}/chunks).
    /// Without the route registration the request returns 404 before reaching the handler.
    /// Remove the Skip once the endpoint is registered.
    /// </remarks>
    [Fact]
    public async Task GetKbChunks_NestedHeadings_ReturnsHeadingPath()
    {
        // Wave 3 Phase 3: spec §6.3.2 — `items` envelope, cursor pagination
        // (no skip/take query params), nextCursor null on last page.
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var docId = await SeedDocWithNestedChunksAsync(dbContext, uploadedByUserId: userId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/kb-docs/{docId}/chunks?limit=10",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("items");
        items.GetArrayLength().Should().Be(3);

        // Spec §6.3.2: `nextCursor` is null on last page.
        doc.RootElement.GetProperty("nextCursor").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        var leaf = items.EnumerateArray()
            .Single(c => c.GetProperty("position").GetInt32() == 2);

        var headingPath = leaf.GetProperty("headingPath")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();

        headingPath.Should().BeEquivalentTo(
            new[] { "Setup", "Players", "Distribution" },
            options => options.WithStrictOrdering());

        // VectorId always present for all viewers (spec §6.3.2 de-gating).
        leaf.GetProperty("vectorId").GetString().Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Wave 3 Phase 3: spec §6.3.2 — limit validation 1..100 fires before doc lookup.
    /// </summary>
    [Fact]
    public async Task GetKbChunks_LimitExceedsMaximum_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/kb-docs/{Guid.NewGuid()}/chunks?limit=500",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Wave 3 Phase 3: spec §6.3.2 — malformed cursor → 400 Bad Request.
    /// </summary>
    [Fact]
    public async Task GetKbChunks_MalformedCursor_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/kb-docs/{Guid.NewGuid()}/chunks?cursor=not-base64!",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Wave 3 Phase 3: spec §6.3.1 — 423 Locked when processingStatus != 'ready'.
    /// </summary>
    [Fact]
    public async Task GetKbDocument_NotReady_Returns423Locked()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var docId = Guid.NewGuid();
        dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = $"in-progress-{docId:N}.pdf",
            ProcessingState = "Embedding", // not Ready
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = userId,
            FilePath = $"/tmp/inprog-{docId:N}.pdf",
            IsPublic = true
        });
        await dbContext.SaveChangesAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/kb-docs/{docId}",
            sessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
    }

    // ========================================
    // GET /api/v1/kb-docs/{id}/chunks/{chunkId}  (G2)
    // ========================================

    /// <summary>
    /// Verifies that requesting a chunk from a different document returns 404,
    /// preventing cross-document chunk enumeration leakage.
    /// The unit test <c>Handle_ChunkBelongsToOtherDoc_ThrowsNotFound</c> already
    /// covers the handler logic; this integration test validates the full HTTP stack.
    /// </summary>
    [Fact]
    public async Task GetKbChunk_ChunkBelongsToOtherDoc_Returns404()
    {
        // Arrange — seed two separate documents, each with one chunk
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var (docAId, _) = await SeedDocWithSingleChunkAsync(dbContext, userId);
        var (_, chunkBId) = await SeedDocWithSingleChunkAsync(dbContext, userId);

        // Request chunk from docB while specifying docA in the URL
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/kb-docs/{docAId}/chunks/{chunkBId}",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ========================================
    // Access control regression (Critical 1)
    // ========================================

    /// <summary>
    /// Verifies that a private document owned by another user returns 403 Forbidden
    /// when accessed by a different authenticated (non-admin) user.
    /// This is the regression test for Issue #730 final review Critical 1.
    /// </summary>
    [Fact]
    public async Task GetKbDocument_PrivateDocOwnedByOtherUser_Returns403()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Seed user A (owner) and a private doc owned by user A
        var (ownerUserId, _) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (docId, _) = await SeedPrivateDocWithSingleChunkAsync(dbContext, ownerUserId);

        // Authenticate as user B (not the owner, not admin)
        var (_, userBSessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/kb-docs/{docId}",
            userBSessionToken);

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ─── Seed helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a PdfDocumentEntity with a single TextChunkEntity.
    /// Returns (docId, chunkId) for use in G2 tests.
    /// </summary>
    private static async Task<(Guid DocId, Guid ChunkId)> SeedDocWithSingleChunkAsync(
        MeepleAiDbContext dbContext,
        Guid uploadedByUserId)
    {
        var docId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();

        dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = $"single-chunk-{docId:N}.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = uploadedByUserId,
            FilePath = $"/tmp/single-{docId:N}.pdf"
        });

        dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = chunkId,
            PdfDocumentId = docId,
            Content = "Only chunk content",
            ChunkIndex = 0,
            PageNumber = 1,
            CharacterCount = 18,
            ElementType = "NarrativeText",
            Level = 1
        });

        await dbContext.SaveChangesAsync();
        return (docId, chunkId);
    }

    /// <summary>
    /// Seeds a PdfDocumentEntity with 3 chained TextChunkEntities that form the
    /// hierarchy: "Setup" (root) → "Players" (child) → "Distribution" (leaf).
    /// </summary>
    /// <param name="dbContext">Test database context.</param>
    /// <param name="uploadedByUserId">
    /// The ID of a user that already exists in the database.
    /// PdfDocument.UploadedByUserId has a FK constraint — a random Guid will fail.
    /// </param>
    private static async Task<Guid> SeedDocWithNestedChunksAsync(
        MeepleAiDbContext dbContext,
        Guid uploadedByUserId)
    {
        var docId = Guid.NewGuid();

        dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = "nested-headings-test.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = uploadedByUserId,
            FilePath = "/tmp/nested-headings-test.pdf"
        });

        var chunk0Id = Guid.NewGuid();
        var chunk1Id = Guid.NewGuid();
        var chunk2Id = Guid.NewGuid();

        dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = chunk0Id,
            PdfDocumentId = docId,
            Content = "Setup content",
            ChunkIndex = 0,
            PageNumber = 1,
            CharacterCount = 13,
            ElementType = "Title",
            Level = 0,
            Heading = "Setup",
            ParentChunkId = null
        });

        dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = chunk1Id,
            PdfDocumentId = docId,
            Content = "Players content",
            ChunkIndex = 1,
            PageNumber = 1,
            CharacterCount = 15,
            ElementType = "Title",
            Level = 1,
            Heading = "Players",
            ParentChunkId = chunk0Id
        });

        dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = chunk2Id,
            PdfDocumentId = docId,
            Content = "Distribution content",
            ChunkIndex = 2,
            PageNumber = 2,
            CharacterCount = 20,
            ElementType = "NarrativeText",
            Level = 2,
            Heading = "Distribution",
            ParentChunkId = chunk1Id
        });

        await dbContext.SaveChangesAsync();
        return docId;
    }

    // ========================================
    // POST /api/v1/kb-docs/{id}/chunks/search  (G3)
    // ========================================

    /// <summary>
    /// Verifies that chunks containing the keyword are returned in descending rank order
    /// and that the snippet contains the expected &lt;mark&gt; highlight tags.
    ///
    /// The seed helper manually populates search_vector via to_tsvector('simple', ...) because
    /// the tsvector trigger is not installed in the Testcontainers-migrated database.
    /// </summary>
    [Fact]
    public async Task SearchChunks_KeywordPresent_ReturnsRankedMatches()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (docId, _) = await SeedDocWithKeywordChunksAsync(dbContext, userId, "initiative", new[] { 3, 0, 2, 1 });

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/kb-docs/{docId}/chunks/search",
            sessionToken,
            new { query = "initiative", skip = 0, take = 20 });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<KbChunkSearchResultDto>();
        result.Should().NotBeNull();
        // 3 chunks contain the keyword (occurrences 3, 2, 1 — the 0-occurrence chunk is excluded)
        result!.TotalCount.Should().Be(3);
        result.Matches.Should().HaveCount(3);
        // Results are in descending rank order
        result.Matches.Should().BeInDescendingOrder(m => m.Rank);
        // The highest-ranked snippet should contain a <mark> tag
        result.Matches.First().Snippet.Should().Contain("<mark>");
    }

    /// <summary>
    /// Verifies that a search with no matching keyword returns an empty result with TotalCount=0.
    /// </summary>
    [Fact]
    public async Task SearchChunks_NoMatches_ReturnsEmpty()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (docId, _) = await SeedDocWithSingleChunkAsync(dbContext, userId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/kb-docs/{docId}/chunks/search",
            sessionToken,
            new { query = "xyzzyx", skip = 0, take = 20 });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<KbChunkSearchResultDto>();
        result.Should().NotBeNull();
        result!.TotalCount.Should().Be(0);
        result.Matches.Should().BeEmpty();
    }

    /// <summary>
    /// Verifies that a query longer than 200 characters is rejected with 400 Bad Request
    /// by the endpoint body validation (before the handler is reached).
    /// </summary>
    [Fact]
    public async Task SearchChunks_QueryTooLong_Returns400()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var longQuery = new string('a', 250);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/kb-docs/{Guid.NewGuid()}/chunks/search",
            sessionToken,
            new { query = longQuery, skip = 0, take = 20 });

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Verifies that a query containing the tsquery pipe operator '|' is treated as literal text
    /// (plainto_tsquery sanitises the input — no "syntax error in tsquery" exception).
    /// The test documents contain "initiative" so a match should still be found.
    /// </summary>
    [Fact]
    public async Task SearchChunks_OperatorInjection_TreatedAsLiteral()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var (docId, _) = await SeedDocWithKeywordChunksAsync(dbContext, userId, "initiative", new[] { 1 });

        // Submit a query that contains the tsquery '|' operator.
        // If plainto_tsquery is NOT used this would throw "syntax error in tsquery".
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post,
            $"/api/v1/kb-docs/{docId}/chunks/search",
            sessionToken,
            new { query = "initiative | drop", skip = 0, take = 20 });

        var response = await _client.SendAsync(request);

        // plainto_tsquery treats '|' as a literal character (part of the lexeme), not a tsquery operator.
        // The response must NOT be 500 (no "syntax error in tsquery").
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ─── G3 seed helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a document with N text chunks, where chunk[i].Content contains the keyword
    /// repeated <paramref name="occurrencesPerChunk"/>[i] times.
    /// After inserting, manually populates <c>search_vector</c> using
    /// <c>to_tsvector('simple', ...)</c> (the trigger is not installed in test DBs).
    /// Chunks with 0 occurrences still have a valid search_vector but no match for the keyword.
    /// </summary>
    /// <returns>(docId, firstChunkId)</returns>
    private static async Task<(Guid DocId, Guid FirstChunkId)> SeedDocWithKeywordChunksAsync(
        MeepleAiDbContext dbContext,
        Guid uploadedByUserId,
        string keyword,
        int[] occurrencesPerChunk)
    {
        var docId = Guid.NewGuid();

        dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = $"fts-test-{docId:N}.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = uploadedByUserId,
            FilePath = $"/tmp/fts-{docId:N}.pdf"
        });

        var firstChunkId = Guid.Empty;

        for (var i = 0; i < occurrencesPerChunk.Length; i++)
        {
            var chunkId = Guid.NewGuid();
            if (i == 0) firstChunkId = chunkId;

            // Build content: repeat keyword N times, padded with filler text so the lexer has tokens
            var keywordPart = occurrencesPerChunk[i] > 0
                ? string.Join(" ", Enumerable.Repeat(keyword, occurrencesPerChunk[i]))
                : "filler text without any matching content";

            dbContext.TextChunks.Add(new TextChunkEntity
            {
                Id = chunkId,
                PdfDocumentId = docId,
                Content = $"Chunk {i}: {keywordPart} some additional context here.",
                ChunkIndex = i,
                PageNumber = i + 1,
                CharacterCount = 50,
                ElementType = "NarrativeText",
                Level = 1
            });
        }

        await dbContext.SaveChangesAsync();
        // search_vector is a GENERATED ALWAYS AS (to_tsvector('simple', "Content")) STORED column
        // added by InitializeAsync, so it is automatically populated on INSERT.

        return (docId, firstChunkId);
    }

    /// <summary>
    /// Seeds a private (IsPublic=false) PdfDocumentEntity owned by <paramref name="ownedByUserId"/>
    /// with a single TextChunkEntity. Used by the Critical 1 regression test.
    /// </summary>
    private static async Task<(Guid DocId, Guid ChunkId)> SeedPrivateDocWithSingleChunkAsync(
        MeepleAiDbContext dbContext,
        Guid ownedByUserId)
    {
        var docId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();

        dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = $"private-{docId:N}.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = ownedByUserId,
            FilePath = $"/tmp/private-{docId:N}.pdf",
            IsPublic = false
        });

        dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = chunkId,
            PdfDocumentId = docId,
            Content = "Private document content",
            ChunkIndex = 0,
            PageNumber = 1,
            CharacterCount = 24,
            ElementType = "NarrativeText",
            Level = 1
        });

        await dbContext.SaveChangesAsync();
        return (docId, chunkId);
    }
}
