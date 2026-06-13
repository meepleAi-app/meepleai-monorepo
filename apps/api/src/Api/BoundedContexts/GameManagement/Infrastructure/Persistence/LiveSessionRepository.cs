using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
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
        var activeStatuses = new[]
        {
            (int)LiveSessionStatus.Created,
            (int)LiveSessionStatus.Setup,
            (int)LiveSessionStatus.InProgress,
            (int)LiveSessionStatus.Paused
        };

        var entities = await DbContext.LiveGameSessions
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .Where(e => e.CreatedByUserId == userId && activeStatuses.Contains(e.Status))
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(LiveGameSessionMapper.ToDomain).ToList();
    }

    public async Task<IReadOnlyList<LiveGameSession>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var activeStatuses = new[]
        {
            (int)LiveSessionStatus.Created,
            (int)LiveSessionStatus.Setup,
            (int)LiveSessionStatus.InProgress,
            (int)LiveSessionStatus.Paused
        };

        var entities = await DbContext.LiveGameSessions
            .Include(e => e.Players)
            .Include(e => e.Teams)
            .Include(e => e.RoundScores)
            .Include(e => e.TurnRecords)
            .Where(e => activeStatuses.Contains(e.Status))
            .OrderByDescending(e => e.UpdatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(LiveGameSessionMapper.ToDomain).ToList();
    }

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

        var entity = LiveGameSessionMapper.ToEntity(session);

        // Preserve Entity-only fields that the Domain aggregate doesn't surface.
        // TotalPausedDurationMs (Issue #216 server-side timer) lives only on the Entity;
        // without this round-trip read it would be silently reset to 0 on every UPDATE
        // because the mapper writes the default. AsNoTracking avoids polluting the
        // change tracker before we issue the Update.
        entity.TotalPausedDurationMs = await DbContext.LiveGameSessions
            .AsNoTracking()
            .Where(e => e.Id == session.Id)
            .Select(e => e.TotalPausedDurationMs)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        DbContext.LiveGameSessions.Update(entity);

        _logger.LogDebug("Staged LiveGameSession {SessionId} for update", session.Id);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.LiveGameSessions
            .AsNoTracking()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }
}
