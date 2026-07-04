using System.Diagnostics;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Observability;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core-backed repository for live game sessions.
/// Issue #2097 / ADR-060: Replaced ConcurrentDictionary in-memory implementation
/// with persistent storage on the live_game_sessions table tree.
/// Live sessions now survive container restarts and are multi-instance ready.
/// </summary>
internal sealed class LiveSessionRepository : RepositoryBase, ILiveSessionRepository
{
    private readonly ILogger<LiveSessionRepository> _logger;

    public LiveSessionRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        ILogger<LiveSessionRepository> logger)
        : base(dbContext, eventCollector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LiveGameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.LiveGameSessions
            .AsNoTracking()
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .Include(e => e.DiaryEntries)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? LiveGameSessionMapper.ToDomain(entity) : null;
    }

    public async Task<LiveGameSession?> GetByCodeAsync(string sessionCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionCode);
        var normalized = sessionCode.ToUpperInvariant();
        var entity = await DbContext.LiveGameSessions
            .AsNoTracking()
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .Include(e => e.DiaryEntries)
            .FirstOrDefaultAsync(e => e.SessionCode == normalized, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? LiveGameSessionMapper.ToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<LiveGameSession>> GetActiveByUserIdAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        // Mirrors LiveGameSession.IsActive (Setup | InProgress | Paused). 'Created' is
        // intentionally excluded — a session that's been created but never moved past the
        // initial state is not "actively" being played and should not show up in
        // dashboards/auto-save sweeps.
        var entities = await DbContext.LiveGameSessions
            .AsNoTracking()
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .Include(e => e.DiaryEntries)
            .Where(e => e.CreatedByUserId == userId && ActiveStatuses.Contains(e.Status))
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(LiveGameSessionMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<LiveGameSession>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.LiveGameSessions
            .AsNoTracking()
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .Include(e => e.DiaryEntries)
            .Where(e => ActiveStatuses.Contains(e.Status))
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(LiveGameSessionMapper.ToDomain).ToList();
    }

    private static readonly int[] ActiveStatuses =
    {
        (int)LiveSessionStatus.Setup,
        (int)LiveSessionStatus.InProgress,
        (int)LiveSessionStatus.Paused
    };

    public async Task AddAsync(LiveGameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        CollectDomainEvents(session);

        var entity = LiveGameSessionMapper.ToEntity(session);
        await DbContext.LiveGameSessions.AddAsync(entity, cancellationToken).ConfigureAwait(false);

        MeepleAiMetrics.LiveSessionWritesTotal.Add(
            1,
            new KeyValuePair<string, object?>("op", "create"));

        _logger.LogDebug("Staged LiveGameSession {SessionId} for insert", session.Id);
    }

    public async Task UpdateAsync(LiveGameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        CollectDomainEvents(session);

        var sw = Stopwatch.StartNew();

        // Map to entity snapshot for scalar values and child collections
        var snapshot = LiveGameSessionMapper.ToEntity(session);

        // Preserve Entity-only fields that Domain doesn't surface, AND read the existing diary
        // ids in the SAME round-trip. TotalPausedDurationMs (Issue #216 server-side timer) lives
        // only on the Entity; read it back from DB (AsNoTracking, no change-tracker pollution) to
        // prevent reset. #2575: the diary-id set is folded into this projection so the append-only
        // diary sync no longer needs its own standalone SELECT.
        var loadProjection = await DbContext.LiveGameSessions
            .AsNoTracking()
            .Where(e => e.Id == session.Id)
            .Select(e => new
            {
                e.TotalPausedDurationMs,
                DiaryIds = e.DiaryEntries.Select(d => d.Id).ToList(),
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        snapshot.TotalPausedDurationMs = loadProjection?.TotalPausedDurationMs ?? 0;
        var existingDiaryIds = (loadProjection?.DiaryIds ?? new List<Guid>()).ToHashSet();

        // Capture child collection snapshots BEFORE modifying snapshot.Players etc.
        var snapshotPlayers = snapshot.Players.ToList();
        var snapshotTeams = snapshot.Teams.ToList();
        var snapshotRoundScores = snapshot.RoundScores.ToList();
        var snapshotTurnRecords = snapshot.TurnRecords.ToList();
        var snapshotDiaryEntries = snapshot.DiaryEntries.ToList(); // #2570 SP3 T2

        // ── Root entity: use Entry-based update so EF uses OriginalValues.Xmin (xmin concurrency token) ──────
        var trackedRoot = DbContext.ChangeTracker.Entries<LiveGameSessionEntity>()
            .FirstOrDefault(e => e.Entity.Id == session.Id)?.Entity;

        if (trackedRoot == null)
        {
            // Not in cache: attach snapshot as disconnected entity and let EF generate
            // a standard UPDATE WHERE id=... AND xmin=@original.
            snapshot.Players.Clear();
            snapshot.Teams.Clear();
            snapshot.RoundScores.Clear();
            snapshot.TurnRecords.Clear();
            snapshot.DiaryEntries.Clear(); // #2570 SP3 T2
            DbContext.Entry(snapshot).State = EntityState.Modified;
        }
        else
        {
            // In cache: copy scalar properties from snapshot onto the tracked entity.
            // EF keeps OriginalValues from the load, so WHERE row_version = @original is correct.
            DbContext.Entry(trackedRoot).CurrentValues.SetValues(snapshot);
        }

        // ── Child collections: sync against DB-resident rows ──────────────────────────────
        await SyncPlayersAsync(session, snapshotPlayers, cancellationToken).ConfigureAwait(false);
        await SyncTeamsAsync(session, snapshotTeams, cancellationToken).ConfigureAwait(false);
        await SyncRoundScoresAsync(session, snapshotRoundScores, cancellationToken).ConfigureAwait(false);
        await SyncTurnRecordsAsync(session, snapshotTurnRecords, cancellationToken).ConfigureAwait(false);
        SyncDiaryEntries(snapshotDiaryEntries, existingDiaryIds); // #2570 SP3 T2 / #2575 refactor

        // Record metrics on the happy path only. If any of the above threw, we never reach here
        // and writes_total{op=update} stays consistent with actually-staged writes. The counter
        // still measures STAGED operations (caller's SaveChangesAsync may still fail with a
        // DbUpdateConcurrencyException) — duration histogram likewise observes mapper+sync work.
        sw.Stop();
        MeepleAiMetrics.LiveSessionWritesTotal.Add(
            1,
            new KeyValuePair<string, object?>("op", "update"));
        MeepleAiMetrics.LiveSessionUpdateDurationSeconds.Record(sw.Elapsed.TotalSeconds);

        _logger.LogDebug("Staged LiveGameSession {SessionId} for update", session.Id);
    }

    // ── Child collection sync helpers ─────────────────────────────────────────────────────

    // ── Generic child entity attachment helper ────────────────────────────────────────────

    /// <summary>
    /// Attaches <paramref name="snapshotEntity"/> to the change tracker with the specified
    /// <paramref name="targetState"/>, correctly handling the case where an entity with the
    /// same primary key is already tracked (uses SetValues in-place rather than re-attaching
    /// the new object, which would throw InvalidOperationException).
    /// </summary>
    private void AttachOrUpdate<T>(T snapshotEntity, Guid id, EntityState targetState)
        where T : class
    {
        var existingEntry = DbContext.ChangeTracker.Entries<T>()
            .FirstOrDefault(e => e.Property("Id").CurrentValue is Guid g && g == id);

        if (existingEntry != null)
        {
            // Entity already tracked — update in place via SetValues so we don't conflict
            if (targetState == EntityState.Modified)
            {
                existingEntry.CurrentValues.SetValues(snapshotEntity);
                existingEntry.State = EntityState.Modified;
            }
            else if (targetState == EntityState.Deleted)
            {
                existingEntry.State = EntityState.Deleted;
            }
            // EntityState.Added is a no-op if already tracked (entity exists)
        }
        else
        {
            DbContext.Entry(snapshotEntity).State = targetState;
        }
    }

    private async Task SyncPlayersAsync(
        LiveGameSession session, ICollection<SessionPlayerEntity> snapshotPlayers,
        CancellationToken cancellationToken)
    {
        var existingIds = await DbContext.Set<SessionPlayerEntity>()
            .AsNoTracking()
            .Where(e => e.LiveGameSessionId == session.Id)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var p in snapshotPlayers)
        {
            AttachOrUpdate(p, p.Id, existingIds.Contains(p.Id)
                ? EntityState.Modified
                : EntityState.Added);
        }

        // Delete players removed from domain
        var snapshotIds = snapshotPlayers.Select(p => p.Id).ToHashSet();
        foreach (var removedId in existingIds.Where(id => !snapshotIds.Contains(id)))
        {
            var stub = new SessionPlayerEntity { Id = removedId, LiveGameSessionId = session.Id };
            AttachOrUpdate(stub, removedId, EntityState.Deleted);
        }
    }

    private async Task SyncTeamsAsync(
        LiveGameSession session, ICollection<SessionTeamEntity> snapshotTeams,
        CancellationToken cancellationToken)
    {
        var existingIds = await DbContext.Set<SessionTeamEntity>()
            .AsNoTracking()
            .Where(e => e.LiveGameSessionId == session.Id)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var t in snapshotTeams)
        {
            AttachOrUpdate(t, t.Id, existingIds.Contains(t.Id)
                ? EntityState.Modified
                : EntityState.Added);
        }

        var snapshotIds = snapshotTeams.Select(t => t.Id).ToHashSet();
        foreach (var removedId in existingIds.Where(id => !snapshotIds.Contains(id)))
        {
            var stub = new SessionTeamEntity { Id = removedId, LiveGameSessionId = session.Id };
            AttachOrUpdate(stub, removedId, EntityState.Deleted);
        }
    }

    private async Task SyncRoundScoresAsync(
        LiveGameSession session, ICollection<LiveRoundScoreEntity> snapshotScores,
        CancellationToken cancellationToken)
    {
        var existingIds = await DbContext.Set<LiveRoundScoreEntity>()
            .AsNoTracking()
            .Where(e => e.LiveGameSessionId == session.Id)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var s in snapshotScores)
        {
            AttachOrUpdate(s, s.Id, existingIds.Contains(s.Id)
                ? EntityState.Modified
                : EntityState.Added);
        }

        var snapshotIds = snapshotScores.Select(s => s.Id).ToHashSet();
        foreach (var removedId in existingIds.Where(id => !snapshotIds.Contains(id)))
        {
            var stub = new LiveRoundScoreEntity { Id = removedId, LiveGameSessionId = session.Id };
            AttachOrUpdate(stub, removedId, EntityState.Deleted);
        }
    }

    private async Task SyncTurnRecordsAsync(
        LiveGameSession session, ICollection<LiveTurnRecordEntity> snapshotTurnRecords,
        CancellationToken cancellationToken)
    {
        var existingIds = await DbContext.Set<LiveTurnRecordEntity>()
            .AsNoTracking()
            .Where(e => e.LiveGameSessionId == session.Id)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var t in snapshotTurnRecords)
        {
            AttachOrUpdate(t, t.Id, existingIds.Contains(t.Id)
                ? EntityState.Modified
                : EntityState.Added);
        }

        var snapshotIds = snapshotTurnRecords.Select(t => t.Id).ToHashSet();
        foreach (var removedId in existingIds.Where(id => !snapshotIds.Contains(id)))
        {
            var stub = new LiveTurnRecordEntity { Id = removedId, LiveGameSessionId = session.Id };
            AttachOrUpdate(stub, removedId, EntityState.Deleted);
        }
    }

    /// <summary>
    /// #2570 SP3 T2: Diary entries are append-only — only INSERT new entries, never UPDATE
    /// or DELETE existing ones. The domain guarantees immutability post-creation; the cascade
    /// on session delete covers the cleanup path automatically.
    /// #2575: the set of already-persisted ids comes from the load-time projection in UpdateAsync
    /// (one fewer DB round-trip), not a fresh SELECT. Synchronous — it only stages EF state.
    /// </summary>
    private void SyncDiaryEntries(
        ICollection<LiveSessionDiaryEntryEntity> snapshotEntries,
        IReadOnlyCollection<Guid> existingDiaryIds)
    {
        // Diary entries are append-only: INSERT new, skip existing (immutable).
        // No DELETE path — the domain never removes diary entries.
        foreach (var entry in snapshotEntries)
        {
            if (!existingDiaryIds.Contains(entry.Id))
            {
                AttachOrUpdate(entry, entry.Id, EntityState.Added);
            }
            // Existing entries are immutable; skipping update is correct by design.
        }
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.LiveGameSessions
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

}
