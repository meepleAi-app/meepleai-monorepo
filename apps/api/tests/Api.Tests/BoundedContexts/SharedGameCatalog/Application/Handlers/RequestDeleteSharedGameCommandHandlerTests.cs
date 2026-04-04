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
public class RequestDeleteSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _gameRepositoryMock;
    private readonly Mock<ISharedGameDeleteRequestRepository> _deleteRequestRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<RequestDeleteSharedGameCommandHandler>> _loggerMock;
    private readonly RequestDeleteSharedGameCommandHandler _handler;

    public RequestDeleteSharedGameCommandHandlerTests()
    {
        _gameRepositoryMock = new Mock<ISharedGameRepository>();
        _deleteRequestRepositoryMock = new Mock<ISharedGameDeleteRequestRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<RequestDeleteSharedGameCommandHandler>>();
        _handler = new RequestDeleteSharedGameCommandHandler(
            _gameRepositoryMock.Object,
            _deleteRequestRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesDeleteRequestSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var requestedBy = Guid.NewGuid();
        var command = new RequestDeleteSharedGameCommand(
            GameId: gameId,
            RequestedBy: requestedBy,
            Reason: "Duplicate entry");

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

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        SharedGameDeleteRequest? capturedRequest = null;
        _deleteRequestRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGameDeleteRequest>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGameDeleteRequest, CancellationToken>((r, _) => capturedRequest = r)
            .Returns(Task.CompletedTask);

        // Act
        var requestId = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        requestId.Should().NotBe(Guid.Empty);
        _deleteRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<SharedGameDeleteRequest>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new RequestDeleteSharedGameCommand(
            GameId: gameId,
            RequestedBy: Guid.NewGuid(),
            Reason: "Test reason");

        _gameRepositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        var act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        _deleteRequestRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<SharedGameDeleteRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
