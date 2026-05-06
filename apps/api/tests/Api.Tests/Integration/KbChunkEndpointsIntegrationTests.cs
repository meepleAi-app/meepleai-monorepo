using System.Net;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
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
    [Fact(Skip = "Wire GET /api/v1/kb-docs/{id}/chunks endpoint in Task 8 before enabling")]
    public async Task GetKbChunks_NestedHeadings_ReturnsHeadingPath()
    {
        // Arrange — seed a doc with 3 chained chunks
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var docId = await SeedDocWithNestedChunksAsync(dbContext);

        var (_, sessionToken) = await TestSessionHelper.CreateUserSessionAsync(dbContext);
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

    // ─── Seed helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Seeds a PdfDocumentEntity with 3 chained TextChunkEntities that form the
    /// hierarchy: "Setup" (root) → "Players" (child) → "Distribution" (leaf).
    /// </summary>
    private static async Task<Guid> SeedDocWithNestedChunksAsync(MeepleAiDbContext dbContext)
    {
        var docId = Guid.NewGuid();
        var uploadedBy = Guid.NewGuid();

        dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = "nested-headings-test.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = uploadedBy,
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
