using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Services;
using Moq;
using Xunit;
using FluentAssertions;
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
        result.Should().NotBeNull();
        (result.Success).Should().BeTrue();
        result.LockStatus.Should().NotBeNull();
        result.LockStatus.GameId.Should().Be(gameId);
        (result.LockStatus.IsCurrentUserLock).Should().BeTrue();

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
        result.Should().NotBeNull();
        (result.Success).Should().BeFalse();
        result.LockStatus.Should().NotBeNull();
        (result.LockStatus.IsLocked).Should().BeTrue();
        (result.LockStatus.IsCurrentUserLock).Should().BeFalse();
        result.Message.Should().NotBeNull();
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
        var act = 
            () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
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
        result.Should().NotBeNull();
        (result.Success).Should().BeTrue();
        result.Message.Should().Be("Lock refreshed");
    }
}
