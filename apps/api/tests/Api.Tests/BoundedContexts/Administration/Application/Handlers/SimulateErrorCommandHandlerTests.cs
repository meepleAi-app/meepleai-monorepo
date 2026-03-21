using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Tests.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for SimulateErrorCommandHandler.
/// Issue #2004: Runbook validation test endpoint.
/// Tests error simulation for Prometheus alert testing.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class SimulateErrorCommandHandlerTests
{
    private readonly Mock<ILogger<SimulateErrorCommandHandler>> _mockLogger;
    private readonly IConfiguration _configuration;
    private readonly SimulateErrorCommandHandler _handler;

    public SimulateErrorCommandHandlerTests()
    {
        _mockLogger = new Mock<ILogger<SimulateErrorCommandHandler>>();

        // Use in-memory configuration (Moq cannot mock extension methods)
        var configData = new Dictionary<string, string?>
        {
            { "TestEndpoints:Enabled", "true" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        _handler = new SimulateErrorCommandHandler(_mockLogger.Object, _configuration);
    }

    [Fact]
    public async Task Handle_WithErrorType500_ThrowsInvalidOperationException()
    {
        // Arrange
        var command = new SimulateErrorCommand("500");

        // Act & Assert
        var act = async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Contain("Simulated 500 Internal Server Error");
        exception.Message.Should().Contain("test endpoint");

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Simulating error type: 500")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithErrorType400_ThrowsArgumentException()
    {
        // Arrange
        var command = new SimulateErrorCommand("400");

        // Act & Assert
        var act = async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().Contain("Simulated 400 Bad Request");
        exception.Message.Should().Contain("test endpoint");
    }

    [Fact]
    public async Task Handle_WithErrorTypeException_ThrowsApplicationException()
    {
        // Arrange
        var command = new SimulateErrorCommand("exception");

        // Act & Assert
        var act = async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ApplicationException>()).Which;

        exception.Message.Should().Contain("Simulated unhandled exception");
        exception.Message.Should().Contain("test endpoint");
    }

    [Fact]
    public async Task Handle_WithErrorTypeTimeout_ThrowsTimeoutException()
    {
        // Arrange
        var command = new SimulateErrorCommand("timeout");
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var act = async () =>
            await _handler.Handle(command, cts.Token);
        await act.Should().ThrowAsync<Exception>();
    }

    [Fact]
    public async Task Handle_WithInvalidErrorType_ThrowsArgumentException()
    {
        // Arrange
        var command = new SimulateErrorCommand("invalid_type");

        // Act & Assert
        var act = async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<ArgumentException>()).Which;

        exception.Message.Should().Contain("Invalid error type: invalid_type");
        exception.Message.Should().Contain("Valid types: 500, 400, timeout, exception");
    }

    [Theory]
    [InlineData("500")]
    [InlineData("400")]
    [InlineData("exception")]
    public async Task Handle_WithDifferentErrorTypes_LogsWarning(string errorType)
    {
        // Arrange
        var command = new SimulateErrorCommand(errorType);

        // Act & Assert
        var act = async () =>
            await _handler.Handle(command, TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<Exception>();

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains($"Simulating error type: {errorType}")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTestEndpointsDisabled_ThrowsInvalidOperationException()
    {
        // Arrange
        var configData = new Dictionary<string, string?>
        {
            { "TestEndpoints:Enabled", "false" }
        };

        var disabledConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var handlerWithDisabledEndpoints = new SimulateErrorCommandHandler(_mockLogger.Object, disabledConfig);
        var command = new SimulateErrorCommand("500");

        // Act & Assert
        var act = async () =>
            await handlerWithDisabledEndpoints.Handle(command, TestContext.Current.CancellationToken);
        var exception = (await act.Should().ThrowAsync<InvalidOperationException>()).Which;

        exception.Message.Should().Contain("Test endpoints are disabled in this environment");

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Test endpoints are disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithCaseSensitiveErrorType_WorksCorrectly()
    {
        // Arrange - Test case insensitivity
        var commands = new[]
        {
            new SimulateErrorCommand("500"),
            new SimulateErrorCommand("400"),
            new SimulateErrorCommand("EXCEPTION"),
            new SimulateErrorCommand("Exception"),
            new SimulateErrorCommand("exception")
        };

        // Act & Assert - All should throw appropriate exceptions
        foreach (var command in commands)
        {
            var act = async () =>
                await _handler.Handle(command, TestContext.Current.CancellationToken);
            await act.Should().ThrowAsync<Exception>();
        }
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new SimulateErrorCommandHandler(null!, _configuration);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new SimulateErrorCommandHandler(_mockLogger.Object, null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
