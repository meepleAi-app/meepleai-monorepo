using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Services;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for AcquireEditorLockCommandHandler.
/// Issue #2055: Tests editor lock acquisition for RuleSpec editing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class AcquireEditorLockCommandHandlerTests
{
    private readonly Mock<IEditorLockService> _lockServiceMock;
    private readonly AcquireEditorLockCommandHandler _handler;

    public AcquireEditorLockCommandHandlerTests()
    {
        _lockServiceMock = new Mock<IEditorLockService>();
        _handler = new AcquireEditorLockCommandHandler(_lockServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CallsLockService()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userEmail = "user@example.com";
        var command = new AcquireEditorLockCommand(gameId, userId, userEmail);

        var expectedResult = new EditorLockResult(
            Success: true,
            LockStatus: new EditorLockDto(
                GameId: gameId,
                LockedByUserId: userId,
                LockedByUserEmail: "u***@e***.com",
                LockedAt: DateTime.UtcNow,
                ExpiresAt: DateTime.UtcNow.AddMinutes(5),
                IsLocked: true,
                IsCurrentUserLock: true),
            Message: "Lock acquired successfully"
        );

        _lockServiceMock
            .Setup(s => s.AcquireLockAsync(gameId, userId, userEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.NotNull(result.LockStatus);
        Assert.Equal(gameId, result.LockStatus.GameId);
        Assert.True(result.LockStatus.IsCurrentUserLock);

        _lockServiceMock.Verify(
            s => s.AcquireLockAsync(gameId, userId, userEmail, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenLockHeldByOtherUser_ReturnsFailure()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var userEmail = "user@example.com";
        var command = new AcquireEditorLockCommand(gameId, userId, userEmail);

        var expectedResult = new EditorLockResult(
            Success: false,
            LockStatus: new EditorLockDto(
                GameId: gameId,
                LockedByUserId: otherUserId,
                LockedByUserEmail: "o***@e***.com",
                LockedAt: DateTime.UtcNow.AddMinutes(-2),
                ExpiresAt: DateTime.UtcNow.AddMinutes(3),
                IsLocked: true,
                IsCurrentUserLock: false),
            Message: "RuleSpec is being edited by o***@e***.com. Try again later."
        );

        _lockServiceMock
            .Setup(s => s.AcquireLockAsync(gameId, userId, userEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Success);
        Assert.NotNull(result.LockStatus);
        Assert.True(result.LockStatus.IsLocked);
        Assert.False(result.LockStatus.IsCurrentUserLock);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToService()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userEmail = "user@example.com";
        var command = new AcquireEditorLockCommand(gameId, userId, userEmail);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var expectedResult = new EditorLockResult(
            Success: true,
            LockStatus: new EditorLockDto(
                GameId: gameId,
                LockedByUserId: userId,
                LockedByUserEmail: "u***@e***.com",
                LockedAt: DateTime.UtcNow,
                ExpiresAt: DateTime.UtcNow.AddMinutes(5),
                IsLocked: true,
                IsCurrentUserLock: true),
            Message: null
        );

        _lockServiceMock
            .Setup(s => s.AcquireLockAsync(gameId, userId, userEmail, token))
            .ReturnsAsync(expectedResult);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _lockServiceMock.Verify(s => s.AcquireLockAsync(gameId, userId, userEmail, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _handler.Handle(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyHoldsLock_RefreshesAndSucceeds()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var userEmail = "user@example.com";
        var command = new AcquireEditorLockCommand(gameId, userId, userEmail);

        var expectedResult = new EditorLockResult(
            Success: true,
            LockStatus: new EditorLockDto(
                GameId: gameId,
                LockedByUserId: userId,
                LockedByUserEmail: "u***@e***.com",
                LockedAt: DateTime.UtcNow.AddMinutes(-2),
                ExpiresAt: DateTime.UtcNow.AddMinutes(5),
                IsLocked: true,
                IsCurrentUserLock: true),
            Message: "Lock refreshed"
        );

        _lockServiceMock
            .Setup(s => s.AcquireLockAsync(gameId, userId, userEmail, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Lock refreshed", result.Message);
    }
}
