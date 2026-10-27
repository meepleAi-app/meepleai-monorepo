using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.Services;
using Api.Tests.BoundedContexts.SharedGameCatalog.Application.Handlers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #4603 — <see cref="SharedGameCacheInvalidator"/>, l'unico punto in cui un comando toglie dalla cache il
/// dettaglio e la lista del catalogo.
/// </summary>
/// <remarks>
/// Fissa ciò da cui dipendono gli handler e che un cambio silenzioso romperebbe: i tag (e che il
/// dettaglio sia per gioco), i due passi per tag nell'ordine <see cref="HybridCache"/> nativo e poi
/// wrapper (decisione di #4603), la politica d'errore (l'evizione avviene DOPO il commit: se fallisce dopo i
/// tentativi della retry policy si registra e non si rilancia, ogni passo gira comunque, #4603 punto 2) e il
/// token (quello della richiesta si ignora: un client che si disconnette fra commit ed evizione non la salta).
/// La retry policy dei doppi ESEGUE il delegato, come quella vera, altrimenti il doppio della cache non viene
/// esercitato.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameCacheInvalidatorTests
{
    private const string ListTag = "search-games";

    private readonly DetailCacheEvictionDoubles _doubles = new();

    private static string DetailTag(Guid gameId) => $"shared-game:{gameId}";

    [Fact]
    public async Task EvictDetail_TouchesOnlyTheDetailTagOfThatGame_NativeThenWrapper()
    {
        var gameId = Guid.NewGuid();

        await _doubles.Invalidator.EvictDetailAsync(gameId, TestContext.Current.CancellationToken);

        _doubles.Events.Should().Equal(
            new[] { $"native:{DetailTag(gameId)}", $"wrapper:{DetailTag(gameId)}" },
            "errata and FAQ change the detail and nothing the list shows: the list tag stays");
    }

    [Fact]
    public async Task EvictDetailAndList_EvictsTheListFirst_ThenTheDetailOfThatGame()
    {
        var gameId = Guid.NewGuid();

        await _doubles.Invalidator.EvictDetailAndListAsync(gameId, TestContext.Current.CancellationToken);

        _doubles.Events.Should().Equal(
            new[]
            {
                $"native:{ListTag}", $"wrapper:{ListTag}",
                $"native:{DetailTag(gameId)}", $"wrapper:{DetailTag(gameId)}",
            },
            "every tag gets both steps, the native one first (#4603): it is local and immediate, and it "
            + "reaches an entry written with the raw HybridCache on this replica, with or without Redis");
    }

    [Fact]
    public async Task EvictDetailsAndList_ForManyGames_EvictsTheListOnce_AndEachDetailOnce()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await _doubles.Invalidator.EvictDetailsAndListAsync([first, second], TestContext.Current.CancellationToken);

        _doubles.Events.Should().Equal(
            new[]
            {
                $"native:{ListTag}", $"wrapper:{ListTag}",
                $"native:{DetailTag(first)}", $"wrapper:{DetailTag(first)}",
                $"native:{DetailTag(second)}", $"wrapper:{DetailTag(second)}",
            });
    }

    [Fact]
    public async Task EvictDetailsAndList_ForNoGames_DoesNothing_ListIncluded()
    {
        await _doubles.Invalidator.EvictDetailsAndListAsync([], TestContext.Current.CancellationToken);

        _doubles.Events.Should().BeEmpty("a bulk action that changed no game has nothing to evict");
    }

    [Fact]
    public async Task Eviction_UsesTheRetryPolicy_WithTheMetricNamesOfTheListAndOfTheDetail()
    {
        var names = new List<string>();
        _doubles.RetryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, ValueTask>, string, CancellationToken>((op, name, ct) =>
            {
                names.Add(name);
                return op(ct).AsTask();
            });

        await _doubles.Invalidator.EvictDetailAndListAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        names.Should().Equal(
            "shared-games.list", "shared-games.list", "shared-games.detail", "shared-games.detail");
    }

    /// <summary>
    /// #4603 punto 2. L'evizione sta DOPO il commit: se il wrapper si arrende dopo i tentativi (Redis giù), la
    /// scrittura è già salvata e il chiamante non deve rispondere 500 (l'admin riproverebbe e un'errata o una
    /// FAQ si duplicherebbe). Il passo nativo, che viene prima, ha già pulito la cache locale; la voce delle
    /// altre repliche scade da sola.
    /// </summary>
    [Fact]
    public async Task WhenTheWrapperStepGivesUp_TheCallerIsNotTold_AndTheNativeStepAlreadyRan()
    {
        var gameId = Guid.NewGuid();
        var calls = 0;

        // La prima chiamata (il passo nativo) passa; la seconda (il wrapper) si arrende.
        _doubles.RetryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, ValueTask>, string, CancellationToken>((op, _, ct) =>
                ++calls == 1 ? op(ct).AsTask() : Task.FromException(new InvalidOperationException("redis down")));

        var act = () => _doubles.Invalidator.EvictDetailAsync(gameId, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync("the write is already committed: a cache that cannot be cleared must not fail it");
        _doubles.Events.Should().Equal(
            new[] { $"native:{DetailTag(gameId)}" },
            "the local step ran before the wrapper gave up, and the wrapper step did not complete");
    }

    /// <summary>
    /// Il caso speculare: se si arrende il passo nativo, il wrapper (e con lui il messaggio alle altre repliche)
    /// gira lo stesso. Prima il rilancio lo saltava.
    /// </summary>
    [Fact]
    public async Task WhenTheNativeStepGivesUp_TheWrapperStepStillRuns_AndTheCallerIsNotTold()
    {
        var gameId = Guid.NewGuid();
        var calls = 0;

        // La prima chiamata (il passo nativo) si arrende; il wrapper deve partire comunque.
        _doubles.RetryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, ValueTask>, string, CancellationToken>((op, _, ct) =>
                ++calls == 1 ? Task.FromException(new InvalidOperationException("l2 down")) : op(ct).AsTask());

        var act = () => _doubles.Invalidator.EvictDetailAsync(gameId, TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        _doubles.Events.Should().Equal(
            new[] { $"wrapper:{DetailTag(gameId)}" },
            "the wrapper step also publishes to the other replicas: one failed step must not take it away");
        calls.Should().Be(2);
    }

    /// <summary>
    /// Un'evizione che fallisce non ferma le altre: se la lista non si riesce a togliere, il dettaglio di
    /// ciascun gioco viene tolto lo stesso.
    /// </summary>
    [Fact]
    public async Task WhenTheListEvictionFails_TheDetailsOfEveryGameAreStillEvicted()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var calls = 0;

        // Le prime due chiamate (nativo e wrapper della lista) si arrendono; le altre passano.
        _doubles.RetryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, ValueTask>, string, CancellationToken>((op, _, ct) =>
                ++calls <= 2 ? Task.FromException(new InvalidOperationException("redis down")) : op(ct).AsTask());

        var act = () => _doubles.Invalidator.EvictDetailsAndListAsync([first, second], TestContext.Current.CancellationToken);

        await act.Should().NotThrowAsync();
        _doubles.Events.Should().Equal(
            $"native:{DetailTag(first)}", $"wrapper:{DetailTag(first)}",
            $"native:{DetailTag(second)}", $"wrapper:{DetailTag(second)}");
    }

    /// <summary>
    /// #4603 punto 2, l'altra metà: se il client si disconnette fra il commit e l'evizione, il token della
    /// richiesta è annullato; usarlo salterebbe l'evizione e la voce vecchia resterebbe. L'evizione ignora il
    /// token (come `HybridCacheInvalidationSubscriber`) e si affida ai tentativi della retry policy.
    /// </summary>
    [Fact]
    public async Task Eviction_IgnoresTheRequestToken_AndRunsEvenWhenTheClientHasGone()
    {
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        var cancelled = source.Token;
        var gameId = Guid.NewGuid();

        await _doubles.Invalidator.EvictDetailAndListAsync(gameId, cancelled);

        _doubles.Events.Should().Equal(
            $"native:{ListTag}", $"wrapper:{ListTag}",
            $"native:{DetailTag(gameId)}", $"wrapper:{DetailTag(gameId)}");
        _doubles.RetryPolicy.Verify(
            p => p.ExecuteAsync(It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), CancellationToken.None),
            Times.Exactly(4));
        _doubles.Cache.Verify(c => c.RemoveByTagAcrossReplicasAsync(ListTag, CancellationToken.None), Times.Once);
        _doubles.Cache.Verify(c => c.RemoveByTagAcrossReplicasAsync(DetailTag(gameId), CancellationToken.None), Times.Once);
        _doubles.HybridCache.Verify(c => c.RemoveByTagAsync(ListTag, CancellationToken.None), Times.Once);
        _doubles.HybridCache.Verify(c => c.RemoveByTagAsync(DetailTag(gameId), CancellationToken.None), Times.Once);
    }

    /// <summary>
    /// Un'evizione che fallisce non risale a chi ha scritto: si vede nel log, con il tag, l'operazione e il passo
    /// (nativo o wrapper) e con l'eccezione. Senza, una cache che resta vecchia fino alla scadenza non lascerebbe
    /// nessuna traccia.
    /// </summary>
    [Fact]
    public async Task WhenAStepGivesUp_TheFailureIsLoggedWithTheTagTheOperationAndTheStep()
    {
        var gameId = Guid.NewGuid();
        var logger = new RecordingLogger();
        var invalidator = new SharedGameCacheInvalidator(
            _doubles.Cache.Object, _doubles.RetryPolicy.Object, _doubles.HybridCache.Object, logger);
        var failure = new InvalidOperationException("redis down");
        var calls = 0;
        _doubles.RetryPolicy
            .Setup(p => p.ExecuteAsync(
                It.IsAny<Func<CancellationToken, ValueTask>>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, ValueTask>, string, CancellationToken>((op, _, ct) =>
                ++calls == 2 ? Task.FromException(failure) : op(ct).AsTask());

        await invalidator.EvictDetailAsync(gameId, TestContext.Current.CancellationToken);

        var entry = logger.Entries.Should().ContainSingle("only the wrapper step failed").Subject;
        entry.Level.Should().Be(LogLevel.Error);
        entry.Exception.Should().BeSameAs(failure);
        entry.Message.Should().Contain(DetailTag(gameId)).And.Contain("shared-games.detail").And.Contain("wrapper");
    }

    [Fact]
    public async Task WhenNothingFails_NothingIsLogged()
    {
        var logger = new RecordingLogger();
        var invalidator = new SharedGameCacheInvalidator(
            _doubles.Cache.Object, _doubles.RetryPolicy.Object, _doubles.HybridCache.Object, logger);

        await invalidator.EvictDetailAndListAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_RejectsMissingDependencies()
    {
        var cache = new Mock<IHybridCacheService>().Object;
        var retry = new Mock<ICacheInvalidationRetryPolicy>().Object;
        var hybrid = new Mock<HybridCache>().Object;

        FluentActions.Invoking(() => new SharedGameCacheInvalidator(null!, retry, hybrid))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SharedGameCacheInvalidator(cache, null!, hybrid))
            .Should().Throw<ArgumentNullException>();
        FluentActions.Invoking(() => new SharedGameCacheInvalidator(cache, retry, null!))
            .Should().Throw<ArgumentNullException>();
    }

    private sealed class RecordingLogger : ILogger<SharedGameCacheInvalidator>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
    }
}
