using Api.BoundedContexts.Administration.Application.EventHandlers;
using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.EventHandlers;

/// <summary>
/// Issue #879: Unit tests for DashboardCacheInvalidationEventHandler.
/// Verifies cache invalidation on configuration update and toggle events.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class DashboardCacheInvalidationEventHandlerTests
{
    private readonly Mock<HybridCache> _cacheMock;
    private readonly Mock<ILogger<DashboardCacheInvalidationEventHandler>> _loggerMock;
    private readonly DashboardCacheInvalidationEventHandler _handler;

    public DashboardCacheInvalidationEventHandlerTests()
    {
        _cacheMock = new Mock<HybridCache>();
        _loggerMock = new Mock<ILogger<DashboardCacheInvalidationEventHandler>>();
        _handler = new DashboardCacheInvalidationEventHandler(_cacheMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ConfigurationUpdatedEvent_InvalidatesDashboardCache()
    {
        // Arrange
        var configKey = new ConfigKey("Features.StreamingResponses");
        var evt = new ConfigurationUpdatedEvent(
            configurationId: Guid.NewGuid(),
            key: configKey,
            previousValue: "false",
            newValue: "true",
            version: 2,
            updatedByUserId: Guid.NewGuid());

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        // Verify cache removal was called multiple times (for common cache key combinations)
        _cacheMock.Verify(
            c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(16), // 4 day ranges × 4 role filters = 16 keys
            "Should invalidate all common dashboard cache key combinations");
    }

    [Fact]
    public async Task Handle_ConfigurationToggledEvent_InvalidatesDashboardCache()
    {
        // Arrange
        var configKey = new ConfigKey("RateLimit.Api.Requests");
        var evt = new ConfigurationToggledEvent(
            configurationId: Guid.NewGuid(),
            key: configKey,
            isActive: true,
            toggledByUserId: Guid.NewGuid());

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _cacheMock.Verify(
            c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(16),
            "Should invalidate dashboard cache on configuration toggle");
    }

    [Fact]
    public async Task Handle_ConfigurationUpdatedEvent_LogsInvalidation()
    {
        // Arrange
        var configKey = new ConfigKey("TestConfig");
        var evt = new ConfigurationUpdatedEvent(
            configurationId: Guid.NewGuid(),
            key: configKey,
            previousValue: "old",
            newValue: "new",
            version: 1,
            updatedByUserId: Guid.NewGuid());

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Configuration updated")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Dashboard cache invalidated")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_CacheRemovalThrows_LogsErrorButDoesNotThrow()
    {
        // Arrange
        var configKey = new ConfigKey("TestConfig");
        var evt = new ConfigurationUpdatedEvent(
            configurationId: Guid.NewGuid(),
            key: configKey,
            previousValue: "old",
            newValue: "new",
            version: 1,
            updatedByUserId: Guid.NewGuid());

        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cache error"));

        // Act
        var exception = await Record.ExceptionAsync(() => _handler.Handle(evt, CancellationToken.None));

        // Assert
        exception.Should().BeNull(); // Should not throw
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to invalidate dashboard cache")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "Should log error when cache removal fails");
    }

    [Fact]
    public async Task Handle_GeneratesCacheKeys_CoversCommonParameters()
    {
        // Arrange
        var configKey = new ConfigKey("TestConfig");
        var evt = new ConfigurationUpdatedEvent(
            configurationId: Guid.NewGuid(),
            key: configKey,
            previousValue: "old",
            newValue: "new",
            version: 1,
            updatedByUserId: Guid.NewGuid());

        var removedKeys = new List<string>();
        _cacheMock
            .Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => removedKeys.Add(key))
            .Returns(ValueTask.CompletedTask);

        // Act
        await _handler.Handle(evt, CancellationToken.None);

        // Assert
        // Verify common day ranges are covered
        removedKeys.Should().Contain(k => k.Contains("dashboard:stats:7:"));
        removedKeys.Should().Contain(k => k.Contains("dashboard:stats:14:"));
        removedKeys.Should().Contain(k => k.Contains("dashboard:stats:30:"));
        removedKeys.Should().Contain(k => k.Contains("dashboard:stats:90:"));

        // Verify common role filters are covered
        removedKeys.Should().Contain(k => k.Contains("::"));      // No filters
        removedKeys.Should().Contain(k => k.Contains("::admin"));  // Admin filter
        removedKeys.Should().Contain(k => k.Contains("::user"));   // User filter
        removedKeys.Should().Contain(k => k.Contains("::all"));    // All roles
    }
}