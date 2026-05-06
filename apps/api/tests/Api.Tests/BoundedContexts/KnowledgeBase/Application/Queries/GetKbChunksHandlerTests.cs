using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetKbChunksHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbChunksHandler> _logger;
    private readonly GetKbChunksHandler _handler;

    public GetKbChunksHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetKbChunksHandler>>();
        _handler = new GetKbChunksHandler(_dbContext, _logger);
    }

    [Fact]
    public async Task Handle_WhenDocHas120Chunks_ReturnsFirstPage()
    {
        var docId = await SeedDocumentWithChunksAsync(120, processingState: "Ready");
        var query = new GetKbChunksQuery(docId, RequestingUserId: Guid.NewGuid(), Skip: 0, Take: 50, UserIsAdmin: false);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Chunks.Should().HaveCount(50);
        result.Chunks.Select(c => c.Position).Should().BeInAscendingOrder();
        result.TotalCount.Should().Be(120);
        result.HasMore.Should().BeTrue();
        result.Chunks.First().Snippet.Length.Should().BeLessThanOrEqualTo(200);
        result.ProcessingState.Should().Be("ready");
    }

    [Fact]
    public async Task Handle_LastPagePartial_HasMoreFalse()
    {
        var docId = await SeedDocumentWithChunksAsync(120, processingState: "Ready");
        var query = new GetKbChunksQuery(docId, RequestingUserId: Guid.NewGuid(), Skip: 100, Take: 50, UserIsAdmin: false);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Chunks.Should().HaveCount(20);
        result.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DocStillProcessing_ReturnsEmptyChunks()
    {
        var docId = await SeedDocumentWithChunksAsync(0, processingState: "Embedding");
        var query = new GetKbChunksQuery(docId, RequestingUserId: Guid.NewGuid(), Skip: 0, Take: 50, UserIsAdmin: false);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Chunks.Should().BeEmpty();
        result.ProcessingState.Should().Be("embedding");
    }

    [Fact]
    public async Task Handle_AdminSeesDiagnosticFields()
    {
        var docId = await SeedDocumentWithChunksAsync(5, processingState: "Ready");
        var query = new GetKbChunksQuery(docId, RequestingUserId: Guid.NewGuid(), Skip: 0, Take: 5, UserIsAdmin: true);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Chunks.Should().HaveCount(5);
        result.Chunks.First().VectorId.Should().NotBeNull();
        result.Chunks.First().CharacterCount.Should().NotBeNull();
        result.Chunks.First().ElementType.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_NonAdminGetsAdminFieldsNulled()
    {
        var docId = await SeedDocumentWithChunksAsync(5, processingState: "Ready");
        var query = new GetKbChunksQuery(docId, RequestingUserId: Guid.NewGuid(), Skip: 0, Take: 5, UserIsAdmin: false);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Chunks.Should().HaveCount(5);
        result.Chunks.First().VectorId.Should().BeNull();
        result.Chunks.First().CharacterCount.Should().BeNull();
        result.Chunks.First().ElementType.Should().BeNull();
        result.Chunks.First().EmbeddingStatus.Should().BeNull();
    }

    private async Task<Guid> SeedDocumentWithChunksAsync(int chunkCount, string processingState)
    {
        var docId = Guid.NewGuid();
        var pdf = new PdfDocumentEntity
        {
            Id = docId,
            FileName = $"test-{chunkCount}.pdf",
            ProcessingState = processingState,
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/test.pdf",
            IsPublic = true
        };
        _dbContext.PdfDocuments.Add(pdf);

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
