using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Handlers;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Commands;

/// <summary>
/// Unit tests for RemovePrivatePdfCommandHandler.
/// Issue #3651: Tests for removing private PDF from library entry.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class RemovePrivatePdfCommandHandlerTests
{
    private readonly Mock<IUserLibraryRepository> _mockLibraryRepository;
    private readonly Mock<ISharedGameRepository> _mockSharedGameRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly RemovePrivatePdfCommandHandler _handler;

    public RemovePrivatePdfCommandHandlerTests()
    {
        _mockLibraryRepository = new Mock<IUserLibraryRepository>();
        _mockSharedGameRepository = new Mock<ISharedGameRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();

        _handler = new RemovePrivatePdfCommandHandler(
            _mockLibraryRepository.Object,
            _mockSharedGameRepository.Object,
            _mockUnitOfWork.Object);
    }

    #region Null Guard Tests

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    #endregion

    #region Entry Not Found Tests

    [Fact]
    public async Task Handle_WhenEntryNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var command = new RemovePrivatePdfCommand(userId, entryId);

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserLibraryEntry?)null);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(act);
        exception.Message.Should().Contain(entryId.ToString());

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task Handle_WhenUserDoesNotOwnEntry_ThrowsForbiddenException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new RemovePrivatePdfCommand(userId, entryId);

        var entry = new UserLibraryEntry(entryId, otherUserId, gameId);
        entry.AssociatePrivatePdf(Guid.NewGuid());

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<ForbiddenException>(act);
        exception.Message.Should().Contain("permission");

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Game Not Found Tests

    [Fact]
    public async Task Handle_WhenSharedGameNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new RemovePrivatePdfCommand(userId, entryId);

        var entry = new UserLibraryEntry(entryId, userId, gameId);
        entry.AssociatePrivatePdf(Guid.NewGuid());

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        var exception = await Assert.ThrowsAsync<NotFoundException>(act);
        exception.Message.Should().Contain(gameId.ToString());

        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion

    #region Success Tests

    [Fact]
    public async Task Handle_WithValidRequest_RemovesPdfAndReturnsUpdatedEntry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var command = new RemovePrivatePdfCommand(userId, entryId);

        var entry = new UserLibraryEntry(entryId, userId, gameId);
        entry.AssociatePrivatePdf(pdfId);

        var sharedGame = CreateSharedGame(gameId, "Test Game", "Test Publisher", 2024);

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(entryId);
        result.UserId.Should().Be(userId);
        result.GameId.Should().Be(gameId);
        result.HasKb.Should().BeFalse();

        _mockLibraryRepository.Verify(r => r.UpdateAsync(entry, It.IsAny<CancellationToken>()), Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnsCorrectGameDetails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var entryId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var command = new RemovePrivatePdfCommand(userId, entryId);

        var entry = new UserLibraryEntry(entryId, userId, gameId);
        entry.AssociatePrivatePdf(Guid.NewGuid());

        var sharedGame = CreateSharedGame(gameId, "Catan", "KOSMOS", 1995);

        _mockLibraryRepository
            .Setup(r => r.GetByIdAsync(entryId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(entry);

        _mockSharedGameRepository
            .Setup(r => r.GetByIdAsync(gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(sharedGame);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.GameTitle.Should().Be("Catan");
        // Note: GamePublisher is null because SharedGame constructor doesn't add publishers
        // The actual publisher mapping is tested in dedicated DTO mapping tests
        result.GameYearPublished.Should().Be(1995);
    }

    #endregion

    #region Helper Methods

    private static SharedGame CreateSharedGame(Guid id, string title, string publisher, int yearPublished)
    {
        // Use the internal constructor for reconstitution (test purposes)
        return new SharedGame(
            id: id,
            title: title,
            yearPublished: yearPublished,
            description: $"{title} description",
            minPlayers: 2,
            maxPlayers: 4,
            playingTimeMinutes: 60,
            minAge: 10,
            complexityRating: 2.5m,
            averageRating: 8.0m,
            imageUrl: "https://example.com/image.jpg",
            thumbnailUrl: "https://example.com/thumb.jpg",
            rules: null,
            status: GameStatus.Published,
            createdBy: Guid.NewGuid(),
            modifiedBy: null,
            createdAt: DateTime.UtcNow.AddDays(-30),
            modifiedAt: null,
            isDeleted: false,
            bggId: 12345);
    }

    #endregion
}
