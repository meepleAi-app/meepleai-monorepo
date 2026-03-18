using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.AgentMemory.Infrastructure.Entities;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.AgentMemory.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IPlayerMemoryRepository.
/// Maps between domain PlayerMemory aggregate and PlayerMemoryEntity persistence model.
/// Serializes/deserializes JSONB fields for player game statistics.
/// </summary>
internal sealed class PlayerMemoryRepository : RepositoryBase, IPlayerMemoryRepository
{
    public PlayerMemoryRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<PlayerMemory?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.PlayerMemories
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<PlayerMemory?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var entity = await DbContext.PlayerMemories
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.UserId == userId, ct)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<PlayerMemory>> GetByGroupIdAsync(Guid groupId, CancellationToken ct = default)
    {
        var entities = await DbContext.PlayerMemories
            .AsNoTracking()
            .Where(e => e.GroupId == groupId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<PlayerMemory?> GetByGuestNameAsync(string guestName, CancellationToken ct = default)
    {
        var entity = await DbContext.PlayerMemories
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.GuestName == guestName && e.UserId == null, ct)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(PlayerMemory memory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        CollectDomainEvents(memory);

        var entity = MapToPersistence(memory);
        await DbContext.PlayerMemories.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(PlayerMemory memory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        CollectDomainEvents(memory);

        var entity = MapToPersistence(memory);
        DbContext.PlayerMemories.Update(entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps persistence entity to domain aggregate.
    /// </summary>
    private static PlayerMemory MapToDomain(PlayerMemoryEntity entity)
    {
        PlayerMemory memory;

        if (entity.UserId.HasValue)
        {
            memory = PlayerMemory.CreateForUser(entity.UserId.Value, entity.GroupId);
        }
        else
        {
            memory = PlayerMemory.CreateForGuest(entity.GuestName ?? "Unknown", entity.GroupId);
        }

        // Restore Id, CreatedAt, ClaimedAt via reflection (factory generates new ones)
        SetPrivateProperty(memory, nameof(PlayerMemory.Id), entity.Id);
        SetPrivateProperty(memory, nameof(PlayerMemory.CreatedAt), entity.CreatedAt);
        SetPrivateProperty(memory, nameof(PlayerMemory.ClaimedAt), entity.ClaimedAt);

        // For guest records that were later claimed, restore UserId
        // (CreateForGuest doesn't set UserId, but the entity might have it from a claim)
        if (entity.GuestName != null && entity.UserId.HasValue)
        {
            SetPrivateProperty(memory, nameof(PlayerMemory.UserId), entity.UserId);
        }

        // Restore game stats from JSONB
        if (entity.GameStatsJson != null)
        {
            var stats = JsonSerializer.Deserialize<List<PlayerGameStatsDto>>(entity.GameStatsJson);
            if (stats != null)
            {
#pragma warning disable S3011 // Reflection needed for domain reconstruction from persistence
                var gameStatsList = (List<PlayerGameStats>)typeof(PlayerMemory)
                    .GetField("_gameStats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .GetValue(memory)!;
#pragma warning restore S3011

                foreach (var stat in stats)
                {
                    gameStatsList.Add(new PlayerGameStats
                    {
                        GameId = stat.GameId,
                        Wins = stat.Wins,
                        Losses = stat.Losses,
                        TotalPlayed = stat.TotalPlayed,
                        BestScore = stat.BestScore,
                    });
                }
            }
        }

        return memory;
    }

    /// <summary>
    /// Maps domain aggregate to persistence entity.
    /// </summary>
    private static PlayerMemoryEntity MapToPersistence(PlayerMemory memory)
    {
        var entity = new PlayerMemoryEntity
        {
            Id = memory.Id,
            UserId = memory.UserId,
            GuestName = memory.GuestName,
            GroupId = memory.GroupId,
            ClaimedAt = memory.ClaimedAt,
            CreatedAt = memory.CreatedAt,
        };

        // Serialize game stats to JSONB
        if (memory.GameStats.Count > 0)
        {
            var dtos = memory.GameStats.Select(s => new PlayerGameStatsDto
            {
                GameId = s.GameId,
                Wins = s.Wins,
                Losses = s.Losses,
                TotalPlayed = s.TotalPlayed,
                BestScore = s.BestScore,
            }).ToList();
            entity.GameStatsJson = JsonSerializer.Serialize(dtos);
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

    /// <summary>DTO for JSON serialization of PlayerGameStats.</summary>
    private sealed class PlayerGameStatsDto
    {
        public Guid GameId { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int TotalPlayed { get; set; }
        public int? BestScore { get; set; }
    }
}
