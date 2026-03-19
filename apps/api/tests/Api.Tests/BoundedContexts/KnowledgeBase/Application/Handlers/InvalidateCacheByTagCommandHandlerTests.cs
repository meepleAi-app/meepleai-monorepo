using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for InvalidateCacheByTagCommandHandler.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class InvalidateCacheByTagCommandHandlerTests
{
    private readonly Mock<IHybridCacheService> _mockHybridCache;
    private readonly Mock<ILogger<InvalidateCacheByTagCommandHandler>> _mockLogger;
    private readonly InvalidateCacheByTagCommandHandler _handler;

    public InvalidateCacheByTagCommandHandlerTests()
    {
        _mockHybridCache = new Mock<IHybridCacheService>();
        _mockLogger = new Mock<ILogger<InvalidateCacheByTagCommandHandler>>();
        _handler = new InvalidateCacheByTagCommandHandler(_mockHybridCache.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Handle_ValidTag_InvalidatesCache()
    {
        // Arrange
        var tag = "game:chess";
        var removedCount = 15;

        _mockHybridCache.Setup(c => c.RemoveByTagAsync(tag, It.IsAny<CancellationToken>()))
            .ReturnsAsync(removedCount);

        var command = new InvalidateCacheByTagCommand(tag);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockHybridCache.Verify(c => c.RemoveByTagAsync(tag, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_EmptyTag_ThrowsArgumentException()
    {
        // Arrange
        var command = new InvalidateCacheByTagCommand("");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("Tag cannot be empty", exception.Message);

        _mockHybridCache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhitespaceTag_ThrowsArgumentException()
    {
        // Arrange
        var command = new InvalidateCacheByTagCommand("   ");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => _handler.Handle(command, TestContext.Current.CancellationToken));

        Assert.Contains("Tag cannot be empty", exception.Message);

        _mockHybridCache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NoEntriesRemoved_CompletesSuccessfully()
    {
        // Arrange
        var tag = "game:nonexistent";
        var removedCount = 0;

        _mockHybridCache.Setup(c => c.RemoveByTagAsync(tag, It.IsAny<CancellationToken>()))
            .ReturnsAsync(removedCount);

        var command = new InvalidateCacheByTagCommand(tag);

        // Act
        await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        _mockHybridCache.Verify(c => c.RemoveByTagAsync(tag, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
