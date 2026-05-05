using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// Unit tests for <see cref="MechanicMetricsRecalculatedCacheInvalidationHandler"/>
/// (ADR-051 Sprint 2 / Task 13). Covers:
///  - Both dashboard + trend tags are evicted on a successful event.
///  - Per-tag failures are isolated: an exception on the first tag does not skip the
///    second.
///  - Null guard on the notification.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicMetricsRecalculatedCacheInvalidationHandlerTests
{
    private readonly Mock<IHybridCacheService> _cache = new(MockBehavior.Strict);
    private readonly Mock<ILogger<MechanicMetricsRecalculatedCacheInvalidationHandler>> _logger = new();

    private static MechanicMetricsRecalculatedEvent BuildEvent(
        RecalcJobStatus status = RecalcJobStatus.Completed) =>
        new(
            JobId: Guid.NewGuid(),
            TriggeredByUserId: Guid.NewGuid(),
            Status: status,
            Processed: 17,
            Failed: 1,
            Skipped: 2,
            Total: 20,
            CompletedAt: DateTimeOffset.UtcNow);

    [Fact]
    public async Task Handle_OnSuccess_InvalidatesBothDashboardAndTrendTags()
    {
        // Arrange
        _cache.Setup(c => c.RemoveByTagAsync(
                MechanicMetricsRecalculatedCacheInvalidationHandler.DashboardTag,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _cache.Setup(c => c.RemoveByTagAsync(
                MechanicMetricsRecalculatedCacheInvalidationHandler.TrendTag,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);

        var handler = new MechanicMetricsRecalculatedCacheInvalidationHandler(_cache.Object, _logger.Object);
        var evt = BuildEvent();

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        _cache.Verify(c => c.RemoveByTagAsync(
            MechanicMetricsRecalculatedCacheInvalidationHandler.DashboardTag,
            It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveByTagAsync(
            MechanicMetricsRecalculatedCacheInvalidationHandler.TrendTag,
            It.IsAny<CancellationToken>()), Times.Once);
        _cache.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_OnFailedJob_StillInvalidatesBothTags()
    {
        // The handler does not short-circuit on Status != Completed: a Failed job's partial
        // counters might still have produced metric snapshots that need eviction.
        // (Decision documented in event payload remarks.)

        // Arrange
        _cache.Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var handler = new MechanicMetricsRecalculatedCacheInvalidationHandler(_cache.Object, _logger.Object);
        var evt = BuildEvent(RecalcJobStatus.Failed);

        // Act
        await handler.Handle(evt, CancellationToken.None);

        // Assert
        _cache.Verify(c => c.RemoveByTagAsync(
            MechanicMetricsRecalculatedCacheInvalidationHandler.DashboardTag,
            It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveByTagAsync(
            MechanicMetricsRecalculatedCacheInvalidationHandler.TrendTag,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_FirstTagThrows_StillInvalidatesSecondTag()
    {
        // Per-tag try/catch isolation is the whole point: a transient cache outage on the
        // dashboard bucket must not leave the trend bucket stale forever.

        // Arrange
        _cache.Setup(c => c.RemoveByTagAsync(
                MechanicMetricsRecalculatedCacheInvalidationHandler.DashboardTag,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis-down"));

        _cache.Setup(c => c.RemoveByTagAsync(
                MechanicMetricsRecalculatedCacheInvalidationHandler.TrendTag,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var handler = new MechanicMetricsRecalculatedCacheInvalidationHandler(_cache.Object, _logger.Object);
        var evt = BuildEvent();

        // Act
        var act = async () => await handler.Handle(evt, CancellationToken.None);

        // Assert — handler swallows; trend tag still invoked.
        await act.Should().NotThrowAsync();
        _cache.Verify(c => c.RemoveByTagAsync(
            MechanicMetricsRecalculatedCacheInvalidationHandler.TrendTag,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_BothTagsThrow_StillSwallowsAndLogs()
    {
        // Arrange
        _cache.Setup(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("cache-down"));

        var handler = new MechanicMetricsRecalculatedCacheInvalidationHandler(_cache.Object, _logger.Object);
        var evt = BuildEvent();

        // Act
        var act = async () => await handler.Handle(evt, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
        // Both attempts were made even though both failed.
        _cache.Verify(c => c.RemoveByTagAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_NullNotification_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new MechanicMetricsRecalculatedCacheInvalidationHandler(_cache.Object, _logger.Object);

        // Act
        var act = async () => await handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
        _cache.VerifyNoOtherCalls();
    }
}
