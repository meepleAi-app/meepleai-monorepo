using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.GameManagement.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of PlayRecord repository.
/// Maps between domain PlayRecord entity and PlayRecordEntity persistence model.
/// Issue #3889: CQRS commands for play records.
/// </summary>
internal class PlayRecordRepository : RepositoryBase, IPlayRecordRepository
{
    public PlayRecordRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<PlayRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<PlayRecord?> GetByIdWithPlayersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PlayRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .OrderByDescending(r => r.SessionDate)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<(IReadOnlyList<PlayRecord> Records, int Total)> GetUserHistoryAsync(
        Guid userId,
        int page,
        int pageSize,
        Guid? gameId = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Where(r => r.CreatedByUserId == userId);

        if (gameId.HasValue)
        {
            query = query.Where(r => r.GameId == gameId.Value);
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entities = await query
            .OrderByDescending(r => r.SessionDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (entities.Select(MapToDomain).ToList(), total);
    }

    public async Task<bool> CanUserViewAsync(Guid userId, Guid recordId, CancellationToken cancellationToken = default)
    {
        var record = await DbContext.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
            .FirstOrDefaultAsync(r => r.Id == recordId, cancellationToken)
            .ConfigureAwait(false);

        if (record == null) return false;

        // Creator can always view
        if (record.CreatedByUserId == userId) return true;

        // Group members can view group records
        // Note: Group feature not yet implemented - will add group membership check in Issue #3891

        // Players can view their records
        return record.Players.Any(p => p.UserId == userId);
    }

    public async Task<bool> CanUserEditAsync(Guid userId, Guid recordId, CancellationToken cancellationToken = default)
    {
        var creatorId = await DbContext.PlayRecords
            .AsNoTracking()
            .Where(r => r.Id == recordId)
            .Select(r => r.CreatedByUserId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return creatorId == userId;
    }

    public async Task AddAsync(PlayRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        CollectDomainEvents(record);

        var entity = MapToPersistence(record);
        await DbContext.PlayRecords.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(PlayRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        CollectDomainEvents(record);

        var entity = MapToPersistence(record);
        DbContext.PlayRecords.Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(PlayRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var entity = MapToPersistence(record);
        DbContext.PlayRecords.Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.PlayRecords
            .AsNoTracking()
            .AnyAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Maps persistence entity to domain aggregate.
    /// </summary>
    private static PlayRecord MapToDomain(PlayRecordEntity entity)
    {
        // Deserialize scoring config
        var scoringConfig = JsonSerializer.Deserialize<SessionScoringConfigDto>(entity.ScoringConfigJson)
            ?? throw new InvalidOperationException("Failed to deserialize ScoringConfigJson");

        var config = new SessionScoringConfig(
            scoringConfig.EnabledDimensions,
            scoringConfig.DimensionUnits);

        // Create record using appropriate factory
        PlayRecord record;
        if (entity.GameId.HasValue)
        {
            record = PlayRecord.CreateWithGame(
                entity.Id,
                entity.GameId.Value,
                entity.GameName,
                entity.CreatedByUserId,
                entity.SessionDate,
                (PlayRecordVisibility)entity.Visibility,
                timeProvider: null,
                groupId: entity.GroupId,
                scoringConfig: config);
        }
        else
        {
            record = PlayRecord.CreateFreeForm(
                entity.Id,
                entity.GameName,
                entity.CreatedByUserId,
                entity.SessionDate,
                (PlayRecordVisibility)entity.Visibility,
                config,
                timeProvider: null,
                groupId: entity.GroupId);
        }

        // Restore state via reflection (private setters)
        SetPrivateProperty(record, nameof(PlayRecord.Status), (PlayRecordStatus)entity.Status);
        SetPrivateProperty(record, nameof(PlayRecord.StartTime), entity.StartTime);
        SetPrivateProperty(record, nameof(PlayRecord.EndTime), entity.EndTime);
        SetPrivateProperty(record, nameof(PlayRecord.Duration), entity.Duration);
        SetPrivateProperty(record, nameof(PlayRecord.Notes), entity.Notes);
        SetPrivateProperty(record, nameof(PlayRecord.Location), entity.Location);
        SetPrivateProperty(record, nameof(PlayRecord.CreatedAt), entity.CreatedAt);
        SetPrivateProperty(record, nameof(PlayRecord.UpdatedAt), entity.UpdatedAt);

        // Restore players and scores
        foreach (var playerEntity in entity.Players)
        {
            record.AddPlayer(playerEntity.UserId, playerEntity.DisplayName);
            var player = record.Players[^1];  // Last player added

            foreach (var scoreEntity in playerEntity.Scores)
            {
                var score = new RecordScore(scoreEntity.Dimension, scoreEntity.Value, scoreEntity.Unit);
                record.RecordScore(player.Id, score);
            }
        }

        return record;
    }

    /// <summary>
    /// Maps domain aggregate to persistence entity.
    /// </summary>
    private static PlayRecordEntity MapToPersistence(PlayRecord record)
    {
        var entity = new PlayRecordEntity
        {
            Id = record.Id,
            GameId = record.GameId,
            GameName = record.GameName,
            CreatedByUserId = record.CreatedByUserId,
            Visibility = (int)record.Visibility,
            GroupId = record.GroupId,
            SessionDate = record.SessionDate,
            StartTime = record.StartTime,
            EndTime = record.EndTime,
            Duration = record.Duration,
            Status = (int)record.Status,
            Notes = record.Notes,
            Location = record.Location,
            CreatedAt = record.CreatedAt,
            UpdatedAt = record.UpdatedAt
        };

        // Serialize scoring config
        var configDto = new SessionScoringConfigDto
        {
            EnabledDimensions = record.ScoringConfig.EnabledDimensions.ToList(),
            DimensionUnits = record.ScoringConfig.DimensionUnits.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value,
                StringComparer.OrdinalIgnoreCase)
        };
        entity.ScoringConfigJson = JsonSerializer.Serialize(configDto);

        // Map players
        foreach (var player in record.Players)
        {
            var playerEntity = new RecordPlayerEntity
            {
                Id = player.Id,
                PlayRecordId = record.Id,
                UserId = player.UserId,
                DisplayName = player.DisplayName
            };

            // Map scores
            foreach (var score in player.Scores)
            {
                var scoreEntity = new RecordScoreEntity
                {
                    Id = Guid.NewGuid(),
                    RecordPlayerId = player.Id,
                    Dimension = score.Dimension,
                    Value = score.Value,
                    Unit = score.Unit
                };
                playerEntity.Scores.Add(scoreEntity);
            }

            entity.Players.Add(playerEntity);
        }

        return entity;
    }

    private static void SetPrivateProperty(object obj, string propertyName, object? value)
    {
        var property = obj.GetType().GetProperty(propertyName);
        if (property != null && property.CanWrite)
        {
            property.SetValue(obj, value);
        }
    }

    /// <summary>
    /// DTO for JSON serialization of SessionScoringConfig.
    /// </summary>
    private sealed class SessionScoringConfigDto
    {
        public List<string> EnabledDimensions { get; set; } = new();
        public Dictionary<string, string> DimensionUnits { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}
