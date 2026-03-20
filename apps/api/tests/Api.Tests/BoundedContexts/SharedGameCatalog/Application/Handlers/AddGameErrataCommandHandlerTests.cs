using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class AddGameErrataCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<AddGameErrataCommandHandler>> _loggerMock;
    private readonly AddGameErrataCommandHandler _handler;

    public AddGameErrataCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<AddGameErrataCommandHandler>>();
        _handler = new AddGameErrataCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_AddsErrataSuccessfully()
    {
        // Arrange
        var game = SharedGame.Create(
            title: "Test Game",
            yearPublished: 2020,
            description: "Test",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: null,
            averageRating: null,
            imageUrl: "https://example.com/game.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: null);

        var publishedDate = DateTime.UtcNow;
        var command = new AddGameErrataCommand(
            SharedGameId: game.Id,
            Description: "Corrected rule on page 5",
            PageReference: "Page 5",
            PublishedDate: publishedDate);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act
        var errataId = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        errataId.Should().NotBe(Guid.Empty);
        _repositoryMock.Verify(r => r.Update(game), Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new AddGameErrataCommand(
            SharedGameId: gameId,
            Description: "Errata description",
            PageReference: "Page 1",
            PublishedDate: DateTime.UtcNow);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _repositoryMock.Verify(r => r.Update(It.IsAny<SharedGame>()), Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}