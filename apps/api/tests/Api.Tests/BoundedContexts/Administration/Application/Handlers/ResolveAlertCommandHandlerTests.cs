using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Handlers;
using Api.Services;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for ResolveAlertCommandHandler.
/// Tests handler delegation to AlertingService for resolving alerts.
/// </summary>
public class ResolveAlertCommandHandlerTests
{
    private readonly Mock<IAlertingService> _mockAlertingService;
    private readonly ResolveAlertCommandHandler _handler;

    public ResolveAlertCommandHandlerTests()
    {
        _mockAlertingService = new Mock<IAlertingService>();
        _handler = new ResolveAlertCommandHandler(_mockAlertingService.Object);
    }

    [Fact]
    public async Task Handle_WithValidAlertType_ResolvesAlert()
    {
        // Arrange
        var command = new ResolveAlertCommand("DatabaseError");
        _mockAlertingService
            .Setup(s => s.ResolveAlertAsync("DatabaseError", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.True(result);
        _mockAlertingService.Verify(
            s => s.ResolveAlertAsync("DatabaseError", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenAlertNotFound_ReturnsFalse()
    {
        // Arrange
        var command = new ResolveAlertCommand("NonExistentAlert");
        _mockAlertingService
            .Setup(s => s.ResolveAlertAsync("NonExistentAlert", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        Assert.False(result);
        _mockAlertingService.Verify(
            s => s.ResolveAlertAsync("NonExistentAlert", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_PassesTokenToService()
    {
        // Arrange
        var command = new ResolveAlertCommand("ApiError");
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockAlertingService
            .Setup(s => s.ResolveAlertAsync("ApiError", cancellationToken))
            .ReturnsAsync(true);

        // Act
        await _handler.Handle(command, cancellationToken);

        // Assert
        _mockAlertingService.Verify(
            s => s.ResolveAlertAsync("ApiError", cancellationToken),
            Times.Once);
    }
}
