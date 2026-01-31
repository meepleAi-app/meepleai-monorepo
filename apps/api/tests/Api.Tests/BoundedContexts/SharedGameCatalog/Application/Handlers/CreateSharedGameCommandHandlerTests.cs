using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class CreateSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<CreateSharedGameCommandHandler>> _loggerMock;
    private readonly CreateSharedGameCommandHandler _handler;

    public CreateSharedGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<CreateSharedGameCommandHandler>>();
        _handler = new CreateSharedGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CreatesGameSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateSharedGameCommand(
            Title: "Catan",
            YearPublished: 1995,
            Description: "A classic resource management game",
            MinPlayers: 3,
            MaxPlayers: 4,
            PlayingTimeMinutes: 90,
            MinAge: 10,
            ComplexityRating: 2.5m,
            AverageRating: 7.8m,
            ImageUrl: "https://example.com/catan.jpg",
            ThumbnailUrl: "https://example.com/catan-thumb.jpg",
            Rules: new GameRulesDto("Game rules", "en"),
            CreatedBy: userId,
            BggId: 13);

        SharedGame? capturedGame = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        _repositoryMock
            .Setup(r => r.ExistsByBggIdAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var gameId = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotEqual(Guid.Empty, gameId);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(capturedGame);
        Assert.Equal("Catan", capturedGame.Title);
        Assert.Equal(1995, capturedGame.YearPublished);
        Assert.Equal(13, capturedGame.BggId);
        Assert.Single(capturedGame.DomainEvents);
    }

    [Fact]
    public async Task Handle_WithDuplicateBggId_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            "Catan",
            1995,
            "Description",
            3,
            4,
            90,
            10,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            null,
            Guid.NewGuid(),
            BggId: 13);

        _repositoryMock
            .Setup(r => r.ExistsByBggIdAsync(13, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _handler.Handle(command, TestContext.Current.CancellationToken));

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WithoutRules_CreatesGameWithNullRules()
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            "Simple Game",
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
            Rules: null,
            CreatedBy: Guid.NewGuid(),
            BggId: null);

        SharedGame? capturedGame = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(capturedGame);
        Assert.Null(capturedGame.Rules);
    }
}
