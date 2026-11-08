using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Infrastructure;

/// <summary>
/// <para>
/// <b>Niente orologio reale sul percorso che passa</b> (#4193). <c>SubscribeAsync</c> è un iteratore
/// lazy che si registra alla prima <c>MoveNextAsync</c>, in un momento deciso dal thread pool. #3711
/// aveva già sostituito il <c>Task.Delay(50)</c> prima delle pubblicazioni con un'attesa sul numero di
/// connessioni, ma i sottoscrittori vivevano ancora sotto <c>CancellationTokenSource</c> a 2 s o 5 s:
/// sotto il carico della suite il token scadeva prima che il test finisse i suoi passi, e il test
/// falliva su un servizio corretto (caduto sulla CI di #4192). Misurato per perturbazione: con l'avvio
/// di ogni sottoscrizione ritardato di 2,5 s, 13 test su 20 della versione precedente fallivano.
/// </para>
/// <para>
/// Ora la vita dei sottoscrittori la decide il test, che li cancella esplicitamente, e ogni attesa ha
/// un limite di <see cref="SafetyNet"/>: scatta solo se qualcosa si blocca, e allora fallisce dicendo
/// che cosa non è successo. Fa eccezione il test di cancellazione, in cui il token che scade è il
/// comportamento sotto verifica.
/// </para>
/// <para>
/// I test «negativi» (un evento che <b>non</b> deve arrivare) usano un evento-marcatore pubblicato
/// dopo: ogni canale è ordinato, quindi quando il marcatore arriva, l'evento sbagliato sarebbe già
/// arrivato prima. Prima cancellavano dopo un istante e potevano passare per niente.
/// </para>
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionBroadcastServiceTests : IDisposable
{
    /// <summary>Limite superiore per non appendere la suite. Mai sul percorso che passa.</summary>
    private static readonly TimeSpan SafetyNet = TimeSpan.FromSeconds(30);

    /// <summary>Punteggio dell'evento-marcatore dei test negativi.</summary>
    private const decimal MarkerScore = 999;

    private readonly SessionBroadcastService _service;

    public SessionBroadcastServiceTests()
    {
        _service = CreateService();
    }

    /// <summary>
    /// Builds a SessionBroadcastService wired with the in-process fallback sequence
    /// provider (no Redis). Issue #2561 SP2 T6.
    /// </summary>
    private static SessionBroadcastService CreateService() =>
        new(
            NullLogger<SessionBroadcastService>.Instance,
            new RedisSessionSequenceProvider(null, NullLogger<RedisSessionSequenceProvider>.Instance));

    public void Dispose()
    {
        _service.Dispose();
    }

    /// <summary>
    /// Attende che il pool della sessione contenga esattamente <paramref name="expected"/> subscriber.
    /// </summary>
    /// <remarks>
    /// Sostituisce l'attesa fissa (<c>Task.Delay(50)</c>) con cui questi test indovinavano la
    /// registrazione del subscriber (#3711): <c>SubscribeAsync</c> è un iteratore lazy, e
    /// <c>pool.TryAdd</c> gira alla prima <c>MoveNextAsync</c> del consumer. Allo scadere di
    /// <see cref="SafetyNet"/> l'eccezione dice che cosa non è successo, invece di lasciarlo dedurre
    /// dall'asserzione a valle. Stesso approccio di <c>WaitForZsetCardinalityAsync</c> in
    /// <c>SessionBroadcastReplayTests</c>.
    /// </remarks>
    private Task WaitForConnectionCountAsync(Guid sessionId, int expected) =>
        WaitForConnectionCountAsync(_service, sessionId, expected);

    /// <inheritdoc cref="WaitForConnectionCountAsync(Guid, int)"/>
    private static async Task WaitForConnectionCountAsync(
        SessionBroadcastService service,
        Guid sessionId,
        int expected)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();

        int actual;
        while ((actual = service.GetConnectionCount(sessionId)) != expected)
        {
            if (elapsed.Elapsed > SafetyNet)
            {
                throw new TimeoutException(
                    $"Il pool della sessione {sessionId} non ha raggiunto {expected} subscriber entro " +
                    $"{SafetyNet.TotalSeconds:0}s (osservati: {actual}). Il subscriber non si è registrato, " +
                    "oppure si è disconnesso prima del previsto.");
            }

            await Task.Delay(5);
        }
    }

    private static ScoreUpdatedEvent Score(Guid sessionId, Guid participantId, decimal score) =>
        new()
        {
            SessionId = sessionId,
            ParticipantId = participantId,
            ScoreEntryId = Guid.NewGuid(),
            NewScore = score
        };

    private static bool IsMarker(SseEventEnvelope envelope) =>
        envelope.Data is ScoreUpdatedEvent { NewScore: MarkerScore };

    /// <summary>
    /// Sottoscrive in background e raccoglie gli eventi finché <paramref name="stopAfter"/> non dice di
    /// fermarsi. La sottoscrizione dura finché il test non cancella <paramref name="ct"/>.
    /// </summary>
    private Task CollectAsync(
        Guid sessionId,
        Guid userId,
        string? lastEventId,
        List<SseEventEnvelope> received,
        Func<SseEventEnvelope, bool> stopAfter,
        CancellationToken ct) =>
        Task.Run(async () =>
        {
            await foreach (var evt in _service.SubscribeAsync(sessionId, userId, lastEventId, ct))
            {
                received.Add(evt);
                if (stopAfter(evt)) break;
            }
        }, CancellationToken.None);

    /// <summary>Sottoscrive in background senza raccogliere niente, fino alla cancellazione.</summary>
    private Task HoldAsync(Guid sessionId, Guid userId, CancellationToken ct) =>
        Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in _service.SubscribeAsync(sessionId, userId, null, ct)) { }
            }
            catch (OperationCanceledException)
            {
                // Expected: the test cancels the subscription when it is done with it.
            }
        }, CancellationToken.None);

    #region SubscribeAsync

    [Fact]
    public async Task SubscribeAsync_ReturnsAsyncEnumerable_ThatCompletsOnCancellation()
    {
        // Arrange — here the expiring token IS the behaviour under test, so a timed token is right.
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        var act = async () =>
        {
            await foreach (var _ in _service.SubscribeAsync(sessionId, userId, null, cts.Token))
            {
                // Should timeout waiting for events
            }
        };
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SubscribeAsync_IncrementsConnectionCount()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        // Act
        var subscription = HoldAsync(sessionId, Guid.NewGuid(), cts.Token);
        await WaitForConnectionCountAsync(sessionId, 1);

        // Assert
        _service.GetConnectionCount(sessionId).Should().Be(1);

        await cts.CancelAsync();
        await subscription.WaitAsync(SafetyNet);
    }

    [Fact]
    public async Task SubscribeAsync_DecrementsConnectionCount_OnDisconnect()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        var subscription = HoldAsync(sessionId, Guid.NewGuid(), cts.Token);
        await WaitForConnectionCountAsync(sessionId, 1);
        _service.GetConnectionCount(sessionId).Should().Be(1);

        // Act
        await cts.CancelAsync();
        await subscription.WaitAsync(SafetyNet);
        await WaitForConnectionCountAsync(sessionId, 0);

        // Assert
        _service.GetConnectionCount(sessionId).Should().Be(0);
    }

    [Fact]
    public async Task SubscribeAsync_RejectsConnection_WhenPoolIsFull()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var poolCts = new CancellationTokenSource();
        var subscriptions = new List<Task>();

        // Fill the pool to max
        for (var i = 0; i < SessionBroadcastService.MaxConnectionsPerSession; i++)
        {
            subscriptions.Add(HoldAsync(sessionId, Guid.NewGuid(), poolCts.Token));
        }

        await WaitForConnectionCountAsync(sessionId, SessionBroadcastService.MaxConnectionsPerSession);
        _service.GetConnectionCount(sessionId).Should().Be(SessionBroadcastService.MaxConnectionsPerSession);

        // Act - attempt to add one more. Its token is never cancelled: a rejected subscription ends by
        // itself (yield break); one that was wrongly accepted would wait for events and hit the safety net.
        var overflowEvents = new List<SseEventEnvelope>();
        using var overflowCts = new CancellationTokenSource();
        var overflow = CollectAsync(sessionId, Guid.NewGuid(), null, overflowEvents, _ => false, overflowCts.Token);
        await overflow.WaitAsync(SafetyNet);

        // Assert - rejected (yield break), should receive no events
        overflowEvents.Should().BeEmpty();

        // Cleanup
        await poolCts.CancelAsync();
        await Task.WhenAll(subscriptions).WaitAsync(SafetyNet);
    }

    [Fact]
    public async Task SubscribeAsync_MultipleSubscribers_AllReceiveEvents()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var received1 = new List<SseEventEnvelope>();
        var received2 = new List<SseEventEnvelope>();

        var sub1 = CollectAsync(sessionId, Guid.NewGuid(), null, received1, _ => true, cts.Token);
        var sub2 = CollectAsync(sessionId, Guid.NewGuid(), null, received2, _ => true, cts.Token);

        await WaitForConnectionCountAsync(sessionId, 2);

        // Act
        await _service.PublishAsync(sessionId, Score(sessionId, Guid.NewGuid(), 10));

        await Task.WhenAll(sub1, sub2).WaitAsync(SafetyNet);

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
        using var cts = new CancellationTokenSource();
        var receivedEvents = new List<SseEventEnvelope>();

        var sub = CollectAsync(sessionId, Guid.NewGuid(), null, receivedEvents, _ => true, cts.Token);
        await WaitForConnectionCountAsync(sessionId, 1);

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

        await sub.WaitAsync(SafetyNet);

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
        using var cts = new CancellationTokenSource();
        var session1Events = new List<SseEventEnvelope>();

        var sub1 = CollectAsync(session1Id, Guid.NewGuid(), null, session1Events, _ => true, cts.Token);
        await WaitForConnectionCountAsync(session1Id, 1);

        // Act - the other session's event goes out FIRST: if it leaked into session 1, it would be the
        // first (and, given the break, the only) event session 1 receives.
        await _service.PublishAsync(session2Id, Score(session2Id, Guid.NewGuid(), 20));
        await _service.PublishAsync(session1Id, Score(session1Id, Guid.NewGuid(), 10));

        await sub1.WaitAsync(SafetyNet);

        // Assert - session1 only gets session1 events
        session1Events.Should().ContainSingle()
            .Which.Data.Should().BeOfType<ScoreUpdatedEvent>()
            .Which.NewScore.Should().Be(10);
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
        using var cts = new CancellationTokenSource();
        var received1 = new List<SseEventEnvelope>();
        var received2 = new List<SseEventEnvelope>();

        var sub1 = CollectAsync(sessionId, user1, null, received1, _ => true, cts.Token);
        var sub2 = CollectAsync(sessionId, user2, null, received2, _ => true, cts.Token);

        await WaitForConnectionCountAsync(sessionId, 2);

        // Act
        await _service.PublishAsync(sessionId, Score(sessionId, user1, 10), EventVisibility.Public);

        await Task.WhenAll(sub1, sub2).WaitAsync(SafetyNet);

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
        using var cts = new CancellationTokenSource();
        var targetReceived = new List<SseEventEnvelope>();
        var otherReceived = new List<SseEventEnvelope>();

        var targetSub = CollectAsync(sessionId, targetUser, null, targetReceived, _ => true, cts.Token);
        var otherSub = CollectAsync(sessionId, otherUser, null, otherReceived, IsMarker, cts.Token);

        await WaitForConnectionCountAsync(sessionId, 2);

        // Act - private event for targetUser only, then a public marker
        await _service.PublishAsync(sessionId, Score(sessionId, targetUser, 10), EventVisibility.PrivateTo(targetUser));
        await _service.PublishAsync(sessionId, Score(sessionId, otherUser, MarkerScore), EventVisibility.Public);

        await Task.WhenAll(targetSub, otherSub).WaitAsync(SafetyNet);

        // Assert - the other user's stream is ordered: had the private event reached it, it would sit
        // before the marker. Cancelling "a moment later", as before, could not tell.
        targetReceived.Should().ContainSingle()
            .Which.Data.Should().BeOfType<ScoreUpdatedEvent>()
            .Which.NewScore.Should().Be(10);
        otherReceived.Should().ContainSingle().Which.Should().Match<SseEventEnvelope>(e => IsMarker(e));
    }

    #endregion

    #region Last-Event-ID Reconnection

    [Fact]
    public async Task SubscribeAsync_WithLastEventId_ReplaysBufferedEvents()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var keepAliveCts = new CancellationTokenSource();

        // Keep a background subscriber alive to prevent pool cleanup: an empty pool is removed together
        // with its replay buffer. It lives until the test says so, not for 5 wall-clock seconds.
        var keepAlive = HoldAsync(sessionId, Guid.NewGuid(), keepAliveCts.Token);
        await WaitForConnectionCountAsync(sessionId, 1);

        // Subscribe and receive events to build buffer
        using var cts1 = new CancellationTokenSource();
        var firstEvents = new List<SseEventEnvelope>();
        var sub1 = CollectAsync(sessionId, userId, null, firstEvents, _ => firstEvents.Count >= 3, cts1.Token);

        await WaitForConnectionCountAsync(sessionId, 2);

        // Publish 3 events
        for (var i = 0; i < 3; i++)
        {
            await _service.PublishAsync(sessionId, Score(sessionId, userId, (i + 1) * 10), EventVisibility.Public);
        }

        await sub1.WaitAsync(SafetyNet);
        firstEvents.Should().HaveCount(3);

        // Act: Reconnect with Last-Event-ID of the first event → should replay events 2 and 3
        var lastEventId = firstEvents[0].Id;
        using var cts2 = new CancellationTokenSource();
        var reconnectedEvents = new List<SseEventEnvelope>();
        var reconnect = CollectAsync(
            sessionId, userId, lastEventId, reconnectedEvents, _ => reconnectedEvents.Count >= 2, cts2.Token);
        await reconnect.WaitAsync(SafetyNet);

        // Cleanup keep-alive
        await keepAliveCts.CancelAsync();
        await keepAlive.WaitAsync(SafetyNet);

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

        // Keep the pool, and so the buffer, alive. Without it the first subscriber's disconnect removed
        // the pool, and "nothing replayed" held for any id because the buffer was gone.
        using var keepAliveCts = new CancellationTokenSource();
        var keepAlive = HoldAsync(sessionId, Guid.NewGuid(), keepAliveCts.Token);
        await WaitForConnectionCountAsync(sessionId, 1);

        // Subscribe and publish one event so the buffer is not empty
        using var cts1 = new CancellationTokenSource();
        var firstEvents = new List<SseEventEnvelope>();
        var sub = CollectAsync(sessionId, userId, null, firstEvents, _ => true, cts1.Token);
        await WaitForConnectionCountAsync(sessionId, 2);

        await _service.PublishAsync(sessionId, Score(sessionId, userId, 10), EventVisibility.Public);
        await sub.WaitAsync(SafetyNet);
        await WaitForConnectionCountAsync(sessionId, 1);

        // Act: Reconnect with an invalid id, then publish a marker. A replay would sit before it.
        using var cts2 = new CancellationTokenSource();
        var reconnectedEvents = new List<SseEventEnvelope>();
        var reconnect = CollectAsync(sessionId, userId, "invalid-id", reconnectedEvents, IsMarker, cts2.Token);
        await WaitForConnectionCountAsync(sessionId, 2);

        await _service.PublishAsync(sessionId, Score(sessionId, userId, MarkerScore), EventVisibility.Public);
        await reconnect.WaitAsync(SafetyNet);

        await keepAliveCts.CancelAsync();
        await keepAlive.WaitAsync(SafetyNet);

        // Assert - should not replay anything (ID not found in buffer): only the marker arrived
        reconnectedEvents.Should().ContainSingle().Which.Should().Match<SseEventEnvelope>(e => IsMarker(e));
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
        using var cts = new CancellationTokenSource();

        var sub1 = HoldAsync(sessionId, Guid.NewGuid(), cts.Token);
        var sub2 = HoldAsync(sessionId, Guid.NewGuid(), cts.Token);

        await WaitForConnectionCountAsync(sessionId, 2);

        // Act & Assert
        _service.GetConnectionCount(sessionId).Should().Be(2);

        await cts.CancelAsync();
        await Task.WhenAll(sub1, sub2).WaitAsync(SafetyNet);
    }

    #endregion

    #region DisconnectAllAsync

    [Fact]
    public async Task DisconnectAllAsync_DisconnectsAllSubscribers()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        using var cts = new CancellationTokenSource();

        // The token is never cancelled: only DisconnectAllAsync can end these subscriptions.
        var sub1 = HoldAsync(sessionId, Guid.NewGuid(), cts.Token);
        var sub2 = HoldAsync(sessionId, Guid.NewGuid(), cts.Token);

        await WaitForConnectionCountAsync(sessionId, 2);
        _service.GetConnectionCount(sessionId).Should().Be(2);

        // Act
        await _service.DisconnectAllAsync(sessionId);

        // Assert - both subscriptions actually end (before #4193 the test never checked it) and the
        // pool is empty
        await Task.WhenAll(sub1, sub2).WaitAsync(SafetyNet);
        await WaitForConnectionCountAsync(sessionId, 0);
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
        using var cts = new CancellationTokenSource();
        var receivedEvents = new List<SseEventEnvelope>();

        var sub = CollectAsync(sessionId, userId, null, receivedEvents, _ => true, cts.Token);
        await WaitForConnectionCountAsync(sessionId, 1);

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
        var publishedFrom = DateTime.UtcNow;
        await _service.PublishAsync(sessionId, testEvent);
        await sub.WaitAsync(SafetyNet);
        var receivedBy = DateTime.UtcNow;

        // Assert
        var envelope = receivedEvents[0];
        // Issue #2561 SP2 T6: Id is now a monotonic D20 sequence (no "{sessionId:N}-" prefix).
        envelope.Id.Should().HaveLength(20).And.MatchRegex(@"^\d{20}$");
        envelope.EventType.Should().Be("session:toolkit");
        envelope.Data.Should().BeOfType<DiceRolledEvent>();
        // Between two measured instants, not "close to now": that tolerance is a wall-clock bet too.
        envelope.Timestamp.Should().BeOnOrAfter(publishedFrom).And.BeOnOrBefore(receivedBy);
    }

    #endregion

    #region PublishEnvelopeAsync

    [Fact]
    public async Task PublishEnvelopeAsync_delivers_explicit_event_type()
    {
        var svc = CreateService();
        var sid = Guid.NewGuid(); var uid = Guid.NewGuid();
        using var cts = new CancellationTokenSource();
        var received = new List<SseEventEnvelope>();
        var sub = Task.Run(async () => {
            await foreach (var e in svc.SubscribeAsync(sid, uid, null, cts.Token)) { received.Add(e); break; }
        });
        await WaitForConnectionCountAsync(svc, sid, 1);
        await svc.PublishEnvelopeAsync(sid,
            new SseEventEnvelope { Id = "ignored", EventType = "session:score", Data = new { value = 7 } });
        await sub.WaitAsync(SafetyNet);
        Assert.Single(received);
        Assert.Equal("session:score", received[0].EventType);
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
