using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for GetAdminGameKbDocumentsQueryHandler.
/// KB-01: Admin per-game KB document list.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class GetAdminGameKbDocumentsQueryHandlerTests
{
    private readonly Mock<IVectorDocumentRepository> _repoMock;
    private readonly Mock<ILogger<GetAdminGameKbDocumentsQueryHandler>> _loggerMock;
    private readonly GetAdminGameKbDocumentsQueryHandler _handler;

    public GetAdminGameKbDocumentsQueryHandlerTests()
    {
        _repoMock = new Mock<IVectorDocumentRepository>();
        _loggerMock = new Mock<ILogger<GetAdminGameKbDocumentsQueryHandler>>();
        _handler = new GetAdminGameKbDocumentsQueryHandler(_repoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenNoDocumentsExistForGame_ReturnsEmptyList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _repoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument>());

        var query = new GetAdminGameKbDocumentsQuery(gameId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GameId.Should().Be(gameId);
        result.Documents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenDocumentsExistForGame_ReturnsMappedList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var docId = Guid.NewGuid();
        var pdfDocId = Guid.NewGuid();

        var vectorDoc = new VectorDocument(docId, gameId, pdfDocId, "en", 42);

        _repoMock
            .Setup(r => r.GetByGameIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<VectorDocument> { vectorDoc });

        var query = new GetAdminGameKbDocumentsQuery(gameId);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.GameId.Should().Be(gameId);
        result.Documents.Should().HaveCount(1);

        var item = result.Documents.First();
        item.Id.Should().Be(docId);
        item.PdfDocumentId.Should().Be(pdfDocId);
        item.Language.Should().Be("en");
        item.ChunkCount.Should().Be(42);
        item.SharedGameId.Should().BeNull();
    }
}
