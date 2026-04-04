using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetAdminGameKbDocuments;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
    private readonly IVectorDocumentRepository _repo;
    private readonly ILogger<GetAdminGameKbDocumentsQueryHandler> _logger;
    private readonly GetAdminGameKbDocumentsQueryHandler _handler;

    public GetAdminGameKbDocumentsQueryHandlerTests()
    {
        _repo = Substitute.For<IVectorDocumentRepository>();
        _logger = Substitute.For<ILogger<GetAdminGameKbDocumentsQueryHandler>>();
        _handler = new GetAdminGameKbDocumentsQueryHandler(_repo, _logger);
    }

    [Fact]
    public async Task Handle_WhenNoDocumentsExistForGame_ReturnsEmptyList()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        _repo.GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument>());

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

        _repo.GetByGameIdAsync(gameId, Arg.Any<CancellationToken>())
            .Returns(new List<VectorDocument> { vectorDoc });

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
