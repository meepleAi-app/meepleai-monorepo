using Api.BoundedContexts.GameManagement.Application.EventHandlers;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
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
/// already exists, and what it leaves behind in the change tracker when it loses the race for it.
///
/// The record is keyed by the session, so the unique index on <c>play_records.source_event_id</c> now fires on
/// every second delivery (before, the key was a fresh Guid each time and it never did). Two things follow, and
/// both live on a context that is SHARED: the outbox processor hands the handler its own scoped
/// <see cref="MeepleAiDbContext"/> and saves it once more at the end of the batch.
///   1. The common case is answered before any write, with a read.
///   2. The rare one (two deliveries at the same instant) loses at the database, and the rows the handler had
///      staged must not stay <c>Added</c>: the processor's final <c>SaveChangesAsync</c> would try the same
///      insert again, fail outside its per-row try/catch and leave the whole batch pending.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4753")]
public sealed class LiveSessionCompletedEventHandlerIdempotencyTests
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 2, 19, 14, 0, 0, TimeSpan.Zero));
    private readonly Mock<IPlayRecordRepository> _playRecordRepository = new();

    private LiveSessionCompletedEventHandler CreateHandler(MeepleAiDbContext dbContext) =>
        new(dbContext, new Mock<ILogger<LiveSessionCompletedEventHandler>>().Object, _timeProvider, _playRecordRepository.Object);

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

    [Fact]
    public async Task Handle_WhenTheSessionAlreadyHasItsRecord_WritesNothing()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sessionId = Guid.NewGuid();
        db.PlayRecords.Add(RecordOf(sessionId));
        await db.SaveChangesAsync();

        await CreateHandler(db).Handle(CompletedEvent(sessionId), CancellationToken.None);

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

        await CreateHandler(db).Handle(CompletedEvent(sessionId), CancellationToken.None);

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

        await CreateHandler(db).Handle(CompletedEvent(Guid.NewGuid()), CancellationToken.None);

        _playRecordRepository.Verify(
            r => r.AddAsync(It.IsAny<PlayRecord>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenItLosesTheRaceAtTheDatabase_ForgetsWhatItStaged_AndOnlyThat()
    {
        // Two deliveries of the same completion at the same instant both pass the read, and the second insert
        // is refused by the unique index (23505). What the handler had staged on the shared context has to go
        // with it (a later SaveChangesAsync would send it again), but not what the caller had staged.
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w =>
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;
        var db = new LosesTheInsertRaceDbContext(options);
        var stagedByTheCaller = RecordOf(Guid.NewGuid());
        db.PlayRecords.Add(stagedByTheCaller);
        var stagedByTheHandler = new List<PlayRecordEntity>();
        _playRecordRepository
            .Setup(r => r.AddAsync(It.IsAny<PlayRecord>(), It.IsAny<CancellationToken>()))
            .Callback<PlayRecord, CancellationToken>((record, _) =>
            {
                // A record and one more row of its graph (players and scores are rows of their own).
                stagedByTheHandler.Add(RecordOf(record.SourceEventId!.Value));
                stagedByTheHandler.Add(RecordOf(record.SourceEventId!.Value));
                db.PlayRecords.AddRange(stagedByTheHandler);
            })
            .Returns(Task.CompletedTask);

        var act = () => CreateHandler(db).Handle(CompletedEvent(Guid.NewGuid()), CancellationToken.None);

        await act.Should().NotThrowAsync("losing the race is a duplicate to skip, not a failure");
        stagedByTheHandler.Should().HaveCount(2);
        stagedByTheHandler.Should().OnlyContain(e => db.Entry(e).State == EntityState.Detached);
        db.Entry(stagedByTheCaller).State.Should().Be(EntityState.Added, "what the caller staged is not the handler's to forget");
    }

    /// <summary>A context whose every save is refused by the unique index, as the second of two deliveries is.</summary>
    private sealed class LosesTheInsertRaceDbContext(DbContextOptions<MeepleAiDbContext> options)
        : MeepleAiDbContext(
            options,
            new Mock<IMediator>().Object,
            TestDbContextFactory.CreateMockEventCollector().Object)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException(
                "duplicate key value violates unique constraint",
                new PostgresException("duplicate key value", "ERROR", "ERROR", "23505"));
    }
}
