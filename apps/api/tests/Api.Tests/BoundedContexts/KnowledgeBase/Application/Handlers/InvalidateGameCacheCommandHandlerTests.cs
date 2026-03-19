using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for InvalidateGameCacheCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class InvalidateGameCacheCommandHandlerTests
{
    private readonly Mock<IHybridCacheService> _mockHybridCache;
    private readonly Mock<ILogger<InvalidateGameCacheCommandHandler>> _mockLogger;
    private readonly InvalidateGameCacheCommandHandler _handler;

    public InvalidateGameCacheCommandHandlerTests()
    {
        _mockHybridCache = new Mock<IHybridCacheService>();
        _mockLogger = new Mock<ILogger<InvalidateGameCacheCommandHandler>>();
        _handler = new InvalidateGameCacheCommandHandler(_mockHybridCache.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidGameId_InvalidatesCacheWithCorrectTag()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var expectedTag = $"game:{gameId}";
        var removedCount = 42;

        _mockHybridCache.Setup(c => c.RemoveByTagAsync(expectedTag, It.IsAny<CancellationToken>()))
            .ReturnsAsync(removedCount);

        var command = new InvalidateGameCacheCommand(gameId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockHybridCache.Verify(c => c.RemoveByTagAsync(expectedTag, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoEntriesForGame_IsIdempotent()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var expectedTag = $"game:{gameId}";
        var removedCount = 0;

        _mockHybridCache.Setup(c => c.RemoveByTagAsync(expectedTag, It.IsAny<CancellationToken>()))
            .ReturnsAsync(removedCount);

        var command = new InvalidateGameCacheCommand(gameId);

        // Act (should not throw)
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockHybridCache.Verify(c => c.RemoveByTagAsync(expectedTag, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_MultipleEntriesForGame_RemovesAll()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var expectedTag = $"game:{gameId}";
        var removedCount = 150;

        _mockHybridCache.Setup(c => c.RemoveByTagAsync(expectedTag, It.IsAny<CancellationToken>()))
            .ReturnsAsync(removedCount);

        var command = new InvalidateGameCacheCommand(gameId);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockHybridCache.Verify(c => c.RemoveByTagAsync(expectedTag, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
