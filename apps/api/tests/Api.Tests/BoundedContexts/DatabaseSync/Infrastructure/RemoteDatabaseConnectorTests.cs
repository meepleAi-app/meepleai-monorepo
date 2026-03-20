using Api.BoundedContexts.DatabaseSync.Domain.Enums;
using Api.BoundedContexts.DatabaseSync.Domain.Interfaces;
using Api.BoundedContexts.DatabaseSync.Domain.Models;
using Api.BoundedContexts.DatabaseSync.Infrastructure;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;

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

        var act = () => connector.OpenConnectionAsync();
        await act.Should().ThrowAsync<InvalidOperationException>();
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

        var act2 = () => connector.OpenConnectionAsync();
        var ex = (await act2.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Contain("Error");
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

        var act3 = () => connector.OpenConnectionAsync();
        var ex = (await act3.Should().ThrowAsync<InvalidOperationException>()).Which;
        ex.Message.Should().Contain("Opening");
    }

    [Fact]
    public void Constructor_ThrowsArgumentNull_WhenTunnelClientIsNull()
    {
        var act4 = () => new RemoteDatabaseConnector(
            null!,
            "Host=localhost;Port=15432;Database=test;Username=test;Password=test",
            NullLogger<RemoteDatabaseConnector>.Instance);
        act4.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsArgumentNull_WhenConnectionStringIsNull()
    {
        var tunnelClient = new Mock<ISshTunnelClient>();

        var act5 = () => new RemoteDatabaseConnector(
            tunnelClient.Object,
            null!,
            NullLogger<RemoteDatabaseConnector>.Instance);
        act5.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ThrowsArgumentNull_WhenLoggerIsNull()
    {
        var tunnelClient = new Mock<ISshTunnelClient>();

        var act6 = () => new RemoteDatabaseConnector(
            tunnelClient.Object,
            "Host=localhost;Port=15432;Database=test;Username=test;Password=test",
            null!);
        act6.Should().Throw<ArgumentNullException>();
    }
}
