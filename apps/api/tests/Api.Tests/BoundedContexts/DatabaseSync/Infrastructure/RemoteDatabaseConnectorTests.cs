using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DatabaseSync.Infrastructure;

/// <summary>
/// Unit tests for RemoteDatabaseConnector – verifies tunnel state gating
/// before attempting to open a Npgsql connection.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DatabaseSync")]
public class RemoteDatabaseConnectorTests
{
    [Fact]
    public async Task OpenConnectionAsync_ThrowsInvalidOperation_WhenTunnelNotOpen()
    {
        var tunnelClient = new Mock<ISshTunnelClient>();
        tunnelClient.Setup(t => t.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TunnelStatusResult(TunnelState.Closed, 0, null));

        var connector = new RemoteDatabaseConnector(
            tunnelClient.Object,
            "Host=localhost;Port=15432;Database=test;Username=test;Password=test",
            NullLogger<RemoteDatabaseConnector>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => connector.OpenConnectionAsync());
    }

    [Fact]
    public async Task OpenConnectionAsync_ThrowsInvalidOperation_WhenTunnelInError()
    {
        var tunnelClient = new Mock<ISshTunnelClient>();
        tunnelClient.Setup(t => t.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TunnelStatusResult(TunnelState.Error, 0, "SSH failed"));

        var connector = new RemoteDatabaseConnector(
            tunnelClient.Object,
            "Host=localhost;Port=15432;Database=test;Username=test;Password=test",
            NullLogger<RemoteDatabaseConnector>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => connector.OpenConnectionAsync());
        Assert.Contains("Error", ex.Message);
    }

    [Fact]
    public async Task OpenConnectionAsync_ThrowsInvalidOperation_WhenTunnelOpening()
    {
        var tunnelClient = new Mock<ISshTunnelClient>();
        tunnelClient.Setup(t => t.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TunnelStatusResult(TunnelState.Opening, 0, "Connecting..."));

        var connector = new RemoteDatabaseConnector(
            tunnelClient.Object,
            "Host=localhost;Port=15432;Database=test;Username=test;Password=test",
            NullLogger<RemoteDatabaseConnector>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => connector.OpenConnectionAsync());
        Assert.Contains("Opening", ex.Message);
    }

    [Fact]
    public void Constructor_ThrowsArgumentNull_WhenTunnelClientIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => new RemoteDatabaseConnector(
            null!,
            "Host=localhost;Port=15432;Database=test;Username=test;Password=test",
            NullLogger<RemoteDatabaseConnector>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNull_WhenConnectionStringIsNull()
    {
        var tunnelClient = new Mock<ISshTunnelClient>();

        Assert.Throws<ArgumentNullException>(() => new RemoteDatabaseConnector(
            tunnelClient.Object,
            null!,
            NullLogger<RemoteDatabaseConnector>.Instance));
    }

    [Fact]
    public void Constructor_ThrowsArgumentNull_WhenLoggerIsNull()
    {
        var tunnelClient = new Mock<ISshTunnelClient>();

        Assert.Throws<ArgumentNullException>(() => new RemoteDatabaseConnector(
            tunnelClient.Object,
            "Host=localhost;Port=15432;Database=test;Username=test;Password=test",
            null!));
    }
}
