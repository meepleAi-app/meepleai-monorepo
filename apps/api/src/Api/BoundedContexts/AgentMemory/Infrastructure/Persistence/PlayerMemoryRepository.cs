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

    public async Task<IReadOnlyList<PlayerMemory>> GetAllByGuestNameAsync(string guestName, CancellationToken ct = default)
    {
        var entities = await DbContext.PlayerMemories
            .AsNoTracking()
            .Where(e => e.GuestName != null
                && EF.Functions.ILike(e.GuestName!, guestName.Replace("%", "\\%").Replace("_", "\\_"))
                && e.UserId == null)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<PlayerMemory>> GetAllByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var entities = await DbContext.PlayerMemories
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
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
    /// <remarks>
    /// Passa da <see cref="PlayerMemory.Reconstitute"/>, non dalle factory di creazione (#4552): quelle conoscono
    /// un solo lato fra utente e ospite, e la memoria di un ospite reclamato perdeva il nome dell'ospite, che
    /// <see cref="UpdateAsync"/> scriveva poi a NULL. Ogni colonna scritta da <see cref="MapToPersistence"/> torna
    /// qui (vedi <c>PlayerMemoryRepositoryTests</c>).
    /// </remarks>
    private static PlayerMemory MapToDomain(PlayerMemoryEntity entity)
    {
        var gameStats = entity.GameStatsJson is null
            ? []
            : JsonSerializer.Deserialize<List<PlayerGameStatsDto>>(entity.GameStatsJson) ?? [];

        return PlayerMemory.Reconstitute(
            id: entity.Id,
            userId: entity.UserId,
            guestName: entity.GuestName,
            groupId: entity.GroupId,
            claimedAt: entity.ClaimedAt,
            createdAt: entity.CreatedAt,
            lastProcessedEventId: entity.LastProcessedEventId,
            gameStats: gameStats.Select(s => PlayerGameStats.Restore(s.GameId, s.Wins, s.Losses, s.TotalPlayed, s.BestScore)));
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
            LastProcessedEventId = memory.LastProcessedEventId,
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
