using Api.BoundedContexts.DatabaseSync.Application.Commands;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application.Commands;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DatabaseSync")]
public sealed class TunnelCommandHandlerTests
{
    private readonly Mock<ISshTunnelClient> _tunnelClient = new();

    // ── OpenTunnelHandler ──────────────────────────────────────────────────────

    [Fact]
    public void OpenTunnelHandler_Constructor_ThrowsOnNullTunnelClient()
    {
        var act = () => new OpenTunnelHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tunnelClient");
    }

    [Fact]
    public async Task OpenTunnelHandler_Handle_CallsOpenAsync_AndReturnsResult()
    {
        // Arrange
        var expected = new TunnelStatusResult(TunnelState.Open, 0, "Opened");
        _tunnelClient.Setup(c => c.OpenAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new OpenTunnelHandler(_tunnelClient.Object);

        // Act
        var result = await handler.Handle(new OpenTunnelCommand(), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        _tunnelClient.Verify(c => c.OpenAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OpenTunnelHandler_Handle_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var expected = new TunnelStatusResult(TunnelState.Open, 0, null);
        _tunnelClient.Setup(c => c.OpenAsync(cts.Token)).ReturnsAsync(expected);
        var handler = new OpenTunnelHandler(_tunnelClient.Object);

        // Act
        await handler.Handle(new OpenTunnelCommand(), cts.Token);

        // Assert
        _tunnelClient.Verify(c => c.OpenAsync(cts.Token), Times.Once);
    }

    // ── CloseTunnelHandler ─────────────────────────────────────────────────────

    [Fact]
    public void CloseTunnelHandler_Constructor_ThrowsOnNullTunnelClient()
    {
        var act = () => new CloseTunnelHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tunnelClient");
    }

    [Fact]
    public async Task CloseTunnelHandler_Handle_CallsCloseAsync_AndReturnsResult()
    {
        // Arrange
        var expected = new TunnelStatusResult(TunnelState.Closed, 0, "Closed");
        _tunnelClient.Setup(c => c.CloseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new CloseTunnelHandler(_tunnelClient.Object);

        // Act
        var result = await handler.Handle(new CloseTunnelCommand(), CancellationToken.None);

        // Assert
        result.Should().Be(expected);
        _tunnelClient.Verify(c => c.CloseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CloseTunnelHandler_Handle_WhenTunnelAlreadyClosed_ReturnsClosedStatus()
    {
        // Arrange
        var expected = new TunnelStatusResult(TunnelState.Closed, 0, "Already closed");
        _tunnelClient.Setup(c => c.CloseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new CloseTunnelHandler(_tunnelClient.Object);

        // Act
        var result = await handler.Handle(new CloseTunnelCommand(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(TunnelState.Closed);
    }

    [Fact]
    public async Task CloseTunnelHandler_Handle_WhenSidecarFails_ReturnsErrorStatus()
    {
        // Arrange
        var expected = new TunnelStatusResult(TunnelState.Error, 0, "Connection refused");
        _tunnelClient.Setup(c => c.CloseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new CloseTunnelHandler(_tunnelClient.Object);

        // Act
        var result = await handler.Handle(new CloseTunnelCommand(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(TunnelState.Error);
        result.Message.Should().Contain("Connection refused");
    }
}
