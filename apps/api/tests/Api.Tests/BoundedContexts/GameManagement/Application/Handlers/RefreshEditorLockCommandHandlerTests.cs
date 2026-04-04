using Api.BoundedContexts.GameManagement.Application.Commands;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Application.Services;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Tests for RefreshEditorLockCommandHandler.
/// Issue #2055: Tests editor lock refresh for RuleSpec editing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class RefreshEditorLockCommandHandlerTests
{
    private readonly Mock<IEditorLockService> _lockServiceMock;
    private readonly RefreshEditorLockCommandHandler _handler;

    public RefreshEditorLockCommandHandlerTests()
    {
        _lockServiceMock = new Mock<IEditorLockService>();
        _handler = new RefreshEditorLockCommandHandler(_lockServiceMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidLock_RefreshesSuccessfully()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RefreshEditorLockCommand(gameId, userId);

        _lockServiceMock
            .Setup(s => s.RefreshLockAsync(gameId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (result).Should().BeTrue();
        _lockServiceMock.Verify(
            s => s.RefreshLockAsync(gameId, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotHoldLock_ReturnsFalse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RefreshEditorLockCommand(gameId, userId);

        _lockServiceMock
            .Setup(s => s.RefreshLockAsync(gameId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (result).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenLockExpired_ReturnsFalse()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RefreshEditorLockCommand(gameId, userId);

        _lockServiceMock
            .Setup(s => s.RefreshLockAsync(gameId, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        (result).Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesToService()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var command = new RefreshEditorLockCommand(gameId, userId);

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _lockServiceMock
            .Setup(s => s.RefreshLockAsync(gameId, userId, token))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(command, token);

        // Assert
        _lockServiceMock.Verify(s => s.RefreshLockAsync(gameId, userId, token), Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act =
            () => _handler.Handle(null!, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
