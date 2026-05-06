using System.Net;
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
        // Arrange — seed a doc with 3 chained chunks
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // Create a real user first — PdfDocument.UploadedByUserId has a FK constraint to users
        var (userId, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
        var docId = await SeedDocWithNestedChunksAsync(dbContext, uploadedByUserId: userId);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/kb-docs/{docId}/chunks?skip=0&take=10",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var chunks = doc.RootElement.GetProperty("chunks");
        chunks.GetArrayLength().Should().Be(3);

        // Leaf is the chunk at position (ChunkIndex) 2
        var leaf = chunks.EnumerateArray()
            .Single(c => c.GetProperty("position").GetInt32() == 2);

        var headingPath = leaf.GetProperty("headingPath")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();

        headingPath.Should().BeEquivalentTo(
            new[] { "Setup", "Players", "Distribution" },
            options => options.WithStrictOrdering());
    }

    /// <summary>
    /// Verifies the endpoint returns 400 Bad Request when take exceeds the maximum of 100.
    /// The take validation happens before the document lookup, so no seed is required.
    /// An unauthenticated client will get 401 before the handler runs; use an authenticated
    /// request so the validation code path is exercised.
    /// </summary>
    [Fact]
    public async Task GetKbChunks_TakeExceedsLimit_Returns400()
    {
        // Arrange — create an authenticated session so we reach the validation code
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get,
            $"/api/v1/kb-docs/{Guid.NewGuid()}/chunks?take=500",
            sessionToken);

        // Act
        var response = await _client.SendAsync(request);

        // Assert — 400 because take=500 > 100 (validation fires before document lookup)
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
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
}
