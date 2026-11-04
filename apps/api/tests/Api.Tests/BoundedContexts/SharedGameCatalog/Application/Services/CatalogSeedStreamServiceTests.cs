using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Unit tests for <see cref="CatalogSeedStreamService"/> (Issue #1903 M6.1).
/// Covers: null-guard, replay-to-new-subscriber, live delivery, multi-subscriber
/// fan-out, replay-buffer bounded eviction, and CT-driven subscriber cleanup.
/// </summary>
/// <remarks>
/// <para>
/// <c>SubscribeAsync</c> è un iteratore lazy: il subscriber si registra alla prima
/// <c>MoveNextAsync</c>, non alla chiamata. I test dei percorsi live non avviano un
/// consumer in <c>Task.Run</c> aspettando un <c>Task.Delay</c> che indovini quando si registra
/// (#4604): pilotano l'enumeratore a mano. La prima <c>MoveNextAsync</c> esegue il corpo
/// dell'iteratore sul thread chiamante fino alla prima sospensione o al primo
/// <c>yield return</c>: quando ritorna, il subscriber è registrato e il replay è stato
/// consegnato fino al primo elemento, oppure è vuoto (e allora la lettura resta in attesa del
/// canale live) — per costruzione, senza thread pool e senza orologio. Con un replay vuoto, un
/// evento pubblicato da lì in poi può arrivare solo dal canale live.
/// </para>
/// <para>
/// Nessun test ha un budget a tempo che decida l'esito: <see cref="SafetyNet"/> serve solo a
/// non appendere la suite se qualcosa si blocca, e non sta mai sul percorso che passa.
/// </para>
/// <para>
/// I subscriber sono un <see cref="Subscription"/>, non un enumeratore nudo in un
/// <c>await using</c>: vedi la nota su quella classe.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSeedStreamServiceTests
{
    /// <summary>Limite superiore per non appendere la suite. Mai sul percorso che passa.</summary>
    private static readonly TimeSpan SafetyNet = TimeSpan.FromSeconds(30);

    private static CatalogSeedStreamService CreateService() =>
        new(NullLogger<CatalogSeedStreamService>.Instance);

    private static BatchStartedEvent NewBatchStarted(Guid? id = null, DateTime? occurredAt = null) =>
        new(id ?? Guid.NewGuid(),
            new[] { Guid.NewGuid(), Guid.NewGuid() },
            occurredAt ?? DateTime.UtcNow);

    /// <summary>
    /// Un subscriber pilotato a mano. Ricorda l'ultima <c>MoveNextAsync</c> e, al dispose, annulla
    /// il token e la attende PRIMA di rilasciare l'enumeratore.
    /// </summary>
    /// <remarks>
    /// Su un iteratore generato dal compilatore, <c>DisposeAsync</c> mentre una
    /// <c>MoveNextAsync</c> è in corso lancia <c>NotSupportedException</c>. Con un semplice
    /// <c>await using</c>, se un'asserzione fallisce (o scatta <see cref="SafetyNet"/>) mentre una
    /// lettura è in sospeso, quell'eccezione prende il posto di quella vera e il test fallisce
    /// senza dire perché. Verificato rompendo apposta un'asserzione: usciva
    /// «NotSupportedException : Specified method is not supported», non il messaggio di
    /// FluentAssertions.
    /// </remarks>
    private sealed class Subscription : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly IAsyncEnumerator<CatalogSeedStreamEvent> _enumerator;
        private Task<bool>? _pending;

        public Subscription(CatalogSeedStreamService svc) =>
            _enumerator = svc.SubscribeAsync(_cts.Token).GetAsyncEnumerator();

        public CatalogSeedStreamEvent Current => _enumerator.Current;

        public Task<bool> MoveNextAsync() => _pending = _enumerator.MoveNextAsync().AsTask();

        public Task CancelAsync() => _cts.CancelAsync();

        public async ValueTask DisposeAsync()
        {
            await _cts.CancelAsync();

            if (_pending is { IsCompleted: false } pending)
            {
                try
                {
                    await pending.WaitAsync(SafetyNet);
                }
                catch (OperationCanceledException)
                {
                    // atteso: la lettura in sospeso è stata annullata
                }
            }

            await _enumerator.DisposeAsync();
            _cts.Dispose();
        }
    }

    [Fact]
    public async Task PublishAsync_Throws_OnNullEvent()
    {
        var svc = CreateService();

        var act = async () => await svc.PublishAsync(null!, default);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task PublishAsync_BeforeSubscribe_ReplaysToNewSubscriber()
    {
        var svc = CreateService();
        var evt = NewBatchStarted();
        await svc.PublishAsync(evt);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(150));

        var received = new List<CatalogSeedStreamEvent>();
        try
        {
            await foreach (var item in svc.SubscribeAsync(cts.Token))
            {
                received.Add(item);
            }
        }
        catch (OperationCanceledException)
        {
            // expected — CT cancels the subscriber loop after replay drains
        }

        received.Should().ContainSingle();
        received[0].Should().Be(evt);
    }

    [Fact]
    public async Task SubscribeAsync_LiveEvent_DeliveredToActiveSubscriber()
    {
        var svc = CreateService();
        await using var subscriber = new Subscription(svc);

        // Registra il subscriber ed esaurisce il replay (vuoto): resta in attesa di un evento live.
        var next = subscriber.MoveNextAsync();
        svc.SubscriberCount.Should().Be(1);
        next.IsCompleted.Should().BeFalse("nulla è stato pubblicato: il subscriber aspetta il canale live");

        var evt = NewBatchStarted();
        await svc.PublishAsync(evt);

        (await next.WaitAsync(SafetyNet)).Should().BeTrue();
        subscriber.Current.Should().Be(evt);
    }

    [Fact]
    public async Task SubscribeAsync_TwoSubscribers_BothReceiveLiveEvent()
    {
        var svc = CreateService();
        await using var subscriber1 = new Subscription(svc);
        await using var subscriber2 = new Subscription(svc);

        var next1 = subscriber1.MoveNextAsync();
        var next2 = subscriber2.MoveNextAsync();
        svc.SubscriberCount.Should().Be(2);

        var evt = NewBatchStarted();
        await svc.PublishAsync(evt);

        (await next1.WaitAsync(SafetyNet)).Should().BeTrue();
        (await next2.WaitAsync(SafetyNet)).Should().BeTrue();
        subscriber1.Current.Should().Be(evt);
        subscriber2.Current.Should().Be(evt);
    }

    [Fact]
    public async Task ReplayBuffer_LimitedTo200Events_DiscardsOldest()
    {
        var svc = CreateService();

        // Publish 250 events — the buffer should retain only the last 200.
        var allEvents = new List<BatchStartedEvent>(250);
        for (var i = 0; i < 250; i++)
        {
            var e = NewBatchStarted();
            allEvents.Add(e);
            await svc.PublishAsync(e);
        }

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        var replay = new List<CatalogSeedStreamEvent>();
        try
        {
            await foreach (var item in svc.SubscribeAsync(cts.Token))
            {
                replay.Add(item);
            }
        }
        catch (OperationCanceledException)
        {
            // expected — replay drained, CT cancels
        }

        replay.Should().HaveCount(200);
        // The 200 most-recent events should be the last 200 published.
        replay.Should().Equal(allEvents.GetRange(50, 200));
    }

    [Fact]
    public async Task SubscribeAsync_CancellationToken_StopsEnumeration_AutoRemovesSubscriber()
    {
        var svc = CreateService();
        await using var subscriber = new Subscription(svc);

        var next = subscriber.MoveNextAsync();
        svc.SubscriberCount.Should().Be(1);

        // Cancel the subscriber — finally{} block must run and remove the channel.
        await subscriber.CancelAsync();
        var cancelled = async () => await next.WaitAsync(SafetyNet);
        await cancelled.Should().ThrowAsync<OperationCanceledException>();

        // Il finally{} dell'iteratore gira prima che l'eccezione arrivi al chiamante: il
        // subscriber è già stato tolto. Prima questo test non lo asseriva — "publish non lancia"
        // vale anche con il subscriber ancora registrato.
        svc.SubscriberCount.Should().Be(0);

        // After cleanup, publishing must not throw — there are no live subscribers
        // and the publish loop has nothing to write to.
        var act = async () => await svc.PublishAsync(NewBatchStarted(), default);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SubscribeAsync_PublishedBeforeSubscribe_DeliveredExactlyOnceViaReplay()
    {
        // Regression: PR #1913 review finding — subscribe/publish race could deliver
        // the same event twice (once via replay snapshot, once via live channel).
        // The cutoff-based dedup must ensure exactly-once delivery for events
        // published strictly before SubscribeAsync registers its channel.
        var svc = CreateService();
        var evt = NewBatchStarted(occurredAt: DateTime.UtcNow.AddSeconds(-1));
        await svc.PublishAsync(evt);

        await using var subscriber = new Subscription(svc);
        (await subscriber.MoveNextAsync().WaitAsync(SafetyNet)).Should().BeTrue();
        subscriber.Current.Should().Be(evt);

        // Exactly once: se `evt` fosse finito anche nel canale live, la lettura successiva lo
        // renderebbe subito. Resta invece in attesa, perché non c'è nient'altro da consegnare.
        var second = subscriber.MoveNextAsync();
        second.IsCompleted.Should().BeFalse("nessun duplicato in coda dopo il replay");
    }

    [Fact]
    public async Task PublishAsync_AfterReplayDrain_DeliversLiveEvents()
    {
        // Edge-case check: a single subscriber should see BOTH the replay AND any
        // subsequent live events arriving after the replay completes.
        var svc = CreateService();
        var replayEvt = NewBatchStarted(occurredAt: DateTime.UtcNow.AddSeconds(-1));
        await svc.PublishAsync(replayEvt);

        await using var subscriber = new Subscription(svc);

        // Il replay si consegna in modo sincrono, prima di qualunque attesa sul canale.
        var first = subscriber.MoveNextAsync();
        first.IsCompleted.Should().BeTrue("il replay è già nel buffer");
        (await first).Should().BeTrue();
        subscriber.Current.Should().Be(replayEvt);

        // Replay esaurito: il subscriber è registrato e aspetta il live.
        var second = subscriber.MoveNextAsync();
        svc.SubscriberCount.Should().Be(1);
        second.IsCompleted.Should().BeFalse("il replay contiene un solo evento");

        var liveEvt = NewBatchStarted();
        await svc.PublishAsync(liveEvt);

        (await second.WaitAsync(SafetyNet)).Should().BeTrue();
        subscriber.Current.Should().Be(liveEvt);
    }

    [Fact]
    public async Task SubscribeAsync_EventPublishedBetweenCallAndFirstMoveNext_ArrivesOnceViaReplay()
    {
        // Contratto stabilito per #4604. SubscribeAsync è lazy: fra la chiamata e la prima
        // MoveNextAsync il subscriber NON è registrato, quindi un evento pubblicato in quella
        // finestra non può arrivare dal canale live. Non va per questo perso: sta nel replay
        // buffer e il subscriber lo riceve alla registrazione, una volta sola.
        var svc = CreateService();

        // SubscribeAsync è già stata chiamata, ma il corpo dell'iteratore non è partito.
        await using var subscriber = new Subscription(svc);

        var early = NewBatchStarted(occurredAt: DateTime.UtcNow.AddSeconds(-1));
        await svc.PublishAsync(early);
        svc.SubscriberCount.Should().Be(0, "la finestra esiste: il corpo dell'iteratore non è ancora partito");

        (await subscriber.MoveNextAsync().WaitAsync(SafetyNet)).Should().BeTrue();
        subscriber.Current.Should().Be(early);
        svc.SubscriberCount.Should().Be(1);

        // Una volta sola: la lettura successiva non rende di nuovo `early`, ma l'evento che segue.
        var next = subscriber.MoveNextAsync();
        var later = NewBatchStarted();
        await svc.PublishAsync(later);

        (await next.WaitAsync(SafetyNet)).Should().BeTrue();
        subscriber.Current.Should().Be(later);
    }
}
