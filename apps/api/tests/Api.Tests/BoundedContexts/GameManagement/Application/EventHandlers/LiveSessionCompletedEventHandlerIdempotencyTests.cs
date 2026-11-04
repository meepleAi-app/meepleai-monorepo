using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using System.Diagnostics.Metrics;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventLog;
using Api.Infrastructure.Entities.GameManagement;
using Api.Observability;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.EventHandlers;

/// <summary>
/// #4753 — what <see cref="LiveSessionCompletedEventHandler"/> does when the play record of the session
/// already exists, and what it leaves behind on the SHARED context when its insert does not go through.
///
/// The record is keyed by the session, so the unique index on <c>play_records.source_event_id</c> now fires on
/// every second delivery (before, the key was a fresh Guid each time and it never did). The outbox processor
/// hands the handler its own scoped <see cref="MeepleAiDbContext"/> and event collector, and saves once more at
/// the end of the batch. Two things follow:
///   1. The common case is answered before any write, with a read.
///   2. The rare one (two deliveries at the same instant) loses at the database, and nothing the handler had
///      staged may stay: neither the rows (the final save would send the insert again, fail outside the
///      per-row try/catch and leave the whole batch pending) nor the events the repository queued for the
///      record (a save that works would write them, and an outbox row each, for a record that was never made).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4753")]
public sealed class LiveSessionCompletedEventHandlerIdempotencyTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 2, 19, 14, 0, 0, TimeSpan.Zero));
    private readonly Mock<IPlayRecordRepository> _playRecordRepository = new();

    private LiveSessionCompletedEvent CompletedEvent(Guid sessionId, TimeSpan? startedAgo = null)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var player = new CompletedPlayerSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Alice", 10, 1);
        return new LiveSessionCompletedEvent(
            sessionId, now, 5, Guid.NewGuid(), "Catan", Guid.NewGuid(), PlayRecordVisibility.Private, null,
            now.AddHours(-1), now - (startedAgo ?? TimeSpan.FromMinutes(30)), null,
            [player], [new CompletedScoreSnapshot(player.PlayerId, "points", 10, "pts")]);
    }

    private PlayRecordEntity RecordOf(Guid sourceEventId, bool deleted = false)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        return new PlayRecordEntity
        {
            GameName = "Catan",
            CreatedByUserId = Guid.NewGuid(),
            ScoringConfigJson = "{}",
            SessionDate = now,
            CreatedAt = now,
            UpdatedAt = now,
            SourceEventId = sourceEventId,
            IsDeleted = deleted,
            DeletedAt = deleted ? now : null,
        };
    }

    private LiveSessionCompletedEventHandler CreateHandler(
        MeepleAiDbContext db,
        IPlayRecordRepository repository,
        IDomainEventCollector collector,
        ILogger<LiveSessionCompletedEventHandler>? logger = null) =>
        new(db, logger ?? new Mock<ILogger<LiveSessionCompletedEventHandler>>().Object, _timeProvider, repository, collector);

    // ── The common case: the session already has its record ─────────────────────────────────────

    [Fact]
    public async Task Handle_WhenTheSessionAlreadyHasItsRecord_WritesNothing()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sessionId = Guid.NewGuid();
        db.PlayRecords.Add(RecordOf(sessionId));
        await db.SaveChangesAsync();

        await CreateHandler(db, _playRecordRepository.Object, new DomainEventCollector())
            .Handle(CompletedEvent(sessionId), CancellationToken.None);

        _playRecordRepository.Verify(
            r => r.AddAsync(It.IsAny<PlayRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTheRecordOfTheSessionWasDeleted_DoesNotMakeItAgain()
    {
        // The unique index counts the soft-deleted rows, and a replayed event must not bring back a record the
        // user deleted: the read has to look past the query filter that hides them.
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sessionId = Guid.NewGuid();
        db.PlayRecords.Add(RecordOf(sessionId, deleted: true));
        await db.SaveChangesAsync();

        await CreateHandler(db, _playRecordRepository.Object, new DomainEventCollector())
            .Handle(CompletedEvent(sessionId), CancellationToken.None);

        _playRecordRepository.Verify(
            r => r.AddAsync(It.IsAny<PlayRecord>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenOnlyAnotherSessionHasARecord_StillWritesThisOne()
    {
        // The other direction: the read must not turn the handler off for every session once any record exists.
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        db.PlayRecords.Add(RecordOf(Guid.NewGuid()));
        await db.SaveChangesAsync();

        await CreateHandler(db, _playRecordRepository.Object, new DomainEventCollector())
            .Handle(CompletedEvent(Guid.NewGuid()), CancellationToken.None);

        _playRecordRepository.Verify(
            r => r.AddAsync(It.IsAny<PlayRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── The rare case: the insert does not go through ───────────────────────────────────────────

    private static DbContextOptions<MeepleAiDbContext> InMemoryOptions() =>
        new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w =>
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;

    private static Exception LostTheRace() =>
        new DbUpdateException(
            "duplicate key value violates unique constraint",
            new PostgresException("duplicate key value", "ERROR", "ERROR", "23505"));

    private static Exception AnyOtherFailure() => new InvalidOperationException("the database went away");

    /// <summary>
    /// Runs the handler, with the REAL repository and the REAL collector (as the outbox processor has them), on a
    /// context whose save is refused with <paramref name="failure"/>. The caller had staged one row and queued
    /// one event before delivering; the handler adds the whole graph of the record and its events.
    /// </summary>
    private async Task<(RefusesEverySaveDbContext Db, DomainEventCollector Collector, PlayRecordEntity StagedByTheCaller, LiveSessionCompletedEvent QueuedByTheCaller)>
        DeliverWhenTheSaveIsRefusedAsync(Exception failure)
    {
        var collector = new DomainEventCollector();
        var db = new RefusesEverySaveDbContext(InMemoryOptions(), collector, failure);
        var stagedByTheCaller = RecordOf(Guid.NewGuid());
        db.PlayRecords.Add(stagedByTheCaller);
        var queuedByTheCaller = CompletedEvent(Guid.NewGuid());
        collector.Collect(queuedByTheCaller);
        var handler = CreateHandler(db, new PlayRecordRepository(db, collector), collector);

        var act = () => handler.Handle(CompletedEvent(Guid.NewGuid()), CancellationToken.None);

        await act.Should().NotThrowAsync("losing the race is a duplicate to skip, and any other failure must not block the completion");
        return (db, collector, stagedByTheCaller, queuedByTheCaller);
    }

    [Theory]
    [InlineData(true)]   // lost the race at the unique index
    [InlineData(false)]  // any other failure of the save, swallowed by the generic catch
    public async Task Handle_WhenTheInsertDoesNotGoThrough_LeavesTheSharedContextAsTheCallerHadIt(bool lostTheRace)
    {
        var (db, collector, stagedByTheCaller, queuedByTheCaller) =
            await DeliverWhenTheSaveIsRefusedAsync(lostTheRace ? LostTheRace() : AnyOtherFailure());

        db.AddedWhenTheSaveWasRefused.Should().BeGreaterThan(2, "the record, its player and the caller's own row were all staged: otherwise this test proves nothing");
        db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Select(e => e.Entity)
            .Should().ContainSingle().Which.Should().BeSameAs(stagedByTheCaller, "only what the caller had staged stays");
        collector.PeekEvents().Should().ContainSingle().Which.Should().BeSameAs(queuedByTheCaller, "the events of the record that was not written must not stay queued for the next save");
    }

    // ── #4839 — a failure that is swallowed is still SEEN ───────────────────────────────────────
    //
    // The handler swallows what it cannot do, on purpose: MediatR runs the handlers of an event in sequence
    // (ForeachAwaitPublisher, no custom NotificationPublisher), so an exception here would stop the ones after it
    // and, on every retry of the outbox row, run again the ones before it (the player statistics among them, whose
    // guard on EventId does not hold while EventId does not survive the outbox). Swallowing it in silence lost the
    // record without a trace in the counters.

    [Fact]
    public async Task Handle_WhenTheInsertFailsForAnyOtherReason_IsCountedAndNamesTheSession()
    {
        using var failures = new DispatchFailureCapture();
        var logger = new CapturingLogger<LiveSessionCompletedEventHandler>();
        var collector = new DomainEventCollector();
        var db = new RefusesEverySaveDbContext(InMemoryOptions(), collector, AnyOtherFailure());
        var handler = CreateHandler(db, new PlayRecordRepository(db, collector), collector, logger);
        var sessionId = Guid.NewGuid();
        var @event = CompletedEvent(sessionId);

        var act = () => handler.Handle(@event, CancellationToken.None);

        await act.Should().NotThrowAsync("the other handlers of the event must still run");
        failures.Events.Should().ContainSingle()
            .Which.Should().Be((EventTypeRegistry.ResolveOrFullName(@event), nameof(LiveSessionCompletedEventHandler)));
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Error && e.Message.Contains(sessionId.ToString()));
    }

    [Fact]
    public async Task Handle_WhenItLosesTheRace_IsNotCountedAsAFailure()
    {
        // The other direction: a duplicate is a success (the record exists), not a dispatch failure.
        using var failures = new DispatchFailureCapture();
        var collector = new DomainEventCollector();
        var db = new RefusesEverySaveDbContext(InMemoryOptions(), collector, LostTheRace());
        var handler = CreateHandler(db, new PlayRecordRepository(db, collector), collector);

        await handler.Handle(CompletedEvent(Guid.NewGuid()), CancellationToken.None);

        failures.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WhenTheCallIsCancelled_PutsTheContextBack_AndPropagatesWithoutCountingAFailure()
    {
        // A cancelled call (the outbox processor shutting down, a request aborted in Hybrid) is not a failure to
        // swallow, nor one to count: the cancellation goes on, after the context is put back as the caller left it.
        using var failures = new DispatchFailureCapture();
        var collector = new DomainEventCollector();
        var db = new RefusesEverySaveDbContext(InMemoryOptions(), collector, new OperationCanceledException());
        var handler = CreateHandler(db, new PlayRecordRepository(db, collector), collector);

        var act = () => handler.Handle(CompletedEvent(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<OperationCanceledException>("a cancelled call is not swallowed");
        failures.Events.Should().BeEmpty("a cancellation is not a dispatch failure");
        db.AddedWhenTheSaveWasRefused.Should().BeGreaterThan(1, "the record and its player were staged: otherwise this test proves nothing");
        db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Should().BeEmpty();
        collector.PeekEvents().Should().BeEmpty();
    }

    // ── #4839, option B — the infrastructure's failures go up, the data's stay swallowed ────────
    //
    // A timeout or a lost connection is not the record's fault: swallowing it loses the play record for good, while
    // the outbox would retry the row once the database is back. A refused value or an invalid record fails the same
    // way at every retry, so those stay swallowed (and counted) and the other handlers of the event still run.

    private static Exception InfrastructureFailure(string kind) => kind switch
    {
        "timeout" => new TimeoutException("the command timed out"),
        "retry-exhausted" => new RetryLimitExceededException("the execution strategy gave up", new TimeoutException()),
        "connection-lost" => new NpgsqlException("connection lost", new IOException("broken pipe")),
        "wrapped-by-the-save" => new DbUpdateException("save failed", new NpgsqlException("connection lost", new IOException("broken pipe"))),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    [Theory]
    [InlineData("timeout")]
    [InlineData("retry-exhausted")]
    [InlineData("connection-lost")]
    [InlineData("wrapped-by-the-save")]
    public async Task Handle_WhenTheInfrastructureFails_ThrowsSoTheOutboxRetries_AndLeavesTheContextAsTheCallerHadIt(string kind)
    {
        using var failures = new DispatchFailureCapture();
        var logger = new CapturingLogger<LiveSessionCompletedEventHandler>();
        var collector = new DomainEventCollector();
        var failure = InfrastructureFailure(kind);
        var db = new RefusesEverySaveDbContext(InMemoryOptions(), collector, failure);
        var stagedByTheCaller = RecordOf(Guid.NewGuid());
        db.PlayRecords.Add(stagedByTheCaller);
        var queuedByTheCaller = CompletedEvent(Guid.NewGuid());
        collector.Collect(queuedByTheCaller);
        var handler = CreateHandler(db, new PlayRecordRepository(db, collector), collector, logger);
        var sessionId = Guid.NewGuid();

        var act = () => handler.Handle(CompletedEvent(sessionId), CancellationToken.None);

        (await act.Should().ThrowAsync<Exception>("the outbox retries what the infrastructure refused"))
            .Which.Should().BeSameAs(failure);
        failures.Events.Should().BeEmpty("the dispatcher that catches it counts it: counting here too would count it twice");
        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning && e.Message.Contains(sessionId.ToString()));
        db.AddedWhenTheSaveWasRefused.Should().BeGreaterThan(2, "the record, its player and the caller's own row were all staged: otherwise this test proves nothing");
        db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Select(e => e.Entity)
            .Should().ContainSingle().Which.Should().BeSameAs(stagedByTheCaller, "the failed insert must not stay staged for the final save of the outbox processor");
        collector.PeekEvents().Should().ContainSingle().Which.Should().BeSameAs(queuedByTheCaller);
    }

    [Fact]
    public async Task Handle_WhenTheDatabaseRefusesTheData_StillSwallowsAndCounts()
    {
        // 23503: the game of the session is gone. It would fail the same way at every retry.
        using var failures = new DispatchFailureCapture();
        var collector = new DomainEventCollector();
        var refusal = new DbUpdateException(
            "insert or update violates foreign key constraint",
            new PostgresException("foreign key violation", "ERROR", "ERROR", "23503"));
        var db = new RefusesEverySaveDbContext(InMemoryOptions(), collector, refusal);
        var handler = CreateHandler(db, new PlayRecordRepository(db, collector), collector);
        var @event = CompletedEvent(Guid.NewGuid());

        var act = () => handler.Handle(@event, CancellationToken.None);

        await act.Should().NotThrowAsync("a refusal of the data is not the infrastructure's to retry");
        failures.Events.Should().ContainSingle()
            .Which.Should().Be((EventTypeRegistry.ResolveOrFullName(@event), nameof(LiveSessionCompletedEventHandler)));
    }

    [Fact]
    public async Task Handle_WhenTheRecordIsInvalid_StillSwallowsAndCounts()
    {
        // A session left open for more than 30 days and then completed: PlayRecord.Complete refuses the duration.
        using var failures = new DispatchFailureCapture();
        var collector = new DomainEventCollector();
        var db = new RefusesEverySaveDbContext(InMemoryOptions(), collector, new InvalidOperationException("never reached"));
        var handler = CreateHandler(db, new PlayRecordRepository(db, collector), collector);
        var @event = CompletedEvent(Guid.NewGuid(), startedAgo: TimeSpan.FromDays(31));

        var act = () => handler.Handle(@event, CancellationToken.None);

        await act.Should().NotThrowAsync("the duration would be refused at every retry");
        db.AddedWhenTheSaveWasRefused.Should().Be(0, "the record is refused before any save: the double is only there to fail loudly if it was reached");
        failures.Events.Should().ContainSingle();
    }

    /// <summary>Collects the measurements of the dispatch-failure counter that carry this handler's name.</summary>
    private sealed class DispatchFailureCapture : IDisposable
    {
        private const string CounterName = "meepleai.domain_event_log.dispatch_failures.total";
        private readonly MeterListener _listener;
        private readonly object _gate = new();
        private readonly List<(string EventType, string HandlerName)> _events = new();

        // The counter is process-wide and other tests may count their own handlers: only this handler's are kept.
        public IReadOnlyList<(string EventType, string HandlerName)> Events
        {
            get { lock (_gate) { return _events.ToList(); } }
        }

        public DispatchFailureCapture()
        {
            _listener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == MeepleAiMetrics.MeterName && instrument.Name == CounterName)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                string eventType = "<missing>";
                string handlerName = "<missing>";
                foreach (var tag in tags)
                {
                    if (tag.Key == "event_type") eventType = tag.Value?.ToString() ?? "";
                    else if (tag.Key == "handler_name") handlerName = tag.Value?.ToString() ?? "";
                }

                if (handlerName != nameof(LiveSessionCompletedEventHandler)) return;
                lock (_gate) { _events.Add((eventType, handlerName)); }
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    /// <summary>A context whose every save is refused, as the second of two deliveries' is by the unique index.</summary>
    private sealed class RefusesEverySaveDbContext(
        DbContextOptions<MeepleAiDbContext> options,
        IDomainEventCollector collector,
        Exception failure)
        : MeepleAiDbContext(options, new Mock<IMediator>().Object, collector)
    {
        public int AddedWhenTheSaveWasRefused { get; private set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            AddedWhenTheSaveWasRefused = ChangeTracker.Entries().Count(e => e.State == EntityState.Added);
            throw failure;
        }
    }
}
