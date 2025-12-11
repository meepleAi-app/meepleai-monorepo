using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of GameSession repository.
/// Maps between domain GameSession entity and GameSessionEntity persistence model.
/// </summary>
public class GameSessionRepository : RepositoryBase, IGameSessionRepository
{
    public GameSessionRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<GameSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sessionEntity = await DbContext.GameSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);

        return sessionEntity != null ? MapToDomain(sessionEntity) : null;
    }

    public async Task<IReadOnlyList<GameSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var sessionEntities = await DbContext.GameSessions
            .AsNoTracking()
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return sessionEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameSession>> FindActiveByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var sessionEntities = await DbContext.GameSessions
            .AsNoTracking()
            .Where(s => s.GameId == gameId &&
                       (s.Status == "Setup" || s.Status == "InProgress" || s.Status == "Paused"))
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return sessionEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameSession>> FindByGameIdAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        var sessionEntities = await DbContext.GameSessions
            .AsNoTracking()
            .Where(s => s.GameId == gameId)
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return sessionEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameSession>> FindByPlayerNameAsync(string playerName, CancellationToken cancellationToken = default)
    {
        var normalized = playerName.Trim().ToLowerInvariant();

        var sessionEntities = await DbContext.GameSessions
            .AsNoTracking()
            .Where(s => EF.Functions.ILike(s.PlayersJson, $"%{normalized}%"))
            .OrderByDescending(s => s.StartedAt)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Filter in-memory for exact player name match
        var sessions = sessionEntities
            .Select(MapToDomain)
            .Where(s => s.HasPlayer(playerName))
            .ToList();

        return sessions;
    }

    public async Task<IReadOnlyList<GameSession>> FindActiveAsync(int? limit = null, int? offset = null, CancellationToken cancellationToken = default)
    {
        IQueryable<Api.Infrastructure.Entities.GameSessionEntity> query = DbContext.GameSessions
            .AsNoTracking()
            .Where(s => s.Status == "Setup" || s.Status == "InProgress" || s.Status == "Paused")
            .OrderByDescending(s => s.StartedAt);

        if (offset.HasValue)
            query = query.Skip(offset.Value);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        var sessionEntities = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return sessionEntities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<GameSession>> FindHistoryAsync(
        Guid? gameId = null,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int? limit = null,
        int? offset = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<Api.Infrastructure.Entities.GameSessionEntity> query = DbContext.GameSessions
            .AsNoTracking()
            .Where(s => s.Status == "Completed" || s.Status == "Abandoned");

        if (gameId.HasValue)
            query = query.Where(s => s.GameId == gameId.Value);

        if (startDate.HasValue)
            query = query.Where(s => s.StartedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(s => s.CompletedAt <= endDate.Value);

        query = query.OrderByDescending(s => s.CompletedAt ?? s.StartedAt);

        if (offset.HasValue)
            query = query.Skip(offset.Value);

        if (limit.HasValue)
            query = query.Take(limit.Value);

        var sessionEntities = await query.ToListAsync(cancellationToken).ConfigureAwait(false);
        return sessionEntities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE mapping to persistence entity
        CollectDomainEvents(session);

        var sessionEntity = MapToPersistence(session);
        await DbContext.GameSessions.AddAsync(sessionEntity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        // Collect domain events BEFORE updating persistence entity
        CollectDomainEvents(session);

        var sessionEntity = MapToPersistence(session);

        // Detach existing tracked entity to avoid conflicts (SPRINT-5 Issue #1142)
        var tracked = DbContext.ChangeTracker.Entries<GameSessionEntity>()
            .FirstOrDefault(e => e.Entity.Id == sessionEntity.Id);

        if (tracked != null)
            tracked.State = EntityState.Detached;

        DbContext.GameSessions.Update(sessionEntity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(GameSession session, CancellationToken cancellationToken = default)
    {
        var sessionEntity = MapToPersistence(session);
        DbContext.GameSessions.Remove(sessionEntity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.GameSessions.AnyAsync(s => s.Id == id, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain entity.
    /// </summary>
    private static GameSession MapToDomain(Api.Infrastructure.Entities.GameSessionEntity entity)
    {
        // Deserialize players from JSON
        var playerDtos = JsonSerializer.Deserialize<List<SessionPlayerDto>>(entity.PlayersJson) ?? new List<SessionPlayerDto>();
        var players = playerDtos.Select(dto => new SessionPlayer(dto.PlayerName, dto.PlayerOrder, dto.Color)).ToList();

        // Reconstruct session
        var session = new GameSession(
            id: entity.Id,
            gameId: entity.GameId,
            players: players
        );

        // Override status and timestamps via reflection
        var statusProp = typeof(GameSession).GetProperty("Status");
        var status = entity.Status switch
        {
            "Setup" => SessionStatus.Setup,
            "InProgress" => SessionStatus.InProgress,
            "Paused" => SessionStatus.Paused,
            "Completed" => SessionStatus.Completed,
            "Abandoned" => SessionStatus.Abandoned,
            _ => SessionStatus.Setup
        };
        statusProp?.SetValue(session, status);

        var startedAtProp = typeof(GameSession).GetProperty("StartedAt");
        startedAtProp?.SetValue(session, entity.StartedAt);

        var completedAtProp = typeof(GameSession).GetProperty("CompletedAt");
        completedAtProp?.SetValue(session, entity.CompletedAt);

        var winnerNameProp = typeof(GameSession).GetProperty("WinnerName");
        winnerNameProp?.SetValue(session, entity.WinnerName);

        var notesProp = typeof(GameSession).GetProperty("Notes");
        notesProp?.SetValue(session, entity.Notes);

        return session;
    }

    /// <summary>
    /// Maps domain entity to persistence entity.
    /// </summary>
    private static Api.Infrastructure.Entities.GameSessionEntity MapToPersistence(GameSession domainEntity)
    {
        // Serialize players to JSON
        var playerDtos = domainEntity.Players.Select(p => new SessionPlayerDto(
            PlayerName: p.PlayerName,
            PlayerOrder: p.PlayerOrder,
            Color: p.Color
        )).ToList();

        var playersJson = JsonSerializer.Serialize(playerDtos);

        return new Api.Infrastructure.Entities.GameSessionEntity
        {
            Id = domainEntity.Id,
            GameId = domainEntity.GameId,
            Status = domainEntity.Status.Value,
            StartedAt = domainEntity.StartedAt,
            CompletedAt = domainEntity.CompletedAt,
            WinnerName = domainEntity.WinnerName,
            Notes = domainEntity.Notes,
            PlayersJson = playersJson
        };
    }

    // Simple DTO for JSON serialization
    private sealed record SessionPlayerDto(string PlayerName, int PlayerOrder, string? Color);
}
