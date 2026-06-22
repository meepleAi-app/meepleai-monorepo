using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.3) — spec-conformant rewrite tests
/// for <see cref="GetKbChunkByIdHandler"/>. Validates Id rename, DocId addition,
/// prev/nextChunkId derivation, markdown sanitization plumbing, Metadata Gate B
/// stub, access control, and 404 cross-document leak prevention.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetKbChunkByIdHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbChunkByIdHandler> _logger;
    private readonly IHybridCacheService _cache;
    private readonly GetKbChunkByIdHandler _handler;

    public GetKbChunkByIdHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetKbChunkByIdHandler>>();
        _cache = new TestHybridCacheService();
        _handler = new GetKbChunkByIdHandler(_dbContext, _cache, _logger);
    }

    [Fact]
    public async Task Handle_MiddleChunk_ReturnsContentWithPrevNext()
    {
        var docId = Guid.NewGuid();
        var (chunk0, chunk1, chunk2) = await SeedThreeChunksAsync(docId);

        var query = new GetKbChunkByIdQuery(docId, RequestingUserId: Guid.NewGuid(), ChunkId: chunk1, UserIsAdmin: false);
        var dto = await _handler.Handle(query, CancellationToken.None);

        dto.Id.Should().Be(chunk1);
        dto.DocId.Should().Be(docId);
        dto.PrevChunkId.Should().Be(chunk0);
        dto.NextChunkId.Should().Be(chunk2);
        dto.Metadata.Should().BeEmpty(); // Gate B v1 carryover
    }

    [Fact]
    public async Task Handle_FirstChunk_PrevIsNull()
    {
        var docId = Guid.NewGuid();
        var (chunk0, _, _) = await SeedThreeChunksAsync(docId);

        var dto = await _handler.Handle(
            new GetKbChunkByIdQuery(docId, RequestingUserId: Guid.NewGuid(), ChunkId: chunk0, UserIsAdmin: false),
            CancellationToken.None);

        dto.PrevChunkId.Should().BeNull();
        dto.NextChunkId.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_LastChunk_NextIsNull()
    {
        var docId = Guid.NewGuid();
        var (_, _, chunk2) = await SeedThreeChunksAsync(docId);

        var dto = await _handler.Handle(
            new GetKbChunkByIdQuery(docId, RequestingUserId: Guid.NewGuid(), ChunkId: chunk2, UserIsAdmin: false),
            CancellationToken.None);

        dto.NextChunkId.Should().BeNull();
        dto.PrevChunkId.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ChunkBelongsToOtherDoc_Throws404()
    {
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        await SeedThreeChunksAsync(docA);
        var (chunkB, _, _) = await SeedThreeChunksAsync(docB);

        var query = new GetKbChunkByIdQuery(docA, RequestingUserId: Guid.NewGuid(), ChunkId: chunkB, UserIsAdmin: false);

        await _handler.Invoking(h => h.Handle(query, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NonExistentChunk_Throws404()
    {
        var docId = Guid.NewGuid();
        await SeedThreeChunksAsync(docId);

        var query = new GetKbChunkByIdQuery(docId, RequestingUserId: Guid.NewGuid(), ChunkId: Guid.NewGuid(), UserIsAdmin: false);

        await _handler.Invoking(h => h.Handle(query, CancellationToken.None))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_PrivateDocOtherUser_Throws403()
    {
        var docId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = "private.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = ownerId,
            FilePath = "/tmp/private.pdf",
            IsPublic = false
        });
        var chunkId = Guid.NewGuid();
        _dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = chunkId,
            PdfDocumentId = docId,
            Content = "private content",
            ChunkIndex = 0,
            CharacterCount = 15,
            ElementType = "NarrativeText",
            Level = 1
        });
        await _dbContext.SaveChangesAsync();

        var query = new GetKbChunkByIdQuery(docId, RequestingUserId: Guid.NewGuid(), ChunkId: chunkId, UserIsAdmin: false);

        await _handler.Invoking(h => h.Handle(query, CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_MarkdownSanitization_StripsImagesAndDemotesH4()
    {
        var docId = Guid.NewGuid();
        var chunkId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = "doc.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/doc.pdf",
            IsPublic = true
        });
        _dbContext.TextChunks.Add(new TextChunkEntity
        {
            Id = chunkId,
            PdfDocumentId = docId,
            Content = "#### Subheader\n\nPara with ![alt](http://example.com/img.png) image.",
            ChunkIndex = 0,
            CharacterCount = 60,
            ElementType = "NarrativeText",
            Level = 1
        });
        await _dbContext.SaveChangesAsync();

        var dto = await _handler.Handle(
            new GetKbChunkByIdQuery(docId, RequestingUserId: Guid.NewGuid(), ChunkId: chunkId, UserIsAdmin: false),
            CancellationToken.None);

        dto.Content.Should().Contain("**Subheader**"); // H4 demoted
        dto.Content.Should().Contain("[Image: alt]"); // image replaced
        dto.Content.Should().NotContain("![alt]"); // raw image syntax removed
        dto.Content.Should().NotContain("####"); // H4 stripped from prefix
    }

    private async Task<(Guid chunk0, Guid chunk1, Guid chunk2)> SeedThreeChunksAsync(Guid docId)
    {
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = $"doc-{docId}.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/doc.pdf",
            IsPublic = true
        });

        var chunk0 = Guid.NewGuid();
        var chunk1 = Guid.NewGuid();
        var chunk2 = Guid.NewGuid();
        _dbContext.TextChunks.AddRange(
            new TextChunkEntity
            {
                Id = chunk0,
                PdfDocumentId = docId,
                Content = "first content",
                ChunkIndex = 0,
                CharacterCount = 13,
                ElementType = "NarrativeText",
                Level = 1
            },
            new TextChunkEntity
            {
                Id = chunk1,
                PdfDocumentId = docId,
                Content = "middle content",
                ChunkIndex = 1,
                CharacterCount = 14,
                ElementType = "NarrativeText",
                Level = 1
            },
            new TextChunkEntity
            {
                Id = chunk2,
                PdfDocumentId = docId,
                Content = "last content",
                ChunkIndex = 2,
                CharacterCount = 12,
                ElementType = "NarrativeText",
                Level = 1
            }
        );
        await _dbContext.SaveChangesAsync();
        return (chunk0, chunk1, chunk2);
    }
}
