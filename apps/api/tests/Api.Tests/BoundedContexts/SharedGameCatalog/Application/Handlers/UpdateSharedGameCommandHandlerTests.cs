using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class UpdateSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UpdateSharedGameCommandHandler>> _loggerMock;
    private readonly UpdateSharedGameCommandHandler _handler;

    public UpdateSharedGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UpdateSharedGameCommandHandler>>();
        _handler = new UpdateSharedGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_UpdatesGameSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var modifierId = Guid.NewGuid();

        var existingGame = SharedGame.Create(
            "Old Title",
            2020,
            "Old description",
            2,
            4,
            60,
            8,
            2.0m,
            7.0m,
            "https://example.com/old.jpg",
            "https://example.com/old-thumb.jpg",
            GameRules.Create("Old rules", "en"),
            userId,
            null);

        var command = new UpdateSharedGameCommand(
            GameId: gameId,
            Title: "New Title",
            YearPublished: 2021,
            Description: "New description",
            MinPlayers: 3,
            MaxPlayers: 6,
            PlayingTimeMinutes: 90,
            MinAge: 10,
            ComplexityRating: 3.0m,
            AverageRating: 8.5m,
            ImageUrl: "https://example.com/new.jpg",
            ThumbnailUrl: "https://example.com/new-thumb.jpg",
            Rules: new GameRulesDto("New rules", "it"),
            ModifiedBy: modifierId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGame>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        // Verify updated game state (through domain events)
        Assert.Equal(2, existingGame.DomainEvents.Count); // Created + Updated events
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new UpdateSharedGameCommand(
            gameId,
            "Title",
            2020,
            "Description",
            2,
            4,
            60,
            8,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            null,
            Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGame>()),
            Times.Never);
    }
}
