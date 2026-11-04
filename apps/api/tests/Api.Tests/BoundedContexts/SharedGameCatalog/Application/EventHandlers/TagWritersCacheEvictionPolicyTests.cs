using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.BoundedContexts.KnowledgeBase.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Application.Jobs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.EventHandlers;

/// <summary>
/// #4659 — chi toglie dalla cache a mano (gli event handler e i job che non passano da
/// <c>SharedGameCacheInvalidator</c>) ha la politica dei comandi: un passo di evizione che si arrende dopo i
/// tentativi della retry policy si registra e non risale, e gli altri passi girano comunque.
/// </summary>
/// <remarks>
/// <para>
/// Prima risaliva. Il caso che pesava: <c>VectorDocumentIndexedForKbFlagHandler</c> gira in linea nella
/// pubblicazione di un PDF appena indicizzato, e un'eccezione dell'evizione portava a <c>Failed</c> un PDF la
/// cui indicizzazione era riuscita; la cache restava vecchia lo stesso (il flag è già salvato, un secondo
/// evento uscirebbe subito).
/// </para>
/// <para>
/// La retry policy dei doppi si arrende (<see cref="GivingUpRetryPolicy"/>) e registra i nomi di operazione e i
/// token: dice quali passi sono stati provati e da chi dipendono. Per il job di riconciliazione del flag KB,
/// che sta in un altro contesto, vedi <c>KbFlagDriftAuditJobTests</c>.
/// </para>
/// <para>
/// Gli handler di toolkit e di definizione d'agente sono qui per il motivo opposto: rilanciano ancora, di
/// proposito, perché il loro evento arriva dall'outbox, che ritenta l'handler che lancia.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "4659")]
public sealed class TagWritersCacheEvictionPolicyTests
{
    private const string CacheEvictionFailed = "Cache eviction failed";

    // ─── VectorDocumentIndexedForKbFlagHandler ──────────────────────────────────

    [Fact]
    public async Task KbFlagHandler_WhenEveryEvictionGivesUp_DoesNotThrow_TheFlagStaysSaved_AndEveryStepIsTried()
    {
        var sharedGameId = Guid.NewGuid();
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.SharedGames.Add(NewGameEntity(sharedGameId));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var policy = GivingUpRetryPolicy.Always();
        var logger = new RecordingLogger<VectorDocumentIndexedForKbFlagHandler>();
        var handler = new VectorDocumentIndexedForKbFlagHandler(
            db, Mock.Of<IHybridCacheService>(), policy, logger);
        using var caller = new CancellationTokenSource();

        var act = () => handler.Handle(
            new VectorDocumentIndexedEvent(Guid.NewGuid(), Guid.NewGuid(), chunkCount: 10, sharedGameId: sharedGameId),
            caller.Token);

        await act.Should().NotThrowAsync("the flag is already saved: a cache that cannot be cleared must not fail the PDF");
        (await db.SharedGames.AsNoTracking().SingleAsync(g => g.Id == sharedGameId, TestContext.Current.CancellationToken))
            .HasKnowledgeBase.Should().BeTrue();
        policy.Operations.Should().Equal("shared-games.list", "shared-games.detail", "shared-games.rails");
        logger.ErrorsContaining(CacheEvictionFailed).Should().Be(3);
        policy.Tokens.Should().OnlyContain(
            t => !t.CanBeCanceled,
            "the flag is saved: the evictions must not depend on the caller's token (a client that disconnects)");
    }

    // ─── PdfCoverGeneratedEventHandler ──────────────────────────────────────────

    [Fact]
    public async Task CoverHandler_WhenEveryEvictionGivesUp_DoesNotThrow_AndEveryStepIsTried()
    {
        var sharedGameId = Guid.NewGuid();
        var game = NewGame();
        game.SetPdfCoverR2Key("old-key");
        var repository = new Mock<ISharedGameRepository>();
        repository.Setup(r => r.GetByIdAsync(sharedGameId, It.IsAny<CancellationToken>())).ReturnsAsync(game);
        var policy = GivingUpRetryPolicy.Always();
        var logger = new RecordingLogger<PdfCoverGeneratedEventHandler>();
        var handler = new PdfCoverGeneratedEventHandler(
            repository.Object, Mock.Of<IUnitOfWork>(), Mock.Of<IHybridCacheService>(), policy, logger);
        using var caller = new CancellationTokenSource();

        var act = () => handler.Handle(
            new PdfCoverGeneratedEvent(Guid.NewGuid(), sharedGameId, "new-key", 0), caller.Token);

        await act.Should().NotThrowAsync();
        policy.Operations.Should().Equal("shared-games.list", "shared-games.detail");
        logger.ErrorsContaining(CacheEvictionFailed).Should().Be(2);
        policy.Tokens.Should().OnlyContain(t => !t.CanBeCanceled, "the cover is saved: the evictions must not depend on the caller's token");
    }

    // ─── ToolkitChangedForCatalogAggregatesHandler ──────────────────────────────

    /// <summary>
    /// Questo handler NON ha la politica «registra e prosegui», di proposito: il suo evento arriva dall'outbox,
    /// che in staging e in produzione ritenta l'handler che lancia, e l'evizione è idempotente. Il rilancio è il
    /// ritentativo. Se qualcuno lo allinea agli altri, il ritentativo sparisce in silenzio.
    /// </summary>
    [Fact]
    public async Task ToolkitHandler_WhenTheListEvictionGivesUp_StillThrows_SoTheOutboxRetriesTheEvent()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(NewGameEntity(gameId));
        var toolkit = Toolkit.CreateDefault(gameId);
        db.Toolkits.Add(toolkit);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var policy = GivingUpRetryPolicy.ForOperation("shared-games.list");
        var handler = new ToolkitChangedForCatalogAggregatesHandler(
            db, CreateHybridCache(), policy, new RecordingLogger<ToolkitChangedForCatalogAggregatesHandler>());

        var act = () => handler.Handle(
            new ToolkitCreatedEvent(toolkit.Id, gameId, privateGameId: null, name: "Test Toolkit"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>("the outbox retries a handler that throws");
    }

    // ─── AgentDefinitionChangedForCatalogAggregatesHandler ──────────────────────

    /// <summary>Stessa scelta del toolkit: l'evento arriva dall'outbox e il rilancio è il ritentativo.</summary>
    [Fact]
    public async Task AgentHandler_WhenTheListEvictionGivesUp_StillThrows_SoTheOutboxRetriesTheEvent()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        var handler = new AgentDefinitionChangedForCatalogAggregatesHandler(
            db, CreateHybridCache(), GivingUpRetryPolicy.Always(),
            new RecordingLogger<AgentDefinitionChangedForCatalogAggregatesHandler>());

        var act = () => handler.Handle(
            new AgentDefinitionCreatedEvent(agentDefinitionId: Guid.NewGuid(), name: "Rules Agent"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>("the outbox retries a handler that throws");
    }

    // ─── ScheduledBulkInvalidationJob ───────────────────────────────────────────

    /// <summary>
    /// Un dettaglio che non si riesce a togliere non ferma gli altri: è una rete di sicurezza, e il resto della
    /// lista va comunque tolto. Il risultato dice quanti sono riusciti e quanti no.
    /// </summary>
    [Fact]
    public async Task BulkJob_WhenOneDetailEvictionGivesUp_TheOthersAreStillTried_AndTheResultSaysSo()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedTrafficAsync(db, games: 3);
        var policy = GivingUpRetryPolicy.ForCall(1); // 0 = la lista, 1 = il primo dettaglio
        var logger = new RecordingLogger<ScheduledBulkInvalidationJob>();
        var job = new ScheduledBulkInvalidationJob(db, CreateHybridCache(), policy, logger);
        using var quartz = new CancellationTokenSource();
        var context = JobContext(quartz.Token);

        await job.Execute(context.Object);

        policy.Operations.Should().HaveCount(4, "the list and each of the three details were tried");
        var result = context.Object.Result!;
        ReadInt(result, "DetailInvalidations").Should().Be(2);
        ReadInt(result, "FailedInvalidations").Should().Be(1);
        ReadInt(result, "SkippedInvalidations").Should().Be(0);
        ReadBool(result, "Success").Should().BeFalse("one step did not succeed");
        logger.ErrorsContaining(CacheEvictionFailed).Should().Be(1);
        policy.Tokens.Should().OnlyContain(t => t == quartz.Token, "the job runs under the scheduler's token");
    }

    /// <summary>
    /// Con la cache che non risponde ogni passo costa i suoi tentativi: tutta la lista farebbe durare il job più
    /// del suo intervallo. Dopo tre passi di fila che si arrendono il job smette e dice quanti ne ha lasciati.
    /// </summary>
    [Fact]
    public async Task BulkJob_WhenTheCacheStopsAnswering_StopsAfterThreeStepsInARow_AndSaysWhatItSkipped()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedTrafficAsync(db, games: 10);
        var policy = GivingUpRetryPolicy.Always();
        var logger = new RecordingLogger<ScheduledBulkInvalidationJob>();
        var job = new ScheduledBulkInvalidationJob(db, CreateHybridCache(), policy, logger);
        var context = JobContext(CancellationToken.None);

        await job.Execute(context.Object);

        policy.Operations.Should().HaveCount(
            ScheduledBulkInvalidationJob.MaxConsecutiveFailures,
            "the list and the first two details gave up, and the job stopped there");
        var result = context.Object.Result!;
        ReadInt(result, "FailedInvalidations").Should().Be(ScheduledBulkInvalidationJob.MaxConsecutiveFailures);
        ReadInt(result, "DetailInvalidations").Should().Be(0);
        ReadInt(result, "SkippedInvalidations").Should().Be(8, "ten games, two of them tried");
        ReadBool(result, "Success").Should().BeFalse();
        logger.ErrorsContaining(CacheEvictionFailed).Should().Be(ScheduledBulkInvalidationJob.MaxConsecutiveFailures);
    }

    /// <summary>
    /// «Di fila» vuol dire di fila: un passo riuscito azzera il conto, così due guasti isolati non fermano il
    /// job (un tag che non va non è la cache che non risponde).
    /// </summary>
    [Fact]
    public async Task BulkJob_ASuccessfulStepResetsTheCount_SoScatteredFailuresDoNotStopTheJob()
    {
        await using var db = TestDbContextFactory.CreateInMemoryDbContext();
        await SeedTrafficAsync(db, games: 6);
        // 0 = la lista (riesce), 1 e 2 si arrendono, 3 riesce, 4 e 5 si arrendono, 6 riesce
        var policy = GivingUpRetryPolicy.ForCalls(1, 2, 4, 5);
        var job = new ScheduledBulkInvalidationJob(
            db, CreateHybridCache(), policy, new RecordingLogger<ScheduledBulkInvalidationJob>());
        var context = JobContext(CancellationToken.None);

        await job.Execute(context.Object);

        policy.Operations.Should().HaveCount(7, "the list and all six details: four failures, but never three in a row");
        var result = context.Object.Result!;
        ReadInt(result, "FailedInvalidations").Should().Be(4);
        ReadInt(result, "DetailInvalidations").Should().Be(2);
        ReadInt(result, "SkippedInvalidations").Should().Be(0);
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private static int ReadInt(object result, string property) =>
        (int)result.GetType().GetProperty(property)!.GetValue(result)!;

    private static bool ReadBool(object result, string property) =>
        (bool)result.GetType().GetProperty(property)!.GetValue(result)!;

    private static async Task SeedTrafficAsync(MeepleAiDbContext db, int games)
    {
        for (var offset = 1; offset <= games; offset++)
        {
            db.Set<GameAnalyticsEventEntity>().Add(new GameAnalyticsEventEntity
            {
                Id = Guid.NewGuid(),
                GameId = Guid.NewGuid(),
                EventType = (int)GameEventType.View,
                UserId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow.AddMinutes(-offset),
            });
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Mock<IJobExecutionContext> JobContext(CancellationToken token)
    {
        var mock = new Mock<IJobExecutionContext>();
        mock.SetupGet(c => c.CancellationToken).Returns(token);
        mock.SetupGet(c => c.FireTimeUtc).Returns(DateTimeOffset.UtcNow);
        mock.SetupProperty(c => c.Result);
        return mock;
    }

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMemoryCache, MemoryCache>();
        services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(
            Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static SharedGameEntity NewGameEntity(Guid id) => new()
    {
        Id = id,
        Title = "Test Game",
        YearPublished = 2020,
        Description = "Desc",
        MinPlayers = 2,
        MaxPlayers = 4,
        PlayingTimeMinutes = 30,
        MinAge = 8,
        ImageUrl = string.Empty,
        ThumbnailUrl = string.Empty,
        Status = 2,
        CreatedBy = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    private static SharedGame NewGame() => SharedGame.Create(
        title: "Test Game",
        yearPublished: 2020,
        description: "Test description",
        minPlayers: 2,
        maxPlayers: 4,
        playingTimeMinutes: 60,
        minAge: 10,
        complexityRating: 2.5m,
        averageRating: 7.5m,
        imageUrl: "https://example.com/image.jpg",
        thumbnailUrl: "https://example.com/thumb.jpg",
        rules: null,
        createdBy: Guid.NewGuid());

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        private readonly List<(LogLevel Level, string Message)> _entries = [];

        public int ErrorsContaining(string text) =>
            _entries.Count(e => e.Level == LogLevel.Error && e.Message.Contains(text, StringComparison.Ordinal));

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            _entries.Add((logLevel, formatter(state, exception)));
    }
}
