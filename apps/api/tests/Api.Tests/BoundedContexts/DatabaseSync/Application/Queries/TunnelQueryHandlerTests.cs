using Api.BoundedContexts.DatabaseSync.Application.Queries;
using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Application.Queries;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DatabaseSync")]
public sealed class TunnelQueryHandlerTests
{
    private readonly Mock<ISshTunnelClient> _tunnelClient = new();

    // ── GetTunnelStatusHandler ─────────────────────────────────────────────────

    [Fact]
    public void GetTunnelStatusHandler_Constructor_ThrowsOnNullTunnelClient()
    {
        var act = () => new GetTunnelStatusHandler(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("tunnelClient");
    }

    [Fact]
    public async Task GetTunnelStatusHandler_Handle_ReturnsOpenStatus_WhenTunnelOpen()
    {
        // Arrange
        var expected = new TunnelStatusResult(TunnelState.Open, 300, "Tunnel operational");
        _tunnelClient.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new GetTunnelStatusHandler(_tunnelClient.Object);

        // Act
        var result = await handler.Handle(new GetTunnelStatusQuery(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(TunnelState.Open);
        result.UptimeSeconds.Should().Be(300);
        _tunnelClient.Verify(c => c.GetStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTunnelStatusHandler_Handle_ReturnsClosedStatus_WhenTunnelClosed()
    {
        // Arrange
        var expected = new TunnelStatusResult(TunnelState.Closed, 0, null);
        _tunnelClient.Setup(c => c.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var handler = new GetTunnelStatusHandler(_tunnelClient.Object);

        // Act
        var result = await handler.Handle(new GetTunnelStatusQuery(), CancellationToken.None);

        // Assert
        result.Status.Should().Be(TunnelState.Closed);
    }

    [Fact]
    public async Task GetTunnelStatusHandler_Handle_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var expected = new TunnelStatusResult(TunnelState.Open, 0, null);
        _tunnelClient.Setup(c => c.GetStatusAsync(cts.Token)).ReturnsAsync(expected);
        var handler = new GetTunnelStatusHandler(_tunnelClient.Object);

        // Act
        await handler.Handle(new GetTunnelStatusQuery(), cts.Token);

        // Assert
        _tunnelClient.Verify(c => c.GetStatusAsync(cts.Token), Times.Once);
    }
}
