using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;

/// <summary>
/// #4848 — the snapshot that a pause takes is keyed by the event that asked for it (<c>source_event_id</c>, a
/// UNIQUE partial index, CF-2 / #1938). Nothing used to fire that index in practice, because <c>EventId</c> is a new
/// Guid every time the outbox rebuilds the event; once it survives (#4841) it fires on every second delivery.
///
/// The command was not idempotent: on a replay it worked out the next index, inserted, and met the index. The insert
/// stayed <c>Added</c> on the context that the outbox processor shares with its handlers, and the processor saves
/// once more at the end of the batch, outside its per-row try/catch: the same insert again, the same 23505, the
/// whole batch rolled back and the same row first in the queue at every poll.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4848")]
public sealed class SessionSnapshotIdempotencyTests
{
    private readonly Mock<ISessionSnapshotRepository> _snapshotRepo = new();
    private readonly Mock<ILiveSessionRepository> _sessionRepo = new();
    private readonly Mock<ISessionAttachmentRepository> _attachmentRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    public SessionSnapshotIdempotencyTests()
    {
        _attachmentRepo.Setup(r => r.GetBySnapshotAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionAttachment>());
    }

    private static LiveGameSession LiveSession(Guid sessionId)
    {
        var session = LiveGameSession.Create(sessionId, Guid.NewGuid(), "Test Game");
        session.UpdateGameState(JsonDocument.Parse("{\"turn\":1}"));
        return session;
    }

    private static Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot SnapshotOf(
        Guid sessionId, int index, Guid? sourceEventId) =>
        new(Guid.NewGuid(), sessionId, index, SnapshotTrigger.SessionPaused, "Auto", "{\"turn\":1}", true, 1, null, null,
            sourceEventId: sourceEventId);

    private CreateSnapshotCommandHandler Handler(
        ISessionSnapshotRepository? snapshots = null, IUnitOfWork? uow = null) =>
        new(snapshots ?? _snapshotRepo.Object, _sessionRepo.Object, _attachmentRepo.Object, uow ?? _uow.Object);

    private static Exception LostTheRace() =>
        new DbUpdateException(
            "duplicate key value violates unique constraint",
            new PostgresException("duplicate key value", "ERROR", "ERROR", "23505"));

    // ── The command: idempotent by the event that asked for it ──────────────────────────────────

    [Fact]
    public async Task CreateSnapshot_ForAnEventThatAlreadyHasItsSnapshot_ReturnsIt_AndWritesNothing()
    {
        var sessionId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();
        var existing = SnapshotOf(sessionId, 3, sourceEventId);
        _snapshotRepo.Setup(r => r.GetBySourceEventIdAsync(sourceEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(LiveSession(sessionId));
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.SessionPaused, "Auto", null, SourceEventId: sourceEventId);

        var result = await Handler().Handle(command, CancellationToken.None);

        result.Id.Should().Be(existing.Id);
        result.SnapshotIndex.Should().Be(3);
        _snapshotRepo.Verify(r => r.AddAsync(It.IsAny<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateSnapshot_ForAnEventWithNoSnapshotYet_CreatesIt()
    {
        // The other direction: the lookup must not turn the command off for every event.
        var sessionId = Guid.NewGuid();
        var sourceEventId = Guid.NewGuid();
        _snapshotRepo.Setup(r => r.GetBySourceEventIdAsync(sourceEventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot?)null);
        _sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(LiveSession(sessionId));
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.SessionPaused, "Auto", null, SourceEventId: sourceEventId);

        await Handler().Handle(command, CancellationToken.None);

        _snapshotRepo.Verify(r => r.AddAsync(
            It.Is<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>(s => s.SourceEventId == sourceEventId),
            It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSnapshot_WithNoSourceEvent_DoesNotLookForOne()
    {
        // A manual snapshot has no event behind it: there is nothing to be idempotent about.
        var sessionId = Guid.NewGuid();
        _sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(LiveSession(sessionId));
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.ManualSave, "Save", null);

        await Handler().Handle(command, CancellationToken.None);

        _snapshotRepo.Verify(r => r.GetBySourceEventIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _snapshotRepo.Verify(r => r.AddAsync(It.IsAny<Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Exception AnyOtherFailure() => new TimeoutException("the database did not answer");

    [Theory]
    [InlineData(true)]   // two deliveries at the same instant: the second is refused by the unique index (23505)
    [InlineData(false)]  // any other failure of the save: a timeout, data the column refuses
    public async Task CreateSnapshot_WhenTheSaveFails_ForgetsTheInsert_AndLetsTheExceptionThrough(bool lostTheRace)
    {
        // The exception goes on (the notification handler reads a duplicate as «already recorded» and swallows the
        // rest), but not before the insert is forgotten: whatever the reason the save failed, the insert stays Added on
        // the shared context, and the final save of the outbox processor would send it again.
        var sessionId = Guid.NewGuid();
        _sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(LiveSession(sessionId));
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(lostTheRace ? LostTheRace() : AnyOtherFailure());
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.SessionPaused, "Auto", null, SourceEventId: Guid.NewGuid());

        var act = () => Handler().Handle(command, CancellationToken.None);

        if (lostTheRace)
        {
            await act.Should().ThrowAsync<DbUpdateException>();
        }
        else
        {
            await act.Should().ThrowAsync<TimeoutException>();
        }

        _snapshotRepo.Verify(r => r.DiscardUnsavedAdditions(), Times.Once);
    }

    // ── The repository ──────────────────────────────────────────────────────────────────────────

    private static SessionSnapshotEntity EntityOf(Guid sessionId, int index, Guid? sourceEventId) => new()
    {
        SessionId = sessionId,
        SnapshotIndex = index,
        TriggerType = (int)SnapshotTrigger.SessionPaused,
        DeltaDataJson = "{}",
        IsCheckpoint = true,
        SourceEventId = sourceEventId,
    };

    [Fact]
    public async Task Repository_GetBySourceEventId_FindsTheSnapshotOfThatEventOnly()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sessionId = Guid.NewGuid();
        var mine = Guid.NewGuid();
        db.SessionSnapshots.Add(EntityOf(sessionId, 0, mine));
        db.SessionSnapshots.Add(EntityOf(sessionId, 1, Guid.NewGuid()));
        await db.SaveChangesAsync();
        var repository = new SessionSnapshotRepository(db, new DomainEventCollector());

        var found = await repository.GetBySourceEventIdAsync(mine);
        var missing = await repository.GetBySourceEventIdAsync(Guid.NewGuid());

        found.Should().NotBeNull();
        found!.SnapshotIndex.Should().Be(0);
        missing.Should().BeNull();
    }

    [Fact]
    public async Task Repository_DiscardUnsavedAdditions_ForgetsTheStagedSnapshots_AndOnlyThose()
    {
        var db = TestDbContextFactory.CreateInMemoryDbContext();
        var sessionId = Guid.NewGuid();
        var saved = EntityOf(sessionId, 0, Guid.NewGuid());
        db.SessionSnapshots.Add(saved);
        await db.SaveChangesAsync();
        var repository = new SessionSnapshotRepository(db, new DomainEventCollector());
        await repository.AddAsync(SnapshotOf(sessionId, 1, Guid.NewGuid()));
        var otherRow = new PlayRecordEntity { GameName = "x", CreatedByUserId = Guid.NewGuid(), ScoringConfigJson = "{}" };
        db.PlayRecords.Add(otherRow);
        var staged = db.ChangeTracker.Entries<SessionSnapshotEntity>().Single(e => e.State == EntityState.Added).Entity;

        repository.DiscardUnsavedAdditions();

        db.Entry(staged).State.Should().Be(EntityState.Detached);
        db.Entry(saved).State.Should().Be(EntityState.Unchanged, "what was saved is not the repository's to forget");
        db.Entry(otherRow).State.Should().Be(EntityState.Added, "another aggregate's staged row is not a snapshot");
    }

    // ── Everything real: a save the database refuses, for any reason, leaves nothing behind ─────

    [Theory]
    [InlineData(true)]   // refused by the unique index on the source event (23505)
    [InlineData(false)]  // refused for another reason: a timeout
    public async Task CreateSnapshot_WhenTheDatabaseRefusesTheInsert_LeavesNoSnapshotStagedOnTheSharedContext(bool lostTheRace)
    {
        var sessionId = Guid.NewGuid();
        var collector = new DomainEventCollector();
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w =>
            {
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning);
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning);
            })
            .Options;
        var db = new RefusesEverySaveDbContext(options, collector, lostTheRace ? LostTheRace() : AnyOtherFailure());
        _sessionRepo.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(LiveSession(sessionId));
        var handler = Handler(new SessionSnapshotRepository(db, collector), new UnitOfWork(db));
        var command = new CreateSnapshotCommand(sessionId, SnapshotTrigger.SessionPaused, "Auto", null, SourceEventId: Guid.NewGuid());

        var act = () => handler.Handle(command, CancellationToken.None);

        if (lostTheRace)
        {
            await act.Should().ThrowAsync<DbUpdateException>();
        }
        else
        {
            await act.Should().ThrowAsync<TimeoutException>();
        }

        db.AddedWhenTheSaveWasRefused.Should().BeGreaterThan(0, "the snapshot was staged: otherwise this test proves nothing");
        db.ChangeTracker.Entries<SessionSnapshotEntity>().Where(e => e.State == EntityState.Added)
            .Should().BeEmpty("the final save of the outbox processor would send the same insert again");
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
