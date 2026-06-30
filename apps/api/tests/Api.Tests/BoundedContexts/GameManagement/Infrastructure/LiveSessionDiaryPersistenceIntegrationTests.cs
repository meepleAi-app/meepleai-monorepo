using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// Integration tests for diary-entry persistence in <see cref="LiveSessionRepository"/>.
/// #2570 SP3 T2: Validates that diary entries are round-tripped correctly to and from
/// the <c>live_session_diary_entries</c> table, including:
/// - AddAsync + reload (append-only insert round-trip)
/// - UpdateAsync with new entries (SyncDiaryEntriesAsync append-only semantics)
/// - FK cascade delete from parent session
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveSessionDiaryPersistenceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private LiveSessionRepository _repository = null!;
    private string _databaseName = null!;

    private static readonly Guid AuthorId1 = Guid.NewGuid();
    private static readonly Guid AuthorId2 = Guid.NewGuid();

    // Seeded in InitializeAsync — live_game_sessions.created_by_user_id is an FK to users, so the
    // session owner must exist before AddAsync. (CreateSession previously used a random Guid,
    // which violated FK_live_game_sessions_users_created_by_user_id — a baseline failure.)
    private Guid _ownerUserId;

    public LiveSessionDiaryPersistenceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_diary_repo_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(connectionString);
        await _dbContext.Database.MigrateAsync();

        // Seed the session owner — created_by_user_id is an FK to users (NOT NULL, Restrict).
        _ownerUserId = Guid.NewGuid();
        _dbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = _ownerUserId,
            Email = $"diary-owner-{_ownerUserId:N}@test.local",
            DisplayName = "Diary Owner",
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var mockCollector = new Mock<IDomainEventCollector>();
        mockCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _repository = new LiveSessionRepository(
            _dbContext,
            mockCollector.Object,
            NullLogger<LiveSessionRepository>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private LiveGameSession CreateSession()
    {
        return LiveGameSession.Create(
            id: Guid.NewGuid(),
            createdByUserId: _ownerUserId,
            gameName: "Mage Knight",
            gameId: null);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Round-trip: AddAsync → GetByIdAsync
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddAsync_WithDiaryEntries_RoundTripsAllEntries()
    {
        // Arrange
        var session = CreateSession();
        session.AddDiaryEntry(AuthorId1, "First entry");
        session.AddDiaryEntry(AuthorId2, "Second entry");

        // Act
        await _repository.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdAsync(session.Id);

        // Assert
        reloaded.Should().NotBeNull();
        reloaded!.DiaryEntries.Should().HaveCount(2);
        reloaded.DiaryEntries[0].AuthorId.Should().Be(AuthorId1);
        reloaded.DiaryEntries[0].Text.Should().Be("First entry");
        reloaded.DiaryEntries[1].AuthorId.Should().Be(AuthorId2);
        reloaded.DiaryEntries[1].Text.Should().Be("Second entry");
        // Verify ordering is non-vacuous: assert entries come back in the EXACT insertion order
        // by identity (authorId at index 0 must be AuthorId1, index 1 must be AuthorId2).
        // This is a stronger assertion than BeOnOrBefore when PostgreSQL sub-millisecond
        // timestamp granularity could produce identical CreatedAt values, making a
        // "BeOnOrBefore" assertion hold vacuously even if ORDER BY CreatedAt were removed.
        reloaded.DiaryEntries[0].AuthorId.Should().Be(AuthorId1,
            "first entry was inserted by AuthorId1 — ORDER BY CreatedAt ASC must return it first");
        reloaded.DiaryEntries[1].AuthorId.Should().Be(AuthorId2,
            "second entry was inserted by AuthorId2 — ORDER BY CreatedAt ASC must return it second");
    }

    [Fact]
    public async Task AddAsync_WithNoDiaryEntries_ReturnsEmptyCollection()
    {
        // Arrange
        var session = CreateSession();

        // Act
        await _repository.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdAsync(session.Id);

        // Assert
        reloaded.Should().NotBeNull();
        reloaded!.DiaryEntries.Should().BeEmpty();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // UpdateAsync: SyncDiaryEntriesAsync append-only semantics
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_AppendsDiaryEntry_WhilePreservingExisting()
    {
        // Arrange: persist session with one entry
        var session = CreateSession();
        session.AddDiaryEntry(AuthorId1, "Original entry");
        await _repository.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act: reload and add a second entry, then update
        var reloaded = await _repository.GetByIdAsync(session.Id);
        reloaded.Should().NotBeNull();
        reloaded!.AddDiaryEntry(AuthorId2, "New entry");

        await _repository.UpdateAsync(reloaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert: both entries survive
        var final = await _repository.GetByIdAsync(session.Id);
        final.Should().NotBeNull();
        final!.DiaryEntries.Should().HaveCount(2);
        final.DiaryEntries.Should().Contain(e => e.AuthorId == AuthorId1 && e.Text == "Original entry");
        final.DiaryEntries.Should().Contain(e => e.AuthorId == AuthorId2 && e.Text == "New entry");
    }

    [Fact]
    public async Task UpdateAsync_WithNoNewEntries_DoesNotCorruptExisting()
    {
        // Arrange: persist session with one entry
        var session = CreateSession();
        session.AddDiaryEntry(AuthorId1, "Stable entry");
        await _repository.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act: reload, mutate notes (not diary), update
        var reloaded = await _repository.GetByIdAsync(session.Id);
        reloaded.Should().NotBeNull();
        reloaded!.UpdateNotes("some notes");

        await _repository.UpdateAsync(reloaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert: existing diary entry still intact
        var final = await _repository.GetByIdAsync(session.Id);
        final.Should().NotBeNull();
        final!.DiaryEntries.Should().HaveCount(1);
        final.DiaryEntries[0].Text.Should().Be("Stable entry");
        final.Notes.Should().Be("some notes");
    }

    [Fact]
    public async Task UpdateAsync_CalledTwice_DoesNotReinsertLoadedEntries()
    {
        // #2575: the existing-id set now comes from the UpdateAsync load-time projection (folded
        // into the TotalPausedDurationMs read) instead of a standalone SELECT. This guards that a
        // reload→update cycle never re-inserts an already-persisted entry (no duplicate ids).
        var session = CreateSession();
        session.AddDiaryEntry(AuthorId1, "First entry");
        await _repository.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // First cycle: reload, no new diary entry, update (touch an unrelated field).
        var first = await _repository.GetByIdAsync(session.Id);
        first!.UpdateNotes("touch 1");
        await _repository.UpdateAsync(first);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Second cycle: reload, append a second entry, update.
        var second = await _repository.GetByIdAsync(session.Id);
        second!.AddDiaryEntry(AuthorId2, "Second entry");
        await _repository.UpdateAsync(second);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var final = await _repository.GetByIdAsync(session.Id);
        final.Should().NotBeNull();
        final!.DiaryEntries.Should().HaveCount(2);
        final.DiaryEntries.Select(e => e.Id).Distinct().Should().HaveCount(2,
            "the loaded entry must not be re-inserted — ids stay distinct after a reload→update cycle");
    }

    [Fact]
    public async Task UpdateAsync_AppendsMultipleNewEntries_AllInserted()
    {
        var session = CreateSession();
        session.AddDiaryEntry(AuthorId1, "Original");
        await _repository.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdAsync(session.Id);
        reloaded!.AddDiaryEntry(AuthorId1, "New A");
        reloaded.AddDiaryEntry(AuthorId2, "New B");
        await _repository.UpdateAsync(reloaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var final = await _repository.GetByIdAsync(session.Id);
        final.Should().NotBeNull();
        final!.DiaryEntries.Should().HaveCount(3);
        final.DiaryEntries.Select(e => e.Id).Distinct().Should().HaveCount(3,
            "two distinct new entries are inserted alongside the original in a single UpdateAsync");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Schema constraint: cascade delete from parent session
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteParentSession_CascadesToDiaryEntries()
    {
        // Arrange
        var session = CreateSession();
        session.AddDiaryEntry(AuthorId1, "Entry A");
        session.AddDiaryEntry(AuthorId2, "Entry B");
        await _repository.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Verify entries exist
        var entryCount = await _dbContext.Set<Api.Infrastructure.Entities.GameManagement.LiveSessionDiaryEntryEntity>()
            .CountAsync(e => e.LiveGameSessionId == session.Id);
        entryCount.Should().Be(2);

        // Act: delete the parent session directly via EF
        var parent = await _dbContext.LiveGameSessions.SingleAsync(s => s.Id == session.Id);
        _dbContext.LiveGameSessions.Remove(parent);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Assert: cascade removes diary entries
        var remainingCount = await _dbContext.Set<Api.Infrastructure.Entities.GameManagement.LiveSessionDiaryEntryEntity>()
            .CountAsync(e => e.LiveGameSessionId == session.Id);
        remainingCount.Should().Be(0, "cascade delete must purge diary entries when the parent session is removed");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Round-trip: field fidelity (Id, AuthorId, CreatedAt, Text)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task DiaryEntry_FieldsRoundTrip_WithFullFidelity()
    {
        // Arrange
        var session = CreateSession();
        session.AddDiaryEntry(AuthorId1, "  Trimmed entry text  "); // Text should be trimmed by domain
        await _repository.AddAsync(session);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var reloaded = await _repository.GetByIdAsync(session.Id);

        // Assert
        reloaded.Should().NotBeNull();
        reloaded!.DiaryEntries.Should().HaveCount(1);

        var entry = reloaded.DiaryEntries[0];
        entry.Id.Should().NotBe(Guid.Empty);
        entry.AuthorId.Should().Be(AuthorId1);
        entry.Text.Should().Be("Trimmed entry text", "domain trims whitespace in AddDiaryEntry");
        entry.CreatedAt.Should().NotBe(default);
        entry.CreatedAt.Offset.Should().Be(TimeSpan.Zero, "CreatedAt is stored as UTC DateTimeOffset");
    }
}
