using Api.BoundedContexts.BusinessSimulations.Application.EventHandlers;
using Api.BoundedContexts.BusinessSimulations.Application.Interfaces;
using Api.BoundedContexts.BusinessSimulations.Domain.Events;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application;

/// <summary>
/// Unit tests for TokenUsageLedgerEventHandler (Issue #3721)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class TokenUsageLedgerEventHandlerTests
{
    private readonly Mock<ILedgerTrackingService> _ledgerTrackingServiceMock;
    private readonly Mock<ILogger<TokenUsageLedgerEventHandler>> _loggerMock;
    private readonly TokenUsageLedgerEventHandler _handler;

    public TokenUsageLedgerEventHandlerTests()
    {
        _ledgerTrackingServiceMock = new Mock<ILedgerTrackingService>();
        _loggerMock = new Mock<ILogger<TokenUsageLedgerEventHandler>>();
        _handler = new TokenUsageLedgerEventHandler(
            _ledgerTrackingServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_WithValidEvent_ShouldCallTrackTokenUsageAsync()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var notification = new TokenUsageLedgerEvent(
            userId, "openai/gpt-4o-mini", 1500, 0.005m, "chat", DateTime.UtcNow);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _ledgerTrackingServiceMock.Verify(
            s => s.TrackTokenUsageAsync(
                userId, "openai/gpt-4o-mini", 1500, 0.005m, "chat", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithNullEndpoint_ShouldCallTrackTokenUsageAsync()
    {
        // Arrange
        var notification = new TokenUsageLedgerEvent(
            Guid.NewGuid(), "model", 100, 0.01m, null, DateTime.UtcNow);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert
        _ledgerTrackingServiceMock.Verify(
            s => s.TrackTokenUsageAsync(
                It.IsAny<Guid>(), "model", 100, 0.01m, null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_ShouldCatchAndLog()
    {
        // Arrange
        _ledgerTrackingServiceMock
            .Setup(s => s.TrackTokenUsageAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var notification = new TokenUsageLedgerEvent(
            Guid.NewGuid(), "model", 100, 0.01m, "chat", DateTime.UtcNow);

        // Act - should NOT throw
        var act = () => _handler.Handle(notification, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_WhenServiceThrows_ShouldLogError()
    {
        // Arrange
        _ledgerTrackingServiceMock
            .Setup(s => s.TrackTokenUsageAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database error"));

        var notification = new TokenUsageLedgerEvent(
            Guid.NewGuid(), "model", 100, 0.01m, "chat", DateTime.UtcNow);

        // Act
        await _handler.Handle(notification, CancellationToken.None);

        // Assert - verify error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var notification = new TokenUsageLedgerEvent(
            Guid.NewGuid(), "model", 100, 0.01m, "chat", DateTime.UtcNow);

        // Act
        await _handler.Handle(notification, cts.Token);

        // Assert
        _ledgerTrackingServiceMock.Verify(
            s => s.TrackTokenUsageAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<decimal>(), It.IsAny<string?>(), cts.Token),
            Times.Once);
    }

    #region Constructor Validation

    [Fact]
    public void Constructor_WithNullService_ShouldThrow()
    {
        var act = () => new TokenUsageLedgerEventHandler(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("ledgerTrackingService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        var act = () => new TokenUsageLedgerEventHandler(_ledgerTrackingServiceMock.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion
}
