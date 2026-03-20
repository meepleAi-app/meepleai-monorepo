using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

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
        gameId.Should().NotBe(Guid.Empty);

        _repositoryMock.Verify(
            r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        capturedGame.Should().NotBeNull();
        capturedGame.Title.Should().Be("Catan");
        capturedGame.YearPublished.Should().Be(1995);
        capturedGame.BggId.Should().Be(13);
        capturedGame.DomainEvents.Should().ContainSingle();
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
        var act = () =>
            _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

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
        capturedGame.Should().NotBeNull();
        capturedGame.Rules.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithCategories_AssociatesCategoriesWithGame()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateSharedGameCommand(
            Title: "Ticket to Ride",
            YearPublished: 2004,
            Description: "A cross-country train adventure game",
            MinPlayers: 2,
            MaxPlayers: 5,
            PlayingTimeMinutes: 75,
            MinAge: 8,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: "https://example.com/ttr.jpg",
            ThumbnailUrl: "https://example.com/ttr-thumb.jpg",
            Rules: null,
            CreatedBy: userId,
            BggId: null,
            Categories: ["Strategy", "Family"]);

        SharedGame? capturedGame = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        capturedGame.Should().NotBeNull();
        capturedGame.Categories.Count.Should().Be(2);
        capturedGame.Categories.Should().Contain(c => c.Name == "Strategy");
        capturedGame.Categories.Should().Contain(c => c.Name == "Family");
    }

    [Fact]
    public async Task Handle_WithDesignersAndPublishers_AssociatesWithGame()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateSharedGameCommand(
            Title: "Pandemic",
            YearPublished: 2008,
            Description: "A cooperative game about fighting diseases",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60,
            MinAge: 8,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: "https://example.com/pandemic.jpg",
            ThumbnailUrl: "https://example.com/pandemic-thumb.jpg",
            Rules: null,
            CreatedBy: userId,
            BggId: null,
            Designers: ["Matt Leacock"],
            Publishers: ["Z-Man Games", "Asmodee"]);

        SharedGame? capturedGame = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        capturedGame.Should().NotBeNull();
        capturedGame.Designers.Should().ContainSingle();
        capturedGame.Designers.First().Name.Should().Be("Matt Leacock");
        capturedGame.Publishers.Count.Should().Be(2);
        capturedGame.Publishers.Should().Contain(p => p.Name == "Z-Man Games");
        capturedGame.Publishers.Should().Contain(p => p.Name == "Asmodee");
    }

    [Fact]
    public async Task Handle_WithNullMetadata_CreatesGameWithoutMetadata()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateSharedGameCommand(
            Title: "Simple Game",
            YearPublished: 2020,
            Description: "A simple game with no metadata",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 30,
            MinAge: 6,
            ComplexityRating: null,
            AverageRating: null,
            ImageUrl: "https://example.com/simple.jpg",
            ThumbnailUrl: "https://example.com/simple-thumb.jpg",
            Rules: null,
            CreatedBy: userId,
            BggId: null,
            Categories: null,
            Mechanics: null,
            Designers: null,
            Publishers: null);

        SharedGame? capturedGame = null;
        _repositoryMock
            .Setup(r => r.AddAsync(It.IsAny<SharedGame>(), It.IsAny<CancellationToken>()))
            .Callback<SharedGame, CancellationToken>((g, _) => capturedGame = g)
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        capturedGame.Should().NotBeNull();
        capturedGame.Categories.Should().BeEmpty();
        capturedGame.Mechanics.Should().BeEmpty();
        capturedGame.Designers.Should().BeEmpty();
        capturedGame.Publishers.Should().BeEmpty();
    }
}