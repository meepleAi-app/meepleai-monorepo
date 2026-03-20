using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Services;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for GetEditorLockStatusQueryHandler.
/// Issue #2055: Tests editor lock status retrieval.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetEditorLockStatusQueryHandlerTests
{
    private readonly Mock<IEditorLockService> _lockServiceMock;
    private readonly GetEditorLockStatusQueryHandler _handler;

    public GetEditorLockStatusQueryHandlerTests()
    {
        _lockServiceMock = new Mock<IEditorLockService>();
        _handler = new GetEditorLockStatusQueryHandler(_lockServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WhenLockExists_ReturnsLockStatus()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var lockHolderId = Guid.NewGuid();
        var query = new GetEditorLockStatusQuery(gameId, currentUserId);

        var expectedDto = new EditorLockDto(
            GameId: gameId,
            LockedByUserId: lockHolderId,
            LockedByUserEmail: "u***@e***.com",
            LockedAt: DateTime.UtcNow.AddMinutes(-2),
            ExpiresAt: DateTime.UtcNow.AddMinutes(3),
            IsLocked: true,
            IsCurrentUserLock: false
        );

        _lockServiceMock
            .Setup(s => s.GetLockStatusAsync(gameId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        result.GameId.Should().Be(gameId);
        Assert.True(result.IsLocked);
        Assert.False(result.IsCurrentUserLock);
        result.LockedByUserId.Should().Be(lockHolderId);
    }

    [Fact]
    public async Task Handle_WhenNoLockExists_ReturnsUnlockedStatus()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var query = new GetEditorLockStatusQuery(gameId, currentUserId);

        var expectedDto = new EditorLockDto(
            GameId: gameId,
            LockedByUserId: null,
            LockedByUserEmail: null,
            LockedAt: null,
            ExpiresAt: null,
            IsLocked: false,
            IsCurrentUserLock: false
        );

        _lockServiceMock
            .Setup(s => s.GetLockStatusAsync(gameId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        result.GameId.Should().Be(gameId);
        Assert.False(result.IsLocked);
        Assert.Null(result.LockedByUserId);
    }

    [Fact]
    public async Task Handle_WhenCurrentUserHoldsLock_ReturnsCurrentUserLockTrue()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var query = new GetEditorLockStatusQuery(gameId, currentUserId);

        var expectedDto = new EditorLockDto(
            GameId: gameId,
            LockedByUserId: currentUserId,
            LockedByUserEmail: "c***@e***.com",
            LockedAt: DateTime.UtcNow.AddMinutes(-1),
            ExpiresAt: DateTime.UtcNow.AddMinutes(4),
            IsLocked: true,
            IsCurrentUserLock: true
        );

        _lockServiceMock
            .Setup(s => s.GetLockStatusAsync(gameId, currentUserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedDto);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.IsLocked);
        Assert.True(result.IsCurrentUserLock);
        result.LockedByUserId.Should().Be(currentUserId);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToService()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var currentUserId = Guid.NewGuid();
        var query = new GetEditorLockStatusQuery(gameId, currentUserId);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var expectedDto = new EditorLockDto(
            GameId: gameId,
            LockedByUserId: null,
            LockedByUserEmail: null,
            LockedAt: null,
            ExpiresAt: null,
            IsLocked: false,
            IsCurrentUserLock: false
        );

        _lockServiceMock
            .Setup(s => s.GetLockStatusAsync(gameId, currentUserId, token))
            .ReturnsAsync(expectedDto);

        // Act
        await _handler.Handle(query, token);

        // Assert
        _lockServiceMock.Verify(s => s.GetLockStatusAsync(gameId, currentUserId, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }
}
