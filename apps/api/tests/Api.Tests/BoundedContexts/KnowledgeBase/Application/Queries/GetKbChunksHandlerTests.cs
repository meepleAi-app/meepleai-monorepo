using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;
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
/// Wave 3 Phase 3 (Issue #805 / PR #732 §6.3.2) — spec-conformant rewrite tests
/// for <see cref="GetKbChunksHandler"/>. Replaces the prior offset-pagination
/// suite with cursor-pagination semantics: opaque (Position, Id) base64 cursor,
/// nextCursor null on last page, malformed cursor → caller emits 400.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetKbChunksHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbChunksHandler> _logger;
    private readonly IHybridCacheService _cache;
    private readonly GetKbChunksHandler _handler;

    public GetKbChunksHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetKbChunksHandler>>();
        _cache = new TestHybridCacheService();
        _handler = new GetKbChunksHandler(_dbContext, _cache, _logger);
    }

    [Fact]
    public async Task Handle_FirstPage_ReturnsItemsAndNextCursor()
    {
        var docId = await SeedDocumentWithChunksAsync(120);
        var query = new GetKbChunksQuery(
            docId,
            RequestingUserId: Guid.NewGuid(),
            Cursor: null,
            Limit: 50,
            UserIsAdmin: false);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Items.Should().HaveCount(50);
        result.Items.Select(c => c.Position).Should().BeInAscendingOrder();
        result.TotalCount.Should().Be(120);
        result.NextCursor.Should().NotBeNullOrEmpty();
        result.Items.First().Snippet.Length.Should().BeLessThanOrEqualTo(200);
    }

    [Fact]
    public async Task Handle_LastPage_ReturnsItemsAndNullCursor()
    {
        var docId = await SeedDocumentWithChunksAsync(120);

        // Fetch first page to obtain a cursor.
        var first = await _handler.Handle(
            new GetKbChunksQuery(docId, Guid.NewGuid(), null, 100, UserIsAdmin: false),
            CancellationToken.None);

        first.NextCursor.Should().NotBeNull();
        var firstCursor = KbChunksCursor.Decode(first.NextCursor);

        var second = await _handler.Handle(
            new GetKbChunksQuery(docId, Guid.NewGuid(), firstCursor, 100, UserIsAdmin: false),
            CancellationToken.None);

        second.Items.Should().HaveCount(20);
        second.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task Handle_CursorPagination_AdvancesPositionMonotonically()
    {
        var docId = await SeedDocumentWithChunksAsync(50);

        var firstPage = await _handler.Handle(
            new GetKbChunksQuery(docId, Guid.NewGuid(), null, 20, UserIsAdmin: false),
            CancellationToken.None);

        var lastPositionFirstPage = firstPage.Items.Last().Position;
        var cursor = KbChunksCursor.Decode(firstPage.NextCursor);

        var secondPage = await _handler.Handle(
            new GetKbChunksQuery(docId, Guid.NewGuid(), cursor, 20, UserIsAdmin: false),
            CancellationToken.None);

        secondPage.Items.First().Position.Should().BeGreaterThan(lastPositionFirstPage);
    }

    [Fact]
    public async Task Handle_VectorIdAlwaysPresent_DegatedFromAdmin()
    {
        var docId = await SeedDocumentWithChunksAsync(3);

        var nonAdminResult = await _handler.Handle(
            new GetKbChunksQuery(docId, Guid.NewGuid(), null, 10, UserIsAdmin: false),
            CancellationToken.None);

        var adminResult = await _handler.Handle(
            new GetKbChunksQuery(docId, Guid.NewGuid(), null, 10, UserIsAdmin: true),
            CancellationToken.None);

        // Spec §6.3.2: VectorId emitted as a string for ALL viewers.
        nonAdminResult.Items.Should().AllSatisfy(c => c.VectorId.Should().NotBeNullOrEmpty());
        adminResult.Items.Should().AllSatisfy(c => c.VectorId.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task Handle_DocNotFound_Throws404()
    {
        var query = new GetKbChunksQuery(
            Guid.NewGuid(),
            RequestingUserId: Guid.NewGuid(),
            Cursor: null,
            Limit: 50,
            UserIsAdmin: false);

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
        await _dbContext.SaveChangesAsync();

        var query = new GetKbChunksQuery(
            docId,
            RequestingUserId: Guid.NewGuid(),
            Cursor: null,
            Limit: 50,
            UserIsAdmin: false);

        await _handler.Invoking(h => h.Handle(query, CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task Handle_EmptyDoc_ReturnsEmptyItemsZeroTotal()
    {
        var docId = await SeedDocumentWithChunksAsync(0);

        var result = await _handler.Handle(
            new GetKbChunksQuery(docId, Guid.NewGuid(), null, 50, UserIsAdmin: false),
            CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
        result.NextCursor.Should().BeNull();
    }

    private async Task<Guid> SeedDocumentWithChunksAsync(int chunkCount)
    {
        var docId = Guid.NewGuid();
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = docId,
            FileName = $"test-{chunkCount}.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/test.pdf",
            IsPublic = true
        });

        for (int i = 0; i < chunkCount; i++)
        {
            _dbContext.TextChunks.Add(new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                PdfDocumentId = docId,
                Content = $"Chunk content {i} - some text to make a snippet",
                ChunkIndex = i,
                PageNumber = (i / 4) + 1,
                CharacterCount = 50,
                ElementType = "NarrativeText",
                Level = 1
            });
        }
        await _dbContext.SaveChangesAsync();
        return docId;
    }
}
