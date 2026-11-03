using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
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

    private LiveSessionCompletedEvent CompletedEvent(Guid sessionId)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var player = new CompletedPlayerSnapshot(Guid.NewGuid(), Guid.NewGuid(), "Alice", 10, 1);
        return new LiveSessionCompletedEvent(
            sessionId, now, 5, Guid.NewGuid(), "Catan", Guid.NewGuid(), PlayRecordVisibility.Private, null,
            now.AddHours(-1), now.AddMinutes(-30), null,
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
        MeepleAiDbContext db, IPlayRecordRepository repository, IDomainEventCollector collector) =>
        new(db, new Mock<ILogger<LiveSessionCompletedEventHandler>>().Object, _timeProvider, repository, collector);

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
        DeliverWhenTheSaveIsRefusedAsync(Exception failure, bool failureIsForTheOutboxToRetry)
    {
        var collector = new DomainEventCollector();
        var db = new RefusesEverySaveDbContext(InMemoryOptions(), collector, failure);
        var stagedByTheCaller = RecordOf(Guid.NewGuid());
        db.PlayRecords.Add(stagedByTheCaller);
        var queuedByTheCaller = CompletedEvent(Guid.NewGuid());
        collector.Collect(queuedByTheCaller);
        var handler = CreateHandler(db, new PlayRecordRepository(db, collector), collector);

        var act = () => handler.Handle(CompletedEvent(Guid.NewGuid()), CancellationToken.None);

        if (failureIsForTheOutboxToRetry)
        {
            // #4839: both delivery paths take an exception from a handler (the in-process one logs it and counts
            // it in DomainEventDispatchFailures, the outbox retries the row and ends in Failed), and the retry is
            // idempotent now that the record is keyed by the session. Swallowing it lost the record in silence.
            await act.Should().ThrowAsync<InvalidOperationException>("any failure other than the duplicate is for the outbox to retry");
        }
        else
        {
            await act.Should().NotThrowAsync("losing the race is a duplicate to skip, not a failure");
        }

        return (db, collector, stagedByTheCaller, queuedByTheCaller);
    }

    [Theory]
    [InlineData(true)]   // lost the race at the unique index
    [InlineData(false)]  // any other failure of the save: cleaned up, then rethrown for the outbox to retry
    public async Task Handle_WhenTheInsertDoesNotGoThrough_LeavesTheSharedContextAsTheCallerHadIt(bool lostTheRace)
    {
        var (db, collector, stagedByTheCaller, queuedByTheCaller) =
            await DeliverWhenTheSaveIsRefusedAsync(
                lostTheRace ? LostTheRace() : AnyOtherFailure(),
                failureIsForTheOutboxToRetry: !lostTheRace);

        db.AddedWhenTheSaveWasRefused.Should().BeGreaterThan(2, "the record, its player and the caller's own row were all staged: otherwise this test proves nothing");
        db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Select(e => e.Entity)
            .Should().ContainSingle().Which.Should().BeSameAs(stagedByTheCaller, "only what the caller had staged stays");
        collector.PeekEvents().Should().ContainSingle().Which.Should().BeSameAs(queuedByTheCaller, "the events of the record that was not written must not stay queued for the next save");
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
