using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
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
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? LiveGameSessionMapper.ToDomain(entity) : null;
    }

    public async Task<LiveGameSession?> GetByCodeAsync(string sessionCode, CancellationToken cancellationToken = default)
    {
        var normalized = sessionCode?.ToUpperInvariant();
        var entity = await DbContext.LiveGameSessions
            .AsNoTracking()
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
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

        _logger.LogDebug("Staged LiveGameSession {SessionId} for insert", session.Id);
    }

    public async Task UpdateAsync(LiveGameSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        CollectDomainEvents(session);

        // Map to entity snapshot for scalar values and child collections
        var snapshot = LiveGameSessionMapper.ToEntity(session);

        // Preserve Entity-only fields that Domain doesn't surface.
        // TotalPausedDurationMs (Issue #216 server-side timer) lives only on the Entity;
        // read it back from DB (AsNoTracking, no change-tracker pollution) to prevent reset.
        snapshot.TotalPausedDurationMs = await DbContext.LiveGameSessions
            .AsNoTracking()
            .Where(e => e.Id == session.Id)
            .Select(e => e.TotalPausedDurationMs)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Capture child collection snapshots BEFORE modifying snapshot.Players etc.
        var snapshotPlayers = snapshot.Players.ToList();
        var snapshotTeams = snapshot.Teams.ToList();
        var snapshotRoundScores = snapshot.RoundScores.ToList();
        var snapshotTurnRecords = snapshot.TurnRecords.ToList();

        // ── Root entity: use Entry-based update so EF uses OriginalValues.RowVersion ──────
        // Find or load the tracked root entity. If it's already in the change tracker
        // (same DbContext scope), EF returns the cached instance; otherwise it hits the DB.
        // We then set CurrentValues from the snapshot so EF marks scalars as Modified.
        var trackedRoot = DbContext.ChangeTracker.Entries<LiveGameSessionEntity>()
            .FirstOrDefault(e => e.Entity.Id == session.Id)?.Entity;

        if (trackedRoot == null)
        {
            // Not in cache: attach snapshot as disconnected entity and let EF generate
            // a standard UPDATE WHERE id=... AND row_version=@original.
            // Clear navigation collections on the snapshot root so EF doesn't double-track
            // children that we will handle explicitly below via SyncXxxAsync.
            snapshot.Players.Clear();
            snapshot.Teams.Clear();
            snapshot.RoundScores.Clear();
            snapshot.TurnRecords.Clear();
            DbContext.Entry(snapshot).State = EntityState.Modified;
        }
        else
        {
            // In cache: copy scalar properties from snapshot onto the tracked entity.
            // EF keeps OriginalValues from the load, so WHERE row_version = @original is correct.
            DbContext.Entry(trackedRoot).CurrentValues.SetValues(snapshot);
        }

        // ── Child collections: sync against DB-resident rows ──────────────────────────────
        // Use the captured snapshots (BEFORE Clear() was called on the root entity's collections).
        await SyncPlayersAsync(session, snapshotPlayers, cancellationToken).ConfigureAwait(false);
        await SyncTeamsAsync(session, snapshotTeams, cancellationToken).ConfigureAwait(false);
        await SyncRoundScoresAsync(session, snapshotRoundScores, cancellationToken).ConfigureAwait(false);
        await SyncTurnRecordsAsync(session, snapshotTurnRecords, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Staged LiveGameSession {SessionId} for update", session.Id);
    }

    // ── Child collection sync helpers ─────────────────────────────────────────────────────

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
            if (existingIds.Contains(p.Id))
                DbContext.Entry(p).State = EntityState.Modified;
            else
                DbContext.Entry(p).State = EntityState.Added;
        }

        // Delete players removed from domain
        var snapshotIds = snapshotPlayers.Select(p => p.Id).ToHashSet();
        foreach (var removedId in existingIds.Where(id => !snapshotIds.Contains(id)))
        {
            var stub = new SessionPlayerEntity { Id = removedId, LiveGameSessionId = session.Id };
            DbContext.Entry(stub).State = EntityState.Deleted;
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
            DbContext.Entry(t).State = existingIds.Contains(t.Id)
                ? EntityState.Modified
                : EntityState.Added;
        }

        var snapshotIds = snapshotTeams.Select(t => t.Id).ToHashSet();
        foreach (var removedId in existingIds.Where(id => !snapshotIds.Contains(id)))
        {
            DbContext.Entry(new SessionTeamEntity { Id = removedId, LiveGameSessionId = session.Id })
                .State = EntityState.Deleted;
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
            DbContext.Entry(s).State = existingIds.Contains(s.Id)
                ? EntityState.Modified
                : EntityState.Added;
        }

        var snapshotIds = snapshotScores.Select(s => s.Id).ToHashSet();
        foreach (var removedId in existingIds.Where(id => !snapshotIds.Contains(id)))
        {
            DbContext.Entry(new LiveRoundScoreEntity { Id = removedId, LiveGameSessionId = session.Id })
                .State = EntityState.Deleted;
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
            DbContext.Entry(t).State = existingIds.Contains(t.Id)
                ? EntityState.Modified
                : EntityState.Added;
        }

        var snapshotIds = snapshotTurnRecords.Select(t => t.Id).ToHashSet();
        foreach (var removedId in existingIds.Where(id => !snapshotIds.Contains(id)))
        {
            DbContext.Entry(new LiveTurnRecordEntity { Id = removedId, LiveGameSessionId = session.Id })
                .State = EntityState.Deleted;
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
