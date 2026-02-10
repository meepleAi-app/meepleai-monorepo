using Api.BoundedContexts.Administration.Application.EventHandlers;
using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Issue #3974: Unit tests for UserActivityCacheInvalidationEventHandler.
/// Verifies tag-based cache invalidation for user activity events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UserActivityCacheInvalidationEventHandlerTests
{
    private readonly Mock<IHybridCacheService> _cacheMock;
    private readonly Mock<ILogger<UserActivityCacheInvalidationEventHandler>> _loggerMock;
    private readonly UserActivityCacheInvalidationEventHandler _handler;

    public UserActivityCacheInvalidationEventHandlerTests()
    {
        _cacheMock = new Mock<IHybridCacheService>();
        _loggerMock = new Mock<ILogger<UserActivityCacheInvalidationEventHandler>>();
        _handler = new UserActivityCacheInvalidationEventHandler(_cacheMock.Object, _loggerMock.Object);

        // Default: RemoveByTagAsync succeeds and returns 3 removed entries
        _cacheMock
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
    }

    [Fact]
    public async Task Handle_UserLibraryGameAddedEvent_InvalidatesUserCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var evt = new UserLibraryGameAddedEvent(userId, gameId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync($"user:{userId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate cache by user tag when game added to library");
    }

    [Fact]
    public async Task Handle_UserGameSessionCompletedEvent_InvalidatesUserCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var evt = new UserGameSessionCompletedEvent(userId, sessionId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync($"user:{userId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate cache by user tag when game session completed");
    }

    [Fact]
    public async Task Handle_UserChatSavedEvent_InvalidatesUserCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var chatId = Guid.NewGuid();
        var evt = new UserChatSavedEvent(userId, chatId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync($"user:{userId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate cache by user tag when chat saved");
    }

    [Fact]
    public async Task Handle_UserWishlistUpdatedEvent_InvalidatesUserCache()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var evt = new UserWishlistUpdatedEvent(userId, gameId);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync($"user:{userId}", It.IsAny<CancellationToken>()),
            Times.Once,
            "Should invalidate cache by user tag when wishlist updated");
    }

    [Fact]
    public async Task Handle_SessionFinalizedEvent_InvalidatesActivityTimelineTag()
    {
        // Arrange
        var evt = new SessionFinalizedEvent
        {
            SessionId = Guid.NewGuid(),
            DurationMinutes = 60
        };

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveByTagAsync("activity-timeline", It.IsAny<CancellationToken>()),
            Times.Once,
            "SessionFinalizedEvent should invalidate activity-timeline tag (no UserId available)");
    }

    [Fact]
    public async Task Handle_CacheThrows_LogsErrorButDoesNotThrow()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var evt = new UserLibraryGameAddedEvent(userId, Guid.NewGuid());

        _cacheMock
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Redis connection failed"));

        // Act
        var exception = await Record.ExceptionAsync(() => _handler.Handle(evt, CancellationToken.None));

        // Assert
        Assert.Null(exception);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate dashboard cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log error when cache invalidation fails");
    }

    [Fact]
    public async Task Handle_SessionFinalizedCacheThrows_LogsErrorButDoesNotThrow()
    {
        // Arrange
        var evt = new SessionFinalizedEvent
        {
            SessionId = Guid.NewGuid(),
            DurationMinutes = 30
        };

        _cacheMock
            .Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache unavailable"));

        // Act
        var exception = await Record.ExceptionAsync(() => _handler.Handle(evt, CancellationToken.None));

        // Assert
        Assert.Null(exception);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate cache after session finalized")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_LogsInformationOnSuccess()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var evt = new UserLibraryGameAddedEvent(userId, Guid.NewGuid());

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Dashboard cache invalidated")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log cache invalidation result");
    }

    [Fact]
    public async Task Handle_UsesCorrectUserTag_ForDifferentUsers()
    {
        // Arrange
        var userId1 = Guid.NewGuid();
        var userId2 = Guid.NewGuid();
        var evt1 = new UserLibraryGameAddedEvent(userId1, Guid.NewGuid());
        var evt2 = new UserChatSavedEvent(userId2, Guid.NewGuid());

        // Act
        await _handler.Handle(evt1, CancellationToken.None);
        await _handler.Handle(evt2, CancellationToken.None);

        // Assert
        _cacheMock.Verify(c => c.RemoveByTagAsync($"user:{userId1}", It.IsAny<CancellationToken>()), Times.Once);
        _cacheMock.Verify(c => c.RemoveByTagAsync($"user:{userId2}", It.IsAny<CancellationToken>()), Times.Once);
    }
}
