using Api.BoundedContexts.KnowledgeBase.Application.Commands.RemoveDocumentFromKb;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
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
    private readonly IVectorDocumentRepository _repo;
    private readonly ILogger<RemoveDocumentFromKbCommandHandler> _logger;
    private readonly RemoveDocumentFromKbCommandHandler _handler;

    public RemoveDocumentFromKbCommandHandlerTests()
    {
        _repo = Substitute.For<IVectorDocumentRepository>();
        _logger = Substitute.For<ILogger<RemoveDocumentFromKbCommandHandler>>();
        _handler = new RemoveDocumentFromKbCommandHandler(_repo, _logger);
    }

    [Fact]
    public async Task Handle_WhenDocumentNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var vectorDocId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        _repo.GetByIdAsync(vectorDocId, Arg.Any<CancellationToken>())
            .Returns((VectorDocument?)null);

        var command = new RemoveDocumentFromKbCommand(vectorDocId, gameId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        await _repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDocumentBelongsToGame_CallsDeleteOnce()
    {
        // Arrange
        var vectorDocId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var vectorDoc = new VectorDocument(vectorDocId, gameId, Guid.NewGuid(), "en", 10);

        _repo.GetByIdAsync(vectorDocId, Arg.Any<CancellationToken>())
            .Returns(vectorDoc);

        var command = new RemoveDocumentFromKbCommand(vectorDocId, gameId);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        await _repo.Received(1).DeleteAsync(vectorDocId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDocumentBelongsToDifferentGame_ThrowsNotFoundException()
    {
        // Arrange
        var vectorDocId = Guid.NewGuid();
        var ownerGameId = Guid.NewGuid();
        var requestedGameId = Guid.NewGuid(); // different game

        var vectorDoc = new VectorDocument(vectorDocId, ownerGameId, Guid.NewGuid(), "it", 5);

        _repo.GetByIdAsync(vectorDocId, Arg.Any<CancellationToken>())
            .Returns(vectorDoc);

        var command = new RemoveDocumentFromKbCommand(vectorDocId, requestedGameId);

        // Act
        var act = () => _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
        await _repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
