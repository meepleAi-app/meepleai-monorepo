using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocuments;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GetRecentKbDocumentsHandler.
/// Verifies IsPublic + ProcessingState filter, IndexedAt!=null filter, and ordering.
/// Uses InMemoryDatabase — no external dependencies.
/// Issue #728.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetRecentKbDocumentsHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetRecentKbDocumentsHandler> _logger;
    private readonly GetRecentKbDocumentsHandler _handler;

    public GetRecentKbDocumentsHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetRecentKbDocumentsHandler>>();
        _handler = new GetRecentKbDocumentsHandler(_dbContext, _logger);
    }

    [Fact]
    public async Task Handle_FiltersOutNonReadyDocs()
    {
        // Arrange — seed one Ready doc, two with other states
        var readyDoc = SeedPdfDoc("Ready", isPublic: true, indexedAt: DateTime.UtcNow);
        SeedPdfDoc("Embedding", isPublic: true, indexedAt: DateTime.UtcNow);
        SeedPdfDoc("Failed", isPublic: true, indexedAt: DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetRecentKbDocumentsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(readyDoc.Id);
    }

    [Fact]
    public async Task Handle_FiltersOutPrivateDocs()
    {
        // Arrange
        SeedPdfDoc("Ready", isPublic: false, indexedAt: DateTime.UtcNow);
        var publicDoc = SeedPdfDoc("Ready", isPublic: true, indexedAt: DateTime.UtcNow);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetRecentKbDocumentsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.Single().Id.Should().Be(publicDoc.Id);
    }

    [Fact]
    public async Task Handle_OrdersByIndexedAtDesc()
    {
        // Arrange
        var older = SeedPdfDoc("Ready", isPublic: true, indexedAt: DateTime.UtcNow.AddDays(-5));
        var newer = SeedPdfDoc("Ready", isPublic: true, indexedAt: DateTime.UtcNow.AddDays(-1));
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetRecentKbDocumentsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result.First().Id.Should().Be(newer.Id);
    }

    private PdfDocumentEntity SeedPdfDoc(string processingState, bool isPublic, DateTime indexedAt)
    {
        var doc = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            FileName = "test.pdf",
            FilePath = "/tmp/test.pdf",
            ProcessingState = processingState,
            IsPublic = isPublic,
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid()
        };
        _dbContext.PdfDocuments.Add(doc);

        var vd = new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = doc.Id,
            GameId = Guid.NewGuid(),
            ChunkCount = 10,
            IndexedAt = indexedAt,
            IndexingStatus = "completed"
        };
        _dbContext.VectorDocuments.Add(vd);

        return doc;
    }
}
