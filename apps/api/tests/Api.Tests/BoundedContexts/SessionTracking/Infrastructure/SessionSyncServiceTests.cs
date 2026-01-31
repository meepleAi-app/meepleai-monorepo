using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public class SessionSyncServiceTests
{
    private readonly SessionSyncService _service;

    public SessionSyncServiceTests()
    {
        _service = new SessionSyncService(NullLogger<SessionSyncService>.Instance);
    }

    [Fact]
    public async Task SubscribeToSessionEvents_CreatesChannel_ReturnsAsyncEnumerable()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act
        var subscription = _service.SubscribeToSessionEvents(sessionId, cts.Token);

        // Assert
        subscription.Should().NotBeNull();
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in subscription)
            {
                // Should timeout waiting for events
            }
        });
    }

    [Fact]
    public async Task PublishEventAsync_WithNoSubscribers_CompletesSuccessfully()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var evt = new ParticipantAddedEvent
        {
            SessionId = sessionId,
            ParticipantId = Guid.NewGuid(),
            DisplayName = "Player 1",
            IsOwner = false,
            JoinOrder = 1
        };

        // Act
        var act = async () => await _service.PublishEventAsync(sessionId, evt, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishEventAsync_WithSubscriber_DeliversEvent()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var receivedEvents = new List<ParticipantAddedEvent>();
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToSessionEvents(sessionId, cts.Token))
            {
                if (evt is ParticipantAddedEvent participantEvent)
                {
                    receivedEvents.Add(participantEvent);
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var publishedEvent = new ParticipantAddedEvent
        {
            SessionId = sessionId,
            ParticipantId = Guid.NewGuid(),
            DisplayName = "Player 1",
            IsOwner = false,
            JoinOrder = 1
        };
        await _service.PublishEventAsync(sessionId, publishedEvent, CancellationToken.None);

        await subscriptionTask;

        // Assert
        receivedEvents.Should().HaveCount(1);
        receivedEvents[0].SessionId.Should().Be(sessionId);
        receivedEvents[0].DisplayName.Should().Be("Player 1");
    }

    [Fact]
    public async Task PublishEventAsync_WithMultipleSubscribers_DeliversToAll()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var receivedEvents1 = new List<ParticipantAddedEvent>();
        var receivedEvents2 = new List<ParticipantAddedEvent>();

        var subscription1Task = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToSessionEvents(sessionId, cts.Token))
            {
                if (evt is ParticipantAddedEvent participantEvent)
                {
                    receivedEvents1.Add(participantEvent);
                    break;
                }
            }
        }, cts.Token);

        var subscription2Task = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToSessionEvents(sessionId, cts.Token))
            {
                if (evt is ParticipantAddedEvent participantEvent)
                {
                    receivedEvents2.Add(participantEvent);
                    break;
                }
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var publishedEvent = new ParticipantAddedEvent
        {
            SessionId = sessionId,
            ParticipantId = Guid.NewGuid(),
            DisplayName = "Player 1",
            IsOwner = false,
            JoinOrder = 1
        };
        await _service.PublishEventAsync(sessionId, publishedEvent, CancellationToken.None);

        await Task.WhenAll(subscription1Task, subscription2Task);

        // Assert
        receivedEvents1.Should().HaveCount(1);
        receivedEvents2.Should().HaveCount(1);
        receivedEvents1[0].DisplayName.Should().Be("Player 1");
        receivedEvents2[0].DisplayName.Should().Be("Player 1");
    }

    [Fact]
    public async Task SubscribeToSessionEvents_CancellationRequested_StopsStream()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        var receivedCount = 0;
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToSessionEvents(sessionId, cts.Token))
            {
                receivedCount++;
            }
        }, cts.Token);

        // Act
        await Task.Delay(50);
        await cts.CancelAsync();

        // Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await subscriptionTask);
        receivedCount.Should().Be(0);
    }

    [Fact]
    public async Task PublishEventAsync_DifferentSessionIds_IsolatesEvents()
    {
        // Arrange
        var session1Id = Guid.NewGuid();
        var session2Id = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var session1Events = new List<ParticipantAddedEvent>();

        var subscription1Task = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeToSessionEvents(session1Id, cts.Token))
            {
                if (evt is ParticipantAddedEvent participantEvent)
                {
                    session1Events.Add(participantEvent);
                    if (session1Events.Count >= 1) break;
                }
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var event1 = new ParticipantAddedEvent
        {
            SessionId = session1Id,
            ParticipantId = Guid.NewGuid(),
            DisplayName = "Session 1 Player",
            IsOwner = false,
            JoinOrder = 1
        };

        var event2 = new ParticipantAddedEvent
        {
            SessionId = session2Id,
            ParticipantId = Guid.NewGuid(),
            DisplayName = "Session 2 Player",
            IsOwner = false,
            JoinOrder = 1
        };

        await _service.PublishEventAsync(session1Id, event1, CancellationToken.None);
        await _service.PublishEventAsync(session2Id, event2, CancellationToken.None);

        await subscription1Task;

        // Assert
        session1Events.Should().HaveCount(1);
        session1Events[0].DisplayName.Should().Be("Session 1 Player");
    }
}
