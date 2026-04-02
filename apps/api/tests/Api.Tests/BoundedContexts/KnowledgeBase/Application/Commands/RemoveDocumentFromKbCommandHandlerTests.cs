using Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Unit tests for RemoveDocumentFromKbCommandHandler.
/// KB-02: Admin per-game KB document removal.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class RemoveDocumentFromKbCommandHandlerTests
{
    private readonly Mock<IVectorDocumentRepository> _repoMock;
    private readonly Mock<ILogger<RemoveDocumentFromKbCommandHandler>> _loggerMock;
    private readonly RemoveDocumentFromKbCommandHandler _handler;

    public RemoveDocumentFromKbCommandHandlerTests()
    {
        _repoMock = new Mock<IVectorDocumentRepository>();
        _loggerMock = new Mock<ILogger<RemoveDocumentFromKbCommandHandler>>();
        _handler = new RemoveDocumentFromKbCommandHandler(_repoMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WhenDocumentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var vectorDocId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _repoMock
            .Setup(r => r.GetByIdAsync(vectorDocId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VectorDocument?)null);

        var command = new RemoveDocumentFromKbCommand(vectorDocId, gameId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenDocumentBelongsToGame_CallsDeleteOnce()
    {
        // Arrange
        var vectorDocId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(vectorDocId, gameId, Guid.NewGuid(), "en", 10);

        _repoMock
            .Setup(r => r.GetByIdAsync(vectorDocId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);

        var command = new RemoveDocumentFromKbCommand(vectorDocId, gameId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _repoMock.Verify(r => r.DeleteAsync(vectorDocId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDocumentBelongsToDifferentGame_ThrowsNotFoundException()
    {
        // Arrange
        var vectorDocId = Guid.NewGuid();
        var ownerGameId = Guid.NewGuid();
        var requestedGameId = Guid.NewGuid(); // different game

        var vectorDoc = new VectorDocument(vectorDocId, ownerGameId, Guid.NewGuid(), "it", 5);

        _repoMock
            .Setup(r => r.GetByIdAsync(vectorDocId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vectorDoc);

        var command = new RemoveDocumentFromKbCommand(vectorDocId, requestedGameId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        _repoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
