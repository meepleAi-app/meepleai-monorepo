using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
public class SetActiveDocumentVersionCommandHandlerTests
{
    private readonly Mock<ISharedGameDocumentRepository> _repositoryMock;
    private readonly Mock<DocumentVersioningService> _versioningServiceMock;
    private readonly Mock<IUnitOfWork> _unitOfWorkMock;
    private readonly Mock<IMediator> _mediatorMock;
    private readonly Mock<ILogger<SetActiveDocumentVersionCommandHandler>> _loggerMock;
    private readonly SetActiveDocumentVersionCommandHandler _handler;

    public SetActiveDocumentVersionCommandHandlerTests()
    {
        _repositoryMock = new Mock<ISharedGameDocumentRepository>();
        _versioningServiceMock = new Mock<DocumentVersioningService>(_repositoryMock.Object);
        _unitOfWorkMock = new Mock<IUnitOfWork>();
        _mediatorMock = new Mock<IMediator>();
        _loggerMock = new Mock<ILogger<SetActiveDocumentVersionCommandHandler>>();

        _handler = new SetActiveDocumentVersionCommandHandler(
            _repositoryMock.Object,
            _versioningServiceMock.Object,
            _unitOfWorkMock.Object,
            _mediatorMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SetsDocumentAsActive()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var document = CreateTestDocument(documentId, gameId, isActive: false);

        var command = new SetActiveDocumentVersionCommand(gameId, documentId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _versioningServiceMock
            .Setup(s => s.SetActiveVersionAsync(
                It.IsAny<SharedGameDocument>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _versioningServiceMock.Verify(
            s => s.SetActiveVersionAsync(document, It.IsAny<CancellationToken>()),
            Times.Once);

        _repositoryMock.Verify(
            r => r.Update(document),
            Times.Once);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Once);

        _mediatorMock.Verify(
            m => m.Publish(It.IsAny<SharedGameDocumentActivatedEvent>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNonExistentDocument_ThrowsException()
    {
        // Arrange
        var command = new SetActiveDocumentVersionCommand(Guid.NewGuid(), Guid.NewGuid());

        _repositoryMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGameDocument?)null);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WithDocumentFromDifferentGame_ThrowsException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var otherGameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var document = CreateTestDocument(documentId, otherGameId, isActive: false);

        var command = new SetActiveDocumentVersionCommand(gameId, documentId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));
        Assert.Contains("does not belong to game", ex.Message);
    }

    [Fact]
    public async Task Handle_WithAlreadyActiveDocument_DoesNothing()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var document = CreateTestDocument(documentId, gameId, isActive: true);

        var command = new SetActiveDocumentVersionCommand(gameId, documentId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - versioning service should NOT be called
        _versioningServiceMock.Verify(
            s => s.SetActiveVersionAsync(It.IsAny<SharedGameDocument>(), It.IsAny<CancellationToken>()),
            Times.Never);

        _repositoryMock.Verify(
            r => r.Update(It.IsAny<SharedGameDocument>()),
            Times.Never);

        _unitOfWorkMock.Verify(
            u => u.SaveChangesAsync(It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_WhenRaceConditionOccurs_ThrowsInformativeException()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var document = CreateTestDocument(documentId, gameId, isActive: false);

        var command = new SetActiveDocumentVersionCommand(gameId, documentId);

        _repositoryMock
            .Setup(r => r.GetByIdAsync(documentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _versioningServiceMock
            .Setup(s => s.SetActiveVersionAsync(
                It.IsAny<SharedGameDocument>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Simulate PostgreSQL unique constraint violation (race condition)
        var pgException = new PostgresException(
            messageText: "duplicate key value violates unique constraint \"ix_shared_game_documents_single_active\"",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: "23505"); // unique_violation

        var dbUpdateException = new DbUpdateException(
            "An error occurred while saving the entity changes.",
            pgException);

        _unitOfWorkMock
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(dbUpdateException);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("activated concurrently", ex.Message);
        Assert.Contains("Please refresh and try again", ex.Message);
        Assert.Same(dbUpdateException, ex.InnerException);
    }

    private static SharedGameDocument CreateTestDocument(Guid id, Guid gameId, bool isActive)
    {
        var doc = SharedGameDocument.Create(
            gameId,
            Guid.NewGuid(),
            SharedGameDocumentType.Rulebook,
            "1.0",
            Guid.NewGuid());

        // If document should be active, activate it
        if (isActive)
        {
            doc.SetAsActive();
        }

        return doc;
    }
}
