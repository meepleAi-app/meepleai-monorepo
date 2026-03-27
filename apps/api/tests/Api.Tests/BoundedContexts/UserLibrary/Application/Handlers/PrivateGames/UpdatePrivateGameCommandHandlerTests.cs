using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Comprehensive unit tests for UpdatePrivateGameCommandHandler.
/// Issue #3670: Phase 9 - Testing &amp; Polish.
/// Tests: Successful updates, ownership verification, not found scenarios.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class UpdatePrivateGameCommandHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<UpdatePrivateGameCommandHandler>> _loggerMock;
    private readonly UpdatePrivateGameCommandHandler _handler;

    public UpdatePrivateGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<IPrivateGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<UpdatePrivateGameCommandHandler>>();

        _handler = new UpdatePrivateGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new UpdatePrivateGameCommandHandler(
                null!,
                _unitOfWorkMock.Object,
                _loggerMock.Object);
        var exception = act.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("repository");
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act2 = () =>
            new UpdatePrivateGameCommandHandler(
                _repositoryMock.Object,
                null!,
                _loggerMock.Object);
        var exception = act2.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("unitOfWork");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act3 = () =>
            new UpdatePrivateGameCommandHandler(
                _repositoryMock.Object,
                _unitOfWorkMock.Object,
                null!);
        var exception = act3.Should().Throw<ArgumentNullException>().Which;

        exception.ParamName.Should().Be("logger");
    }

    #endregion

    #region Successful Update Tests

    [Fact]
    public async Task Handle_ValidUpdate_UpdatesAllFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Original Title",
            minPlayers: 2,
            maxPlayers: 4);

        // Use reflection to set the Id since it's protected
        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: userId,
            Title: "Updated Title",
            MinPlayers: 3,
            MaxPlayers: 6,
            YearPublished: 2024,
            Description: "Updated description",
            PlayingTimeMinutes: 120,
            MinAge: 14,
            ComplexityRating: 3.5m,
            ImageUrl: "https://example.com/updated.jpg");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Updated Title");
        result.MinPlayers.Should().Be(3);
        result.MaxPlayers.Should().Be(6);
        result.YearPublished.Should().Be(2024);
        result.Description.Should().Be("Updated description");
        result.PlayingTimeMinutes.Should().Be(120);
        result.MinAge.Should().Be(14);
        result.ComplexityRating.Should().Be(3.5m);
        result.ImageUrl.Should().Be("https://example.com/updated.jpg");
        result.UpdatedAt.Should().NotBeNull();

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PartialUpdate_ClearsOptionalFields()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Original Title",
            minPlayers: 2,
            maxPlayers: 4,
            yearPublished: 2020,
            description: "Original description",
            complexityRating: 2.5m);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: userId,
            Title: "Updated Title",
            MinPlayers: 2,
            MaxPlayers: 4,
            YearPublished: null, // Clear year
            Description: null,   // Clear description
            PlayingTimeMinutes: null,
            MinAge: null,
            ComplexityRating: null, // Clear complexity
            ImageUrl: null);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Updated Title");
        result.YearPublished.Should().BeNull();
        result.Description.Should().BeNull();
        result.ComplexityRating.Should().BeNull();
    }

    [Fact]
    public async Task Handle_BggGame_PreservesBggIdAndSource()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateFromBgg(
            ownerId: userId,
            bggId: 12345,
            title: "Original BGG Title",
            yearPublished: 2020,
            description: "Original",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            imageUrl: "https://example.com/old.jpg",
            thumbnailUrl: "https://example.com/old-thumb.jpg");

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: userId,
            Title: "Updated BGG Title",
            MinPlayers: 3,
            MaxPlayers: 5,
            YearPublished: 2021,
            Description: "Updated description",
            PlayingTimeMinutes: 90,
            MinAge: 12,
            ComplexityRating: 3.0m,
            ImageUrl: "https://example.com/new.jpg");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BggId.Should().Be(12345); // Preserved
        result.Source.Should().Be("BoardGameGeek"); // Preserved
        result.ThumbnailUrl.Should().Be("https://example.com/old-thumb.jpg"); // Preserved (not updated by UpdateInfo)
        result.Title.Should().Be("Updated BGG Title");
        result.CanProposeToCatalog.Should().BeTrue();
    }

    #endregion

    #region Not Found Tests

    [Fact]
    public async Task Handle_GameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: Guid.NewGuid(),
            Title: "Updated Title",
            MinPlayers: 2,
            MaxPlayers: 4,
            YearPublished: null,
            Description: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            ComplexityRating: null,
            ImageUrl: null);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PrivateGame?)null);

        // Act & Assert
        var act4 = () =>
            _handler.Handle(command, CancellationToken.None);
        var exception = (await act4.Should().ThrowAsync<NotFoundException>()).Which;

        exception.Message.Should().Contain(gameId.ToString());

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Ownership Verification Tests

    [Fact]
    public async Task Handle_DifferentOwner_ThrowsForbiddenException()
    {
        // Arrange
        var ownerId = Guid.NewGuid();
        var attackerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: ownerId,
            title: "Original Title",
            minPlayers: 2,
            maxPlayers: 4);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: attackerId, // Different user trying to update
            Title: "Malicious Update",
            MinPlayers: 2,
            MaxPlayers: 4,
            YearPublished: null,
            Description: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            ComplexityRating: null,
            ImageUrl: null);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        // Act & Assert
        var act5 = () =>
            _handler.Handle(command, CancellationToken.None);
        var exception = (await act5.Should().ThrowAsync<ForbiddenException>()).Which;

        exception.Message.Should().Contain("only update your own");

        _repositoryMock.Verify(
            r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SameOwner_UpdatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Original Title",
            minPlayers: 2,
            maxPlayers: 4);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: userId,
            Title: "Updated Title",
            MinPlayers: 2,
            MaxPlayers: 4,
            YearPublished: null,
            Description: null,
            PlayingTimeMinutes: null,
            MinAge: null,
            ComplexityRating: null,
            ImageUrl: null);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Updated Title");
    }

    #endregion

    #region Null Command Tests

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act6 = () =>
            _handler.Handle(null!, CancellationToken.None);
        await act6.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region DTO Mapping Tests

    [Fact]
    public async Task Handle_UpdatedGame_MapsAllFieldsToDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var existingGame = PrivateGame.CreateManual(
            ownerId: userId,
            title: "Original",
            minPlayers: 1,
            maxPlayers: 2);

        typeof(PrivateGame).GetProperty("Id")!.SetValue(existingGame, gameId);

        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: userId,
            Title: "Mapped Title",
            MinPlayers: 2,
            MaxPlayers: 8,
            YearPublished: 2022,
            Description: "Mapped description",
            PlayingTimeMinutes: 150,
            MinAge: 16,
            ComplexityRating: 4.0m,
            ImageUrl: "https://example.com/mapped.jpg");

        _repositoryMock
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingGame);

        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(gameId);
        result.OwnerId.Should().Be(userId);
        result.Source.Should().Be("Manual");
        result.BggId.Should().BeNull();
        result.Title.Should().Be("Mapped Title");
        result.YearPublished.Should().Be(2022);
        result.Description.Should().Be("Mapped description");
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(8);
        result.PlayingTimeMinutes.Should().Be(150);
        result.MinAge.Should().Be(16);
        result.ComplexityRating.Should().Be(4.0m);
        result.ImageUrl.Should().Be("https://example.com/mapped.jpg");
        result.ThumbnailUrl.Should().BeNull();
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.BggSyncedAt.Should().BeNull();
        result.CanProposeToCatalog.Should().BeFalse();
    }

    #endregion

    #region Command Record Tests

    [Fact]
    public void UpdatePrivateGameCommand_CreatesWithAllProperties()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var command = new UpdatePrivateGameCommand(
            PrivateGameId: gameId,
            UserId: userId,
            Title: "Updated Title",
            MinPlayers: 3,
            MaxPlayers: 6,
            YearPublished: 2024,
            Description: "Updated description",
            PlayingTimeMinutes: 90,
            MinAge: 12,
            ComplexityRating: 3.0m,
            ImageUrl: "https://example.com/updated.jpg");

        // Assert
        command.PrivateGameId.Should().Be(gameId);
        command.UserId.Should().Be(userId);
        command.Title.Should().Be("Updated Title");
        command.MinPlayers.Should().Be(3);
        command.MaxPlayers.Should().Be(6);
    }

    #endregion
}
