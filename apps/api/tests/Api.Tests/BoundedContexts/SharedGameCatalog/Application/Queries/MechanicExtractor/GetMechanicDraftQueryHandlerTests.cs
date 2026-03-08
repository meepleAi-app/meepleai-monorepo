using Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class GetMechanicDraftQueryHandlerTests
{
    private readonly Mock<IMechanicDraftRepository> _draftRepositoryMock;
    private readonly GetMechanicDraftQueryHandler _handler;

    public GetMechanicDraftQueryHandlerTests()
    {
        _draftRepositoryMock = new Mock<IMechanicDraftRepository>();
        _handler = new GetMechanicDraftQueryHandler(_draftRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_WhenDraftExists_ReturnsMappedDto()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var draft = MechanicDraft.Create(gameId, pdfId, "Catan", Guid.NewGuid());
        draft.UpdateNotes("summary", "My summary notes");

        var query = new GetMechanicDraftQuery(gameId, pdfId);

        _draftRepositoryMock.Setup(r => r.GetDraftForGameAsync(gameId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.SharedGameId.Should().Be(gameId);
        result.PdfDocumentId.Should().Be(pdfId);
        result.GameTitle.Should().Be("Catan");
        result.SummaryNotes.Should().Be("My summary notes");
    }

    [Fact]
    public async Task Handle_WhenNoDraftExists_ReturnsNull()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var query = new GetMechanicDraftQuery(gameId, pdfId);

        _draftRepositoryMock.Setup(r => r.GetDraftForGameAsync(gameId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicDraft?)null);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_MapsAllFieldsCorrectly()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var draft = MechanicDraft.Create(gameId, pdfId, "Wingspan", Guid.NewGuid());
        draft.UpdateAllNotes("s", "m", "v", "r", "p", "q");
        draft.AcceptDraft("summary", "AI summary");
        draft.AcceptDraft("mechanics", "[\"Engine Building\"]");

        var query = new GetMechanicDraftQuery(gameId, pdfId);

        _draftRepositoryMock.Setup(r => r.GetDraftForGameAsync(gameId, pdfId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result!.SummaryNotes.Should().Be("s");
        result.MechanicsNotes.Should().Be("m");
        result.SummaryDraft.Should().Be("AI summary");
        result.MechanicsDraft.Should().Be("[\"Engine Building\"]");
        result.VictoryDraft.Should().BeEmpty();
        result.Status.Should().Be("Draft");
    }
}
