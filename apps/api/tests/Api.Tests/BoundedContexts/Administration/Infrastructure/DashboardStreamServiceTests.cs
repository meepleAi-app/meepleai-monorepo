using Api.BoundedContexts.Administration.Domain.Events;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure;

/// <summary>
/// Unit tests for DashboardStreamService (Issue #3324).
/// Tests SSE streaming pub/sub functionality.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public class DashboardStreamServiceTests
{
    private readonly DashboardStreamService _service;

    public DashboardStreamServiceTests()
    {
        _service = new DashboardStreamService(NullLogger<DashboardStreamService>.Instance);
    }

    [Fact]
    public async Task SubscribeToDashboardEvents_CreatesChannel_ReturnsAsyncEnumerable()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var subscription = _service.SubscribeToDashboardEvents(userId, cts.Token);

        // Assert
        subscription.Should().NotBeNull();
        var act = async () =>
        {
            await foreach (var _ in subscription)
            {
                // Should timeout waiting for events
            }
        };
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task PublishEventAsync_WithNoSubscribers_CompletesSuccessfully()
    {
        // Arrange
        var evt = new DashboardStatsUpdatedEvent
        {
            CollectionCount = 10,
            PlayedCount = 5,
            ActiveSessionCount = 2,
            OnlineUserCount = 3
        };

        // Act
        var act = async () => await _service.PublishEventAsync(evt, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishEventAsync_WithSubscriber_DeliversEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var receivedEvents = new List<DashboardStatsUpdatedEvent>();
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToDashboardEvents(userId, cts.Token))
            {
                if (evt is DashboardStatsUpdatedEvent statsEvent)
                {
                    receivedEvents.Add(statsEvent);
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var publishedEvent = new DashboardStatsUpdatedEvent
        {
            CollectionCount = 10,
            PlayedCount = 5,
            ActiveSessionCount = 2,
            OnlineUserCount = 3
        };
        await _service.PublishEventAsync(publishedEvent, CancellationToken.None);

        await subscriptionTask;

        // Assert
        receivedEvents.Should().HaveCount(1);
        receivedEvents[0].CollectionCount.Should().Be(10);
        receivedEvents[0].PlayedCount.Should().Be(5);
    }

    [Fact]
    public async Task PublishEventAsync_WithMultipleSubscribers_DeliversToAll()
    {
        // Arrange
        var user1Id = Guid.NewGuid();
        var user2Id = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var receivedEvents1 = new List<DashboardStatsUpdatedEvent>();
        var receivedEvents2 = new List<DashboardStatsUpdatedEvent>();

        var subscription1Task = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToDashboardEvents(user1Id, cts.Token))
            {
                if (evt is DashboardStatsUpdatedEvent statsEvent)
                {
                    receivedEvents1.Add(statsEvent);
                    break;
                }
            }
        }, cts.Token);

        var subscription2Task = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToDashboardEvents(user2Id, cts.Token))
            {
                if (evt is DashboardStatsUpdatedEvent statsEvent)
                {
                    receivedEvents2.Add(statsEvent);
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var publishedEvent = new DashboardStatsUpdatedEvent
        {
            CollectionCount = 10,
            PlayedCount = 5,
            ActiveSessionCount = 2,
            OnlineUserCount = 3
        };
        await _service.PublishEventAsync(publishedEvent, CancellationToken.None);

        await Task.WhenAll(subscription1Task, subscription2Task);

        // Assert
        receivedEvents1.Should().HaveCount(1);
        receivedEvents2.Should().HaveCount(1);
        receivedEvents1[0].CollectionCount.Should().Be(10);
        receivedEvents2[0].CollectionCount.Should().Be(10);
    }

    [Fact]
    public async Task PublishEventToUserAsync_DeliversToSpecificUser()
    {
        // Arrange
        var targetUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var targetUserEvents = new List<DashboardNotificationEvent>();
        var otherUserEvents = new List<DashboardNotificationEvent>();

        var targetSubscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToDashboardEvents(targetUserId, cts.Token))
            {
                if (evt is DashboardNotificationEvent notification)
                {
                    targetUserEvents.Add(notification);
                    break;
                }
            }
        }, cts.Token);

        // Second user subscription - should NOT receive the event
        var otherSubscriptionStarted = new TaskCompletionSource<bool>();
        var otherSubscriptionTask = Task.Run(async () =>
        {
            otherSubscriptionStarted.SetResult(true);
            await foreach (var evt in _service.SubscribeToDashboardEvents(otherUserId, cts.Token))
            {
                if (evt is DashboardNotificationEvent notification)
                {
                    otherUserEvents.Add(notification);
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act - publish to target user only
        var notification = new DashboardNotificationEvent
        {
            UserId = targetUserId,
            Type = "info",
            Title = "Test Notification",
            Message = "This is a test"
        };
        await _service.PublishEventToUserAsync(targetUserId, notification, CancellationToken.None);

        // Wait for target subscription to receive
        await targetSubscriptionTask;

        // Cancel to clean up other subscription
        await cts.CancelAsync();

        // Assert
        targetUserEvents.Should().HaveCount(1);
        targetUserEvents[0].Title.Should().Be("Test Notification");
        otherUserEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task SubscribeToDashboardEvents_CancellationRequested_StopsStream()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        var receivedCount = 0;
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToDashboardEvents(userId, cts.Token))
            {
                receivedCount++;
            }
        }, cts.Token);

        // Act
        await Task.Delay(50);
        await cts.CancelAsync();

        // Assert
        var act = async () => await subscriptionTask;
        await act.Should().ThrowAsync<OperationCanceledException>();
        receivedCount.Should().Be(0);
    }

    [Fact]
    public async Task PublishEventAsync_ActivityEvent_DeliversCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var receivedEvents = new List<DashboardActivityEvent>();
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToDashboardEvents(userId, cts.Token))
            {
                if (evt is DashboardActivityEvent activityEvent)
                {
                    receivedEvents.Add(activityEvent);
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var activity = new DashboardActivityEvent
        {
            ActivityType = "game_added",
            Title = "Wingspan added to collection",
            EntityId = Guid.NewGuid(),
            UserId = userId
        };
        await _service.PublishEventAsync(activity, CancellationToken.None);

        await subscriptionTask;

        // Assert
        receivedEvents.Should().HaveCount(1);
        receivedEvents[0].ActivityType.Should().Be("game_added");
        receivedEvents[0].Title.Should().Be("Wingspan added to collection");
    }

    [Fact]
    public async Task PublishEventAsync_SessionUpdatedEvent_DeliversCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var receivedEvents = new List<DashboardSessionUpdatedEvent>();
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToDashboardEvents(userId, cts.Token))
            {
                if (evt is DashboardSessionUpdatedEvent sessionEvent)
                {
                    receivedEvents.Add(sessionEvent);
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var sessionUpdate = new DashboardSessionUpdatedEvent
        {
            SessionId = Guid.NewGuid(),
            UserId = userId,
            GameTitle = "Catan",
            Turn = 5,
            Status = "active"
        };
        await _service.PublishEventAsync(sessionUpdate, CancellationToken.None);

        await subscriptionTask;

        // Assert
        receivedEvents.Should().HaveCount(1);
        receivedEvents[0].GameTitle.Should().Be("Catan");
        receivedEvents[0].Turn.Should().Be(5);
    }
}
