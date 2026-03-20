using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;

/// <summary>
/// Comprehensive unit tests for AddPrivateGameCommandHandler.
/// Issue #3670: Phase 9 - Testing &amp; Polish.
/// Tests: Manual/BGG creation, auto-redirect, duplicate prevention, validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class AddPrivateGameCommandHandlerTests
{
    private readonly Mock<IPrivateGameRepository> _privateGameRepositoryMock;
    private readonly Mock<IUserLibraryRepository> _userLibraryRepositoryMock;
    private readonly Mock<ISharedGameRepository> _sharedGameRepositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<AddPrivateGameCommandHandler>> _loggerMock;
    private readonly AddPrivateGameCommandHandler _handler;

    public AddPrivateGameCommandHandlerTests()
    {
        _privateGameRepositoryMock = new Mock<IPrivateGameRepository>();
        _userLibraryRepositoryMock = new Mock<IUserLibraryRepository>();
        _sharedGameRepositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<AddPrivateGameCommandHandler>>();

        _handler = new AddPrivateGameCommandHandler(
            _privateGameRepositoryMock.Object,
            _userLibraryRepositoryMock.Object,
            _sharedGameRepositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_NullPrivateGameRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AddPrivateGameCommandHandler(
                null!,
                _userLibraryRepositoryMock.Object,
                _sharedGameRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _loggerMock.Object));

        exception.ParamName.Should().Be("privateGameRepository");
    }

    [Fact]
    public void Constructor_NullUserLibraryRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AddPrivateGameCommandHandler(
                _privateGameRepositoryMock.Object,
                null!,
                _sharedGameRepositoryMock.Object,
                _unitOfWorkMock.Object,
                _loggerMock.Object));

        exception.ParamName.Should().Be("userLibraryRepository");
    }

    [Fact]
    public void Constructor_NullSharedGameRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AddPrivateGameCommandHandler(
                _privateGameRepositoryMock.Object,
                _userLibraryRepositoryMock.Object,
                null!,
                _unitOfWorkMock.Object,
                _loggerMock.Object));

        exception.ParamName.Should().Be("sharedGameRepository");
    }

    [Fact]
    public void Constructor_NullUnitOfWork_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AddPrivateGameCommandHandler(
                _privateGameRepositoryMock.Object,
                _userLibraryRepositoryMock.Object,
                _sharedGameRepositoryMock.Object,
                null!,
                _loggerMock.Object));

        exception.ParamName.Should().Be("unitOfWork");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            new AddPrivateGameCommandHandler(
                _privateGameRepositoryMock.Object,
                _userLibraryRepositoryMock.Object,
                _sharedGameRepositoryMock.Object,
                _unitOfWorkMock.Object,
                null!));

        exception.ParamName.Should().Be("logger");
    }

    #endregion

    #region Manual Game Creation Tests

    [Fact]
    public async Task Handle_ManualGameWithRequiredFields_CreatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "Manual",
            BggId: null,
            Title: "My Custom Game",
            MinPlayers: 2,
            MaxPlayers: 4);

        _privateGameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("My Custom Game");
        result.Source.Should().Be("Manual");
        result.MinPlayers.Should().Be(2);
        result.MaxPlayers.Should().Be(4);
        result.BggId.Should().BeNull();
        result.OwnerId.Should().Be(userId);
        result.CanProposeToCatalog.Should().BeFalse(); // Manual games can't be proposed

        _privateGameRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ManualGameWithAllOptionalFields_CreatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "Manual",
            BggId: null,
            Title: "Detailed Custom Game",
            MinPlayers: 1,
            MaxPlayers: 6,
            YearPublished: 2023,
            Description: "A detailed description",
            PlayingTimeMinutes: 90,
            MinAge: 12,
            ComplexityRating: 3.5m,
            ImageUrl: "https://example.com/image.jpg");

        _privateGameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.YearPublished.Should().Be(2023);
        result.Description.Should().Be("A detailed description");
        result.PlayingTimeMinutes.Should().Be(90);
        result.MinAge.Should().Be(12);
        result.ComplexityRating.Should().Be(3.5m);
        result.ImageUrl.Should().Be("https://example.com/image.jpg");
    }

    [Theory]
    [InlineData("manual")]
    [InlineData("MANUAL")]
    [InlineData("Manual")]
    public async Task Handle_ManualSourceCaseInsensitive_CreatesSuccessfully(string source)
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: source,
            BggId: null,
            Title: "Test Game",
            MinPlayers: 2,
            MaxPlayers: 4);

        _privateGameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Source.Should().Be("Manual");
    }

    #endregion

    #region BGG Game Creation Tests

    [Fact]
    public async Task Handle_BggGameWithAllFields_CreatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "BoardGameGeek",
            BggId: 12345,
            Title: "Imported BGG Game",
            MinPlayers: 2,
            MaxPlayers: 5,
            YearPublished: 2020,
            Description: "A game from BGG",
            PlayingTimeMinutes: 120,
            MinAge: 14,
            ComplexityRating: 4.2m,
            ImageUrl: "https://cf.geekdo-images.com/image.jpg",
            ThumbnailUrl: "https://cf.geekdo-images.com/thumb.jpg");

        _sharedGameRepositoryMock
            .Setup(r => r.GetByBggIdAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        _privateGameRepositoryMock
            .Setup(r => r.ExistsByOwnerAndBggIdAsync(userId, 12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privateGameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Imported BGG Game");
        result.Source.Should().Be("BoardGameGeek");
        result.BggId.Should().Be(12345);
        result.ThumbnailUrl.Should().Be("https://cf.geekdo-images.com/thumb.jpg");
        result.CanProposeToCatalog.Should().BeTrue(); // BGG games can be proposed
    }

    [Theory]
    [InlineData("boardgamegeek")]
    [InlineData("BOARDGAMEGEEK")]
    [InlineData("BoardGameGeek")]
    public async Task Handle_BggSourceCaseInsensitive_CreatesSuccessfully(string source)
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: source,
            BggId: 99999,
            Title: "Test BGG Game",
            MinPlayers: 2,
            MaxPlayers: 4);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByBggIdAsync(99999, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        _privateGameRepositoryMock
            .Setup(r => r.ExistsByOwnerAndBggIdAsync(It.IsAny<Guid>(), 99999, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privateGameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Source.Should().Be("BoardGameGeek");
    }

    #endregion

    #region Auto-Redirect Tests (BggId exists in SharedCatalog)

    [Fact]
    public async Task Handle_BggIdExistsInSharedCatalog_ThrowsConflictException()
    {
        // Arrange
        var existingSharedGameId = Guid.NewGuid();
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "BoardGameGeek",
            BggId: 12345,
            Title: "Already in Catalog",
            MinPlayers: 2,
            MaxPlayers: 4);

        var existingSharedGame = SharedGame.Create(
            title: "Existing Shared Game",
            yearPublished: 2020,
            description: "Test description",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 7.5m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            createdBy: Guid.NewGuid(),
            bggId: 12345);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByBggIdAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingSharedGame);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("already exists in the shared catalog");
        exception.Message.Should().Contain("12345");

        _privateGameRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Duplicate Prevention Tests

    [Fact]
    public async Task Handle_DuplicateBggIdForSameUser_ThrowsConflictException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "BoardGameGeek",
            BggId: 12345,
            Title: "Duplicate Game",
            MinPlayers: 2,
            MaxPlayers: 4);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByBggIdAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        _privateGameRepositoryMock
            .Setup(r => r.ExistsByOwnerAndBggIdAsync(userId, 12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Already exists

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("already have a private game");
        exception.Message.Should().Contain("12345");

        _privateGameRepositoryMock.Verify(
            r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_SameBggIdDifferentUser_CreatesSuccessfully()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "BoardGameGeek",
            BggId: 12345,
            Title: "Same BGG Different User",
            MinPlayers: 2,
            MaxPlayers: 4);

        _sharedGameRepositoryMock
            .Setup(r => r.GetByBggIdAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Different user has this BggId, but current user doesn't
        _privateGameRepositoryMock
            .Setup(r => r.ExistsByOwnerAndBggIdAsync(userId, 12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privateGameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.BggId.Should().Be(12345);
    }

    #endregion

    #region Validation Error Tests

    [Fact]
    public async Task Handle_InvalidSource_ThrowsDomainException()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "InvalidSource",
            BggId: null,
            Title: "Test Game",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("Invalid source");
        exception.Message.Should().Contain("InvalidSource");
    }

    [Fact]
    public async Task Handle_BggSourceWithoutBggId_ThrowsDomainException()
    {
        // Arrange
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "BoardGameGeek",
            BggId: null, // Missing BggId
            Title: "Test Game",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<DomainException>(() =>
            _handler.Handle(command, CancellationToken.None));

        exception.Message.Should().Contain("BoardGameGeek source requires BggId");
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.Handle(null!, CancellationToken.None));
    }

    #endregion

    #region DTO Mapping Tests

    [Fact]
    public async Task Handle_CreatedGame_MapsAllFieldsToDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "BoardGameGeek",
            BggId: 55555,
            Title: "Complete Mapping Test",
            MinPlayers: 1,
            MaxPlayers: 8,
            YearPublished: 2021,
            Description: "Full description for mapping test",
            PlayingTimeMinutes: 180,
            MinAge: 10,
            ComplexityRating: 2.8m,
            ImageUrl: "https://example.com/full.jpg",
            ThumbnailUrl: "https://example.com/thumb.jpg");

        _sharedGameRepositoryMock
            .Setup(r => r.GetByBggIdAsync(55555, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        _privateGameRepositoryMock
            .Setup(r => r.ExistsByOwnerAndBggIdAsync(userId, 55555, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _privateGameRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<PrivateGame>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert - Verify all fields are mapped correctly
        result.Should().NotBeNull();
        result.Id.Should().NotBeEmpty();
        result.OwnerId.Should().Be(userId);
        result.Source.Should().Be("BoardGameGeek");
        result.BggId.Should().Be(55555);
        result.Title.Should().Be("Complete Mapping Test");
        result.YearPublished.Should().Be(2021);
        result.Description.Should().Be("Full description for mapping test");
        result.MinPlayers.Should().Be(1);
        result.MaxPlayers.Should().Be(8);
        result.PlayingTimeMinutes.Should().Be(180);
        result.MinAge.Should().Be(10);
        result.ComplexityRating.Should().Be(2.8m);
        result.ImageUrl.Should().Be("https://example.com/full.jpg");
        result.ThumbnailUrl.Should().Be("https://example.com/thumb.jpg");
        result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.UpdatedAt.Should().BeNull();
        result.BggSyncedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5)); // BGG games have sync time
        result.CanProposeToCatalog.Should().BeTrue();
    }

    #endregion

    #region Command Record Tests

    [Fact]
    public void AddPrivateGameCommand_Manual_CreatesWithRequiredProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "Manual",
            BggId: null,
            Title: "My Custom Game",
            MinPlayers: 2,
            MaxPlayers: 4);

        // Assert
        command.UserId.Should().Be(userId);
        command.Source.Should().Be("Manual");
        command.BggId.Should().BeNull();
        command.Title.Should().Be("My Custom Game");
        command.MinPlayers.Should().Be(2);
        command.MaxPlayers.Should().Be(4);
        command.YearPublished.Should().BeNull();
        command.Description.Should().BeNull();
    }

    [Fact]
    public void AddPrivateGameCommand_Manual_CreatesWithAllOptionalProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "Manual",
            BggId: null,
            Title: "Detailed Custom Game",
            MinPlayers: 1,
            MaxPlayers: 6,
            YearPublished: 2023,
            Description: "A custom game with full details",
            PlayingTimeMinutes: 90,
            MinAge: 12,
            ComplexityRating: 3.5m,
            ImageUrl: "https://example.com/image.jpg");

        // Assert
        command.YearPublished.Should().Be(2023);
        command.Description.Should().Be("A custom game with full details");
        command.PlayingTimeMinutes.Should().Be(90);
        command.MinAge.Should().Be(12);
        command.ComplexityRating.Should().Be(3.5m);
        command.ImageUrl.Should().Be("https://example.com/image.jpg");
    }

    [Fact]
    public void AddPrivateGameCommand_BoardGameGeek_CreatesWithBggId()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var command = new AddPrivateGameCommand(
            UserId: userId,
            Source: "BoardGameGeek",
            BggId: 12345,
            Title: "BGG Imported Game",
            MinPlayers: 2,
            MaxPlayers: 5,
            ThumbnailUrl: "https://cf.geekdo-images.com/thumb.jpg");

        // Assert
        command.Source.Should().Be("BoardGameGeek");
        command.BggId.Should().Be(12345);
        command.ThumbnailUrl.Should().Be("https://cf.geekdo-images.com/thumb.jpg");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 4)]
    [InlineData(1, 100)]
    public void AddPrivateGameCommand_ValidPlayerCounts_CreatesSuccessfully(int minPlayers, int maxPlayers)
    {
        // Act
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test Game",
            MinPlayers: minPlayers,
            MaxPlayers: maxPlayers);

        // Assert
        command.MinPlayers.Should().Be(minPlayers);
        command.MaxPlayers.Should().Be(maxPlayers);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(2.5)]
    [InlineData(5.0)]
    public void AddPrivateGameCommand_ValidComplexityRating_CreatesSuccessfully(decimal rating)
    {
        // Act
        var command = new AddPrivateGameCommand(
            UserId: Guid.NewGuid(),
            Source: "Manual",
            BggId: null,
            Title: "Test Game",
            MinPlayers: 2,
            MaxPlayers: 4,
            ComplexityRating: rating);

        // Assert
        command.ComplexityRating.Should().Be(rating);
    }

    #endregion
}
