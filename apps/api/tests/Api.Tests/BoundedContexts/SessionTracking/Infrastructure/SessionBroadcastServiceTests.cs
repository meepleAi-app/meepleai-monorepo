using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionBroadcastServiceTests : IDisposable
{
    private readonly SessionBroadcastService _service;

    public SessionBroadcastServiceTests()
    {
        _service = new SessionBroadcastService(
            NullLogger<SessionBroadcastService>.Instance);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    #region SubscribeAsync

    [Fact]
    public async Task SubscribeAsync_ReturnsAsyncEnumerable_ThatCompletsOnCancellation()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in _service.SubscribeAsync(sessionId, userId, null, cts.Token))
            {
                // Should timeout waiting for events
            }
        });
    }

    [Fact]
    public async Task SubscribeAsync_IncrementsConnectionCount()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Act
        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var _ in _service.SubscribeAsync(sessionId, userId, null, cts.Token))
            {
            }
        }, cts.Token);

        await Task.Delay(50);

        // Assert
        _service.GetConnectionCount(sessionId).Should().Be(1);

        await cts.CancelAsync();
        try { await subscriptionTask; } catch (OperationCanceledException) { }
    }

    [Fact]
    public async Task SubscribeAsync_DecrementsConnectionCount_OnDisconnect()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        var subscriptionTask = Task.Run(async () =>
        {
            await foreach (var _ in _service.SubscribeAsync(sessionId, userId, null, cts.Token))
            {
            }
        }, cts.Token);

        await Task.Delay(50);
        _service.GetConnectionCount(sessionId).Should().Be(1);

        // Act
        await cts.CancelAsync();
        try { await subscriptionTask; } catch (OperationCanceledException) { }
        await Task.Delay(50);

        // Assert
        _service.GetConnectionCount(sessionId).Should().Be(0);
    }

    [Fact]
    public async Task SubscribeAsync_RejectsConnection_WhenPoolIsFull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var ctsList = new List<CancellationTokenSource>();
        var tasks = new List<Task>();

        // Fill the pool to max
        for (var i = 0; i < SessionBroadcastService.MaxConnectionsPerSession; i++)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            ctsList.Add(cts);
            var task = Task.Run(async () =>
            {
                await foreach (var _ in _service.SubscribeAsync(sessionId, Guid.NewGuid(), null, cts.Token))
                {
                }
            }, cts.Token);
            tasks.Add(task);
        }

        await Task.Delay(100);
        _service.GetConnectionCount(sessionId).Should().Be(SessionBroadcastService.MaxConnectionsPerSession);

        // Act - attempt to add one more
        var overflowEvents = new List<SseEventEnvelope>();
        using var overflowCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await foreach (var evt in _service.SubscribeAsync(sessionId, Guid.NewGuid(), null, overflowCts.Token))
        {
            overflowEvents.Add(evt);
        }

        // Assert - rejected (yield break), should receive no events
        overflowEvents.Should().BeEmpty();

        // Cleanup
        foreach (var cts in ctsList) await cts.CancelAsync();
        foreach (var cts in ctsList) cts.Dispose();
    }

    [Fact]
    public async Task SubscribeAsync_MultipleSubscribers_AllReceiveEvents()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var received1 = new List<SseEventEnvelope>();
        var received2 = new List<SseEventEnvelope>();

        var sub1Task = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, Guid.NewGuid(), null, cts.Token))
            {
                received1.Add(evt);
                if (received1.Count >= 1) break;
            }
        }, cts.Token);

        var sub2Task = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, Guid.NewGuid(), null, cts.Token))
            {
                received2.Add(evt);
                if (received2.Count >= 1) break;
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var testEvent = new ScoreUpdatedEvent
        {
            SessionId = sessionId,
            ParticipantId = Guid.NewGuid(),
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 10
        };
        await _service.PublishAsync(sessionId, testEvent);

        await Task.WhenAll(sub1Task, sub2Task);

        // Assert
        received1.Should().HaveCount(1);
        received2.Should().HaveCount(1);
        received1[0].EventType.Should().Be("session:score");
        received2[0].EventType.Should().Be("session:score");
    }

    #endregion

    #region PublishAsync

    [Fact]
    public async Task PublishAsync_WithNoSubscribers_CompletesSuccessfully()
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
        var act = async () => await _service.PublishAsync(sessionId, evt);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task PublishAsync_DeliversEventToSubscriber()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var receivedEvents = new List<SseEventEnvelope>();

        var subTask = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, userId, null, cts.Token))
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 1) break;
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var testEvent = new ParticipantAddedEvent
        {
            SessionId = sessionId,
            ParticipantId = Guid.NewGuid(),
            DisplayName = "Player 1",
            IsOwner = false,
            JoinOrder = 1
        };
        await _service.PublishAsync(sessionId, testEvent);

        await subTask;

        // Assert
        receivedEvents.Should().HaveCount(1);
        receivedEvents[0].EventType.Should().Be("session:player");
        receivedEvents[0].Id.Should().NotBeNullOrEmpty();
        receivedEvents[0].Data.Should().NotBeNull();
    }

    [Fact]
    public async Task PublishAsync_DifferentSessions_IsolatesEvents()
    {
        // Arrange
        var session1Id = Guid.NewGuid();
        var session2Id = Guid.NewGuid();
        var session1Events = new List<SseEventEnvelope>();

        var sub1Task = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(session1Id, Guid.NewGuid(), null, cts.Token))
            {
                session1Events.Add(evt);
                if (session1Events.Count >= 1) break;
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act - publish to both sessions
        var event1 = new ScoreUpdatedEvent
        {
            SessionId = session1Id,
            ParticipantId = Guid.NewGuid(),
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 10
        };
        var event2 = new ScoreUpdatedEvent
        {
            SessionId = session2Id,
            ParticipantId = Guid.NewGuid(),
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 20
        };

        await _service.PublishAsync(session1Id, event1);
        await _service.PublishAsync(session2Id, event2);

        await sub1Task;

        // Assert - session1 only gets session1 events
        session1Events.Should().HaveCount(1);
    }

    #endregion

    #region Selective Broadcasting (EventVisibility)

    [Fact]
    public async Task PublishAsync_PublicEvent_DeliveredToAll()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();
        var received1 = new List<SseEventEnvelope>();
        var received2 = new List<SseEventEnvelope>();

        var sub1 = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, user1, null, cts.Token))
            {
                received1.Add(evt);
                if (received1.Count >= 1) break;
            }
        }, cts.Token);

        var sub2 = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, user2, null, cts.Token))
            {
                received2.Add(evt);
                if (received2.Count >= 1) break;
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var testEvent = new ScoreUpdatedEvent
        {
            SessionId = sessionId,
            ParticipantId = user1,
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 10
        };
        await _service.PublishAsync(sessionId, testEvent, EventVisibility.Public);

        await Task.WhenAll(sub1, sub2);

        // Assert
        received1.Should().HaveCount(1);
        received2.Should().HaveCount(1);
    }

    [Fact]
    public async Task PublishAsync_PrivateEvent_DeliveredOnlyToTarget()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var targetUser = Guid.NewGuid();
        var otherUser = Guid.NewGuid();
        var targetReceived = new List<SseEventEnvelope>();
        var otherReceived = new List<SseEventEnvelope>();

        var targetSub = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, targetUser, null, cts.Token))
            {
                targetReceived.Add(evt);
                if (targetReceived.Count >= 1) break;
            }
        }, cts.Token);

        var otherSub = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, otherUser, null, cts.Token))
            {
                otherReceived.Add(evt);
                if (otherReceived.Count >= 1) break;
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act - private event for targetUser only
        var testEvent = new ScoreUpdatedEvent
        {
            SessionId = sessionId,
            ParticipantId = targetUser,
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 10
        };
        await _service.PublishAsync(sessionId, testEvent, EventVisibility.PrivateTo(targetUser));

        // Wait for target to receive
        await targetSub;

        // otherUser should NOT have received anything - cancel to verify
        await cts.CancelAsync();
        try { await otherSub; } catch (OperationCanceledException) { }

        // Assert
        targetReceived.Should().HaveCount(1);
        otherReceived.Should().BeEmpty();
    }

    #endregion

    #region Last-Event-ID Reconnection

    [Fact]
    public async Task SubscribeAsync_WithLastEventId_ReplaysBufferedEvents()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var keepAliveCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // Keep a background subscriber alive to prevent pool cleanup
        var keepAliveTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in _service.SubscribeAsync(sessionId, Guid.NewGuid(), null, keepAliveCts.Token)) { }
            }
            catch (OperationCanceledException) { }
        }, keepAliveCts.Token);

        await Task.Delay(50);

        // Subscribe and receive events to build buffer
        using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var firstEvents = new List<SseEventEnvelope>();
        var sub1 = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, userId, null, cts1.Token))
            {
                firstEvents.Add(evt);
                if (firstEvents.Count >= 3) break;
            }
        }, cts1.Token);

        await Task.Delay(50);

        // Publish 3 events
        for (var i = 0; i < 3; i++)
        {
            var evt = new ScoreUpdatedEvent
            {
                SessionId = sessionId,
                ParticipantId = userId,
                ScoreEntryId = Guid.NewGuid(),
                NewScore = (i + 1) * 10
            };
            await _service.PublishAsync(sessionId, evt, EventVisibility.Public);
            await Task.Delay(10);
        }

        await sub1;
        firstEvents.Should().HaveCount(3);

        // Act: Reconnect with Last-Event-ID of the first event → should replay events 2 and 3
        var lastEventId = firstEvents[0].Id;
        using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var reconnectedEvents = new List<SseEventEnvelope>();

        try
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, userId, lastEventId, cts2.Token))
            {
                reconnectedEvents.Add(evt);
                if (reconnectedEvents.Count >= 2) break;
            }
        }
        catch (OperationCanceledException)
        {
            // Expected if timeout fires before getting 2 events
        }

        // Cleanup keep-alive
        await keepAliveCts.CancelAsync();
        try { await keepAliveTask; } catch (OperationCanceledException) { }

        // Assert - should have replayed events after the first one
        reconnectedEvents.Should().HaveCount(2);
        reconnectedEvents[0].Id.Should().Be(firstEvents[1].Id);
        reconnectedEvents[1].Id.Should().Be(firstEvents[2].Id);
    }

    [Fact]
    public async Task SubscribeAsync_WithInvalidLastEventId_ReplaysNothing()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Subscribe and publish one event to ensure pool exists
        var firstEvents = new List<SseEventEnvelope>();
        var sub = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, userId, null, cts1.Token))
            {
                firstEvents.Add(evt);
                if (firstEvents.Count >= 1) break;
            }
        }, cts1.Token);

        await Task.Delay(50);

        await _service.PublishAsync(sessionId, new ScoreUpdatedEvent
        {
            SessionId = sessionId,
            ParticipantId = userId,
            ScoreEntryId = Guid.NewGuid(),
            NewScore = 10
        }, EventVisibility.Public);

        await sub;

        // Act: Reconnect with invalid ID
        using var cts2 = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var reconnectedEvents = new List<SseEventEnvelope>();

        try
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, userId, "invalid-id", cts2.Token))
            {
                reconnectedEvents.Add(evt);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected: subscription cancels when no new events arrive
        }

        // Assert - should not replay anything (ID not found in buffer)
        reconnectedEvents.Should().BeEmpty();
    }

    #endregion

    #region GetConnectionCount

    [Fact]
    public void GetConnectionCount_NoSubscribers_ReturnsZero()
    {
        // Act
        var count = _service.GetConnectionCount(Guid.NewGuid());

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task GetConnectionCount_WithMultipleSubscribers_ReturnsCorrectCount()
    {
        // Arrange
        var sessionId = Guid.NewGuid();

        var sub1 = Task.Run(async () =>
        {
            await foreach (var _ in _service.SubscribeAsync(sessionId, Guid.NewGuid(), null, cts.Token)) { }
        }, cts.Token);

        var sub2 = Task.Run(async () =>
        {
            await foreach (var _ in _service.SubscribeAsync(sessionId, Guid.NewGuid(), null, cts.Token)) { }
        }, cts.Token);

        await Task.Delay(50);

        // Act & Assert
        _service.GetConnectionCount(sessionId).Should().Be(2);

        await cts.CancelAsync();
        try { await Task.WhenAll(sub1, sub2); } catch (OperationCanceledException) { }
    }

    #endregion

    #region DisconnectAllAsync

    [Fact]
    public async Task DisconnectAllAsync_DisconnectsAllSubscribers()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var completed1 = false;
        var completed2 = false;

        var sub1 = Task.Run(async () =>
        {
            await foreach (var _ in _service.SubscribeAsync(sessionId, Guid.NewGuid(), null, cts.Token)) { }
            completed1 = true;
        }, cts.Token);

        var sub2 = Task.Run(async () =>
        {
            await foreach (var _ in _service.SubscribeAsync(sessionId, Guid.NewGuid(), null, cts.Token)) { }
            completed2 = true;
        }, cts.Token);

        await Task.Delay(50);
        _service.GetConnectionCount(sessionId).Should().Be(2);

        // Act
        await _service.DisconnectAllAsync(sessionId);
        await Task.Delay(100);

        // Assert
        _service.GetConnectionCount(sessionId).Should().Be(0);
    }

    #endregion

    #region SseEventEnvelope

    [Fact]
    public async Task PublishAsync_CreatesEnvelopeWithCorrectFields()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var receivedEvents = new List<SseEventEnvelope>();

        var sub = Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, userId, null, cts.Token))
            {
                receivedEvents.Add(evt);
                if (receivedEvents.Count >= 1) break;
            }
        }, cts.Token);

        await Task.Delay(50);

        // Act
        var testEvent = new DiceRolledEvent
        {
            DiceRollId = Guid.NewGuid(),
            SessionId = sessionId,
            ParticipantId = userId,
            ParticipantName = "Tester",
            Formula = "2d6",
            Rolls = [3, 5],
            Total = 8
        };
        await _service.PublishAsync(sessionId, testEvent);

        await sub;

        // Assert
        var envelope = receivedEvents[0];
        envelope.Id.Should().StartWith(sessionId.ToString("N"));
        envelope.EventType.Should().Be("session:toolkit");
        envelope.Data.Should().BeOfType<DiceRolledEvent>();
        envelope.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Constants

    [Fact]
    public void MaxConnectionsPerSession_IsReasonableValue()
    {
        SessionBroadcastService.MaxConnectionsPerSession.Should().BeGreaterThan(0);
        SessionBroadcastService.MaxConnectionsPerSession.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void MaxEventsPerSecond_IsReasonableValue()
    {
        SessionBroadcastService.MaxEventsPerSecond.Should().BeGreaterThan(0);
        SessionBroadcastService.MaxEventsPerSecond.Should().BeLessThanOrEqualTo(1000);
    }

    [Fact]
    public void EventBufferSize_IsReasonableValue()
    {
        SessionBroadcastService.EventBufferSize.Should().BeGreaterThan(0);
        SessionBroadcastService.EventBufferSize.Should().BeLessThanOrEqualTo(10000);
    }

    #endregion
}
