using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests proving that <see cref="GameNightEventRepository.UpdateAsync"/> reconciles the
/// aggregate's child collections (<c>game_night_rsvps</c>, <c>game_night_sessions</c>) instead of
/// re-mapping the whole graph and calling a detached <c>DbSet.Update()</c>.
///
/// <para><b>The bug (P2 — GameNight UpdateAsync child-loss):</b> the previous <c>UpdateAsync</c> did
/// <c>MapToPersistence(gameNight)</c> (a brand-new graph where every child already carries a
/// <c>Guid.NewGuid()</c> Id) followed by <c>DbContext.GameNightEvents.Update(entity)</c>. On a
/// disconnected graph EF marks every child with a set key as <c>Modified</c>, so a NEW child
/// (invitee / session added since the load) becomes an <c>UPDATE</c> against a row that does not
/// exist → 0 rows affected → <see cref="DbUpdateConcurrencyException"/> and a full rollback. The
/// in-memory provider used by the unit tests upserts silently and never reproduces this — only a
/// real relational provider (Postgres) does.</para>
///
/// <para>Two production symptoms this reproduces:
/// <list type="bullet">
///   <item><c>InviteToGameNightCommandHandler</c> — the added invitees are silently rolled back.</item>
///   <item><c>StartGameNightSessionCommandHandler</c> — the aggregate save ALWAYS throws
///         <see cref="DbUpdateConcurrencyException"/>, which the handler mis-maps to
///         <c>MaxLiveSessionsExceededException</c> (a false 409 "max live sessions") so the session
///         is never linked to the night.</item>
/// </list></para>
///
/// <para>These tests fail on origin/main-dev (child rows lost / concurrency exception / EF identity
/// conflict on the tracked-root path) and pass once <c>UpdateAsync</c> reconciles child state
/// (Added / Modified / Deleted) the way <see cref="LiveSessionRepository"/> already does.</para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "gamenight-update-child-loss")]
public sealed class GameNightEventChildReconciliationIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;

    public GameNightEventChildReconciliationIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"gamenight_childrecon_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private GameNightEventRepository CreateRepository(MeepleAiDbContext dbContext) =>
        new(dbContext, Mock.Of<IDomainEventCollector>(), Mock.Of<ILogger<GameNightEventRepository>>());

    /// <summary>Seeds a persisted Published night with no children and returns its id.</summary>
    private async Task<Guid> SeedPublishedEventAsync()
    {
        var eventId = Guid.NewGuid();
        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = Guid.NewGuid(),
            Title = "Boardgame Night",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(3),
            GameIdsJson = "[]",
            Status = "Published",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return eventId;
    }

    /// <summary>
    /// Seeds a persisted Published night that already has one Pending session linked to
    /// <paramref name="sessionId"/> (so <c>FindByLinkedSessionIdAsync</c> can load it TRACKED).
    /// </summary>
    private async Task<Guid> SeedPublishedEventWithSessionAsync(Guid sessionId)
    {
        var eventId = Guid.NewGuid();
        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = Guid.NewGuid(),
            Title = "Boardgame Night",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(3),
            GameIdsJson = "[]",
            Status = "Published",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Sessions =
            {
                new GameNightSessionEntity
                {
                    Id = Guid.NewGuid(),
                    GameNightEventId = eventId,
                    SessionId = sessionId,
                    GameId = Guid.NewGuid(),
                    GameTitle = "Catan",
                    PlayOrder = 1,
                    Status = "Pending"
                }
            }
        });
        await _dbContext.SaveChangesAsync(TestCancellationToken);
        return eventId;
    }

    /// <summary>
    /// Detached-root path (the 13 <c>GetByIdAsync</c> callers). Reload a persisted night, add a NEW
    /// invitee AND a NEW session, then <c>UpdateAsync</c> + <c>SaveChanges</c>. Both new child rows
    /// must exist afterwards. Before the fix, the detached <c>.Update()</c> marks the new children
    /// <c>Modified</c> → the <c>SaveChanges</c> throws <see cref="DbUpdateConcurrencyException"/> and
    /// nothing is persisted.
    /// </summary>
    [Fact(DisplayName = "UpdateAsync persists a new invitee AND a new session added after a detached reload")]
    public async Task UpdateAsync_DetachedRoot_AddInviteeAndSession_PersistsNewChildRows()
    {
        // ── Arrange ─────────────────────────────────────────────────────────────
        var eventId = await SeedPublishedEventAsync();
        var inviteeId = Guid.NewGuid();
        var trackingSessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var dbWrite = _fixture.CreateDbContext(_connectionString);
        var repo = CreateRepository(dbWrite);

        var night = await repo.GetByIdAsync(eventId, TestCancellationToken);
        night.Should().NotBeNull();

        // ── Act: add a brand-new invitee and a brand-new session, then persist ──
        night!.AddInvitees(new List<Guid> { inviteeId });
        night.AddSession(trackingSessionId, gameId, "Wingspan");

        await repo.UpdateAsync(night, TestCancellationToken);
        // Pre-fix: the detached graph marks both new children Modified → this throws
        // DbUpdateConcurrencyException (UPDATE affected 0 rows) and rolls the whole write back.
        await dbWrite.SaveChangesAsync(TestCancellationToken);

        // ── Assert: reload from a fresh context; both new child rows survive ──
        await using var dbVerify = _fixture.CreateDbContext(_connectionString);

        var rsvpRows = await dbVerify.GameNightRsvps
            .AsNoTracking()
            .CountAsync(r => r.EventId == eventId && r.UserId == inviteeId, TestCancellationToken);
        rsvpRows.Should().Be(1, "the invitee added after reload must be inserted, not lost");

        var sessionRows = await dbVerify.GameNightSessions
            .AsNoTracking()
            .CountAsync(s => s.GameNightEventId == eventId && s.SessionId == trackingSessionId, TestCancellationToken);
        sessionRows.Should().Be(1, "the session added after reload must be inserted, not lost");
    }

    /// <summary>
    /// Reproduces the <c>StartGameNightSessionCommandHandler</c> false-409: the handler adds a
    /// session and starts it (Pending → InProgress) then persists via <c>UpdateAsync</c>. Before the
    /// fix, that save ALWAYS throws <see cref="DbUpdateConcurrencyException"/> (the new session row is
    /// marked Modified), which the handler mis-maps to a "max live sessions" 409 even on the very
    /// first, uncontended start. This test proves the repository operation succeeds and persists the
    /// InProgress session, so the concurrency exception (hence the false 409) no longer fires.
    /// </summary>
    [Fact(DisplayName = "UpdateAsync persists an InProgress session on first start without a false concurrency conflict")]
    public async Task UpdateAsync_DetachedRoot_StartSession_PersistsInProgressSession_NoFalseConflict()
    {
        // ── Arrange ─────────────────────────────────────────────────────────────
        var eventId = await SeedPublishedEventAsync();
        var trackingSessionId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        await using var dbWrite = _fixture.CreateDbContext(_connectionString);
        var repo = CreateRepository(dbWrite);

        var night = await repo.GetByIdAsync(eventId, TestCancellationToken);
        night.Should().NotBeNull();

        // ── Act: mirror the handler — add a session, start it, persist ──
        night!.AddSession(trackingSessionId, gameId, "Catan");
        night.StartCurrentSession(); // Pending → InProgress

        await repo.UpdateAsync(night, TestCancellationToken);

        // The uncontended first start must NOT surface as a concurrency conflict.
        Func<Task> act = async () => await dbWrite.SaveChangesAsync(TestCancellationToken);
        await act.Should().NotThrowAsync<DbUpdateConcurrencyException>(
            "the first, uncontended start-session save must not raise the concurrency exception the " +
            "handler mis-maps to a false MAX_LIVE_SESSIONS_EXCEEDED 409");

        // ── Assert: the session is persisted and live ──
        await using var dbVerify = _fixture.CreateDbContext(_connectionString);
        var session = await dbVerify.GameNightSessions
            .AsNoTracking()
            .SingleAsync(s => s.GameNightEventId == eventId && s.SessionId == trackingSessionId, TestCancellationToken);
        session.Status.Should().Be("InProgress", "the started session must persist as InProgress");
    }

    /// <summary>
    /// Tracked-root path (the single <c>SessionStartedHandler</c> caller, which loads via
    /// <c>FindByLinkedSessionIdAsync</c> — TRACKED + Include). Adding a NEW child on an
    /// already-tracked root and persisting via <c>UpdateAsync</c> must not raise an EF identity
    /// conflict (the pre-fix full-graph re-map + <c>DbSet.Update()</c> collides with the tracked
    /// instance — the same failure the reminder job worked around, #2720). The new invitee row must
    /// persist and the pre-existing session must remain intact.
    /// </summary>
    [Fact(DisplayName = "UpdateAsync on a tracked root (FindByLinkedSessionId) persists a new invitee without an identity conflict")]
    public async Task UpdateAsync_TrackedRoot_ViaFindByLinkedSession_AddInvitee_PersistsWithoutIdentityConflict()
    {
        // ── Arrange: a Published night with one Pending session ──
        var trackingSessionId = Guid.NewGuid();
        var eventId = await SeedPublishedEventWithSessionAsync(trackingSessionId);
        var inviteeId = Guid.NewGuid();

        await using var dbWrite = _fixture.CreateDbContext(_connectionString);
        var repo = CreateRepository(dbWrite);

        // TRACKED load — the caller (SessionStartedHandler) needs change-tracking end-to-end.
        var night = await repo.FindByLinkedSessionIdAsync(trackingSessionId, TestCancellationToken);
        night.Should().NotBeNull();

        // ── Act: mutate a tracked root (new child) and persist ──
        night!.AddInvitees(new List<Guid> { inviteeId });

        await repo.UpdateAsync(night, TestCancellationToken);
        await dbWrite.SaveChangesAsync(TestCancellationToken);

        // ── Assert: the new invitee row exists and the original session is untouched ──
        await using var dbVerify = _fixture.CreateDbContext(_connectionString);

        var rsvpRows = await dbVerify.GameNightRsvps
            .AsNoTracking()
            .CountAsync(r => r.EventId == eventId && r.UserId == inviteeId, TestCancellationToken);
        rsvpRows.Should().Be(1, "the invitee added on a tracked root must be inserted");

        var sessionRows = await dbVerify.GameNightSessions
            .AsNoTracking()
            .CountAsync(s => s.GameNightEventId == eventId && s.SessionId == trackingSessionId, TestCancellationToken);
        sessionRows.Should().Be(1, "the pre-existing session must survive the update");
    }

    /// <summary>
    /// Guards a subtle data-loss trap in child reconciliation: votes are managed by the DEDICATED
    /// <c>AddVoteAsync</c> / <c>RemoveVoteAsync</c> paths, which insert/delete a single vote row
    /// WITHOUT touching the aggregate root — so casting a vote does NOT advance the root's xmin.
    /// A concurrently-cast vote is therefore invisible to a stale-snapshot <c>UpdateAsync</c>. If
    /// <c>UpdateAsync</c> reconciled votes with a Delete branch, that unrelated scalar update would
    /// silently DELETE the concurrently-cast vote (it appears in the DB but not in the loaded
    /// snapshot). <c>UpdateAsync</c> must therefore leave vote rows untouched.
    /// </summary>
    [Fact(DisplayName = "UpdateAsync must not delete a vote row added concurrently (absent from the loaded snapshot)")]
    public async Task UpdateAsync_DoesNotDeleteConcurrentlyAddedVote()
    {
        // ── Arrange: seed a Published night, then load it (snapshot has zero votes) ──
        var eventId = await SeedPublishedEventAsync();

        await using var dbWrite = _fixture.CreateDbContext(_connectionString);
        var repo = CreateRepository(dbWrite);

        var night = await repo.GetByIdAsync(eventId, TestCancellationToken);
        night.Should().NotBeNull();

        // A vote is cast from another context AFTER the snapshot was loaded (the AddVoteAsync path
        // inserts only the vote row and never advances the root xmin).
        var voteId = Guid.NewGuid();
        await using (var dbVote = _fixture.CreateDbContext(_connectionString))
        {
            dbVote.GameNightVotes.Add(new GameNightVoteEntity
            {
                Id = voteId,
                EventId = eventId,
                VoterUserId = Guid.NewGuid(),
                CandidateGameId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await dbVote.SaveChangesAsync(TestCancellationToken);
        }

        // ── Act: an unrelated scalar update on the stale aggregate ──
        night!.Update("Renamed Night", null, night.ScheduledAt, null, null, null);
        await repo.UpdateAsync(night, TestCancellationToken);
        await dbWrite.SaveChangesAsync(TestCancellationToken);

        // ── Assert: the concurrently-cast vote must survive the unrelated update ──
        await using var dbVerify = _fixture.CreateDbContext(_connectionString);
        var voteRows = await dbVerify.GameNightVotes
            .AsNoTracking()
            .CountAsync(v => v.Id == voteId, TestCancellationToken);
        voteRows.Should().Be(1, "UpdateAsync must not delete vote rows it never manages");
    }
}
