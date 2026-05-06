using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;
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
public sealed class GetKbDocumentByIdHandlerTests
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetKbDocumentByIdHandler> _logger;
    private readonly GetKbDocumentByIdHandler _handler;

    public GetKbDocumentByIdHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _logger = Substitute.For<ILogger<GetKbDocumentByIdHandler>>();
        _handler = new GetKbDocumentByIdHandler(_dbContext, _logger);
    }

    [Fact]
    public async Task Handle_WhenDocReady_ReturnsFullMetadata()
    {
        // Arrange
        var docId = Guid.NewGuid();
        var pdf = new PdfDocumentEntity
        {
            Id = docId,
            FileName = "Catan rulebook.pdf",
            ProcessingState = "Ready",
            PageCount = 32,
            UploadedAt = DateTime.UtcNow.AddHours(-1),
            Language = "it",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/test.pdf"
        };
        var vd = new VectorDocumentEntity
        {
            Id = Guid.NewGuid(),
            PdfDocumentId = docId,
            GameId = Guid.NewGuid(),
            ChunkCount = 120,
            IndexedAt = DateTime.UtcNow,
            IndexingStatus = "Completed"
        };
        _dbContext.PdfDocuments.Add(pdf);
        _dbContext.VectorDocuments.Add(vd);
        await _dbContext.SaveChangesAsync();

        var query = new GetKbDocumentByIdQuery(docId, UserIsAdmin: false);

        // Act
        var dto = await _handler.Handle(query, CancellationToken.None);

        // Assert
        dto.Should().NotBeNull();
        dto!.Title.Should().Be("Catan rulebook.pdf");
        dto.ProcessingState.Should().Be("ready");
        dto.TotalChunks.Should().Be(120);
        dto.PageCount.Should().Be(32);
        dto.ProcessingError.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenDocFailed_AdminSeesError()
    {
        var docId = Guid.NewGuid();
        var pdf = new PdfDocumentEntity
        {
            Id = docId,
            FileName = "BrokenDoc.pdf",
            ProcessingState = "Failed",
            ProcessingError = "Embedding API timeout",
            RetryCount = 2,
            FailedAtState = "Embedding",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/broken.pdf"
        };
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var query = new GetKbDocumentByIdQuery(docId, UserIsAdmin: true);

        var dto = await _handler.Handle(query, CancellationToken.None);

        dto.Should().NotBeNull();
        dto!.ProcessingError.Should().Be("Embedding API timeout");
        dto.RetryCount.Should().Be(2);
        dto.FailedAtState.Should().Be("Embedding");
    }

    [Fact]
    public async Task Handle_WhenNonAdminViewsFailedDoc_ErrorFieldsAreNull()
    {
        var docId = Guid.NewGuid();
        var pdf = new PdfDocumentEntity
        {
            Id = docId,
            FileName = "BrokenDoc.pdf",
            ProcessingState = "Failed",
            ProcessingError = "Embedding API timeout",
            RetryCount = 2,
            FailedAtState = "Embedding",
            UploadedAt = DateTime.UtcNow,
            Language = "en",
            DocumentCategory = "Rulebook",
            UploadedByUserId = Guid.NewGuid(),
            FilePath = "/tmp/broken.pdf"
        };
        _dbContext.PdfDocuments.Add(pdf);
        await _dbContext.SaveChangesAsync();

        var query = new GetKbDocumentByIdQuery(docId, UserIsAdmin: false);

        var dto = await _handler.Handle(query, CancellationToken.None);

        dto!.ProcessingState.Should().Be("failed");
        dto.ProcessingError.Should().BeNull();
        dto.RetryCount.Should().BeNull();
        dto.FailedAtState.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenDocNotFound_ThrowsNotFoundException()
    {
        var query = new GetKbDocumentByIdQuery(Guid.NewGuid(), UserIsAdmin: false);

        var act = () => _handler.Handle(query, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
