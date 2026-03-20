using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class ArchiveSharedGameCommandHandlerTests
{
    private readonly Mock<ISharedGameRepository> _repositoryMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<ILogger<ArchiveSharedGameCommandHandler>> _loggerMock;
    private readonly ArchiveSharedGameCommandHandler _handler;

    public ArchiveSharedGameCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameRepository>();
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _loggerMock = new Mock<ILogger<ArchiveSharedGameCommandHandler>>();
        _handler = new ArchiveSharedGameCommandHandler(
            _repositoryMock.Object,
            _unitOfWorkMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithPublishedGame_ArchivesSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var archiverId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();

        var game = SharedGame.Create(
            "Test Game",
            2024,
            "Description",
            2,
            4,
            60,
            10,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            null,
            creatorId);

        var adminId = Guid.NewGuid();
        game.SubmitForApproval(adminId);
        game.ApprovePublication(adminId);

        var command = new ArchiveSharedGameCommand(game.Id, archiverId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        SharedGame? capturedGame = null;
        _repositoryMock
            .Setup(r => r.Update(It.IsAny<SharedGame>()))
            .Callback<SharedGame>(g => capturedGame = g);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGame>()),
            Times.Once);
        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        capturedGame.Should().NotBeNull();
        capturedGame.Status.Should().Be(GameStatus.Archived);
        capturedGame.ModifiedBy.Should().Be(archiverId);
        capturedGame.ModifiedAt.Should().NotBeNull();

        // Verify domain event raised (CreatedEvent + SubmittedForApprovalEvent + PublicationApprovedEvent + ArchivedEvent)
        capturedGame.DomainEvents.Count.Should().Be(4);
        var archivedEvent = capturedGame.DomainEvents.Last().Should().BeOfType<SharedGameArchivedEvent>().Subject;
        archivedEvent.GameId.Should().Be(game.Id);
        archivedEvent.ArchivedBy.Should().Be(archiverId);
    }

    [Fact]
    public async Task Handle_WithNonExistentGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new ArchiveSharedGameCommand(Guid.NewGuid(), Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(command.GameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGame?)null);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Handle_WithAlreadyArchivedGame_ThrowsInvalidOperationException()
    {
        // Arrange
        var game = CreateArchivedGame();
        var command = new ArchiveSharedGameCommand(game.Id, Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(game.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(game);

        // Act & Assert
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private static SharedGame CreateArchivedGame()
    {
        var game = SharedGame.Create(
            "Test Game",
            2024,
            "Description",
            2,
            4,
            60,
            10,
            null,
            null,
            "https://example.com/image.jpg",
            "https://example.com/thumb.jpg",
            null,
            Guid.NewGuid());

        var adminId = Guid.NewGuid();
        game.SubmitForApproval(adminId);
        game.ApprovePublication(adminId);
        game.Archive(Guid.NewGuid());
        return game;
    }
}