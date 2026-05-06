using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetKbChunkByIdHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbChunkByIdHandler> _logger;
    private readonly GetKbChunkByIdHandler _handler;

    public GetKbChunkByIdHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetKbChunkByIdHandler>>();
        _handler = new GetKbChunkByIdHandler(_dbContext, _logger);
    }

    [Fact]
    public async Task Handle_MiddleChunk_ReturnsContentWithPrevNext()
    {
        var docId = Guid.NewGuid();
        var (chunk0, chunk1, chunk2) = await SeedThreeChunksAsync(docId);

        var query = new GetKbChunkByIdQuery(docId, chunk1, UserIsAdmin: false);
        var dto = await _handler.Handle(query, CancellationToken.None);

        dto.ChunkId.Should().Be(chunk1);
        dto.PrevChunkId.Should().Be(chunk0);
        dto.NextChunkId.Should().Be(chunk2);
    }

    [Fact]
    public async Task Handle_FirstChunk_PrevIsNull()
    {
        var docId = Guid.NewGuid();
        var (chunk0, _, _) = await SeedThreeChunksAsync(docId);

        var query = new GetKbChunkByIdQuery(docId, chunk0, UserIsAdmin: false);
        var dto = await _handler.Handle(query, CancellationToken.None);

        dto.PrevChunkId.Should().BeNull();
        dto.NextChunkId.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_LastChunk_NextIsNull()
    {
        var docId = Guid.NewGuid();
        var (_, _, chunk2) = await SeedThreeChunksAsync(docId);

        var query = new GetKbChunkByIdQuery(docId, chunk2, UserIsAdmin: false);
        var dto = await _handler.Handle(query, CancellationToken.None);

        dto.NextChunkId.Should().BeNull();
        dto.PrevChunkId.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_ChunkBelongsToOtherDoc_ThrowsNotFound()
    {
        var docA = Guid.NewGuid();
        var docB = Guid.NewGuid();
        var (_, _, _) = await SeedThreeChunksAsync(docA);
        var (chunkB, _, _) = await SeedThreeChunksAsync(docB);

        // Request chunk from docB but specifying docA — must 404
        var query = new GetKbChunkByIdQuery(docA, chunkB, UserIsAdmin: false);
        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_NonExistentChunk_ThrowsNotFound()
    {
        var docId = Guid.NewGuid();
        await SeedThreeChunksAsync(docId);

        var query = new GetKbChunkByIdQuery(docId, Guid.NewGuid(), UserIsAdmin: false);
        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private async Task<(Guid chunk0, Guid chunk1, Guid chunk2)> SeedThreeChunksAsync(Guid docId)
    {
        var pdf = new PdfDocumentEntity
        {
            Id = docId,
            FileName = $"doc-{docId}.pdf",
            ProcessingState = "Ready",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/doc.pdf"
        };
        _dbContext.PdfDocuments.Add(pdf);

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
