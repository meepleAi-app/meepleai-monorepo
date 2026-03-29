using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Models;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.AgentMemory.Infrastructure.Entities;
using Api.BoundedContexts.GameManagement.Domain.Models;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Api.BoundedContexts.AgentMemory.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IGameMemoryRepository.
/// Maps between domain GameMemory aggregate and GameMemoryEntity persistence model.
/// Serializes/deserializes JSONB fields for house rules, custom setup, and notes.
/// </summary>
internal sealed class GameMemoryRepository : RepositoryBase, IGameMemoryRepository
{
    public GameMemoryRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<GameMemory?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.GameMemories
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<GameMemory?> GetByGameAndOwnerAsync(Guid gameId, Guid ownerId, CancellationToken ct = default)
    {
        var entity = await DbContext.GameMemories
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.GameId == gameId && e.OwnerId == ownerId, ct)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(GameMemory memory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        CollectDomainEvents(memory);

        var entity = MapToPersistence(memory);
        await DbContext.GameMemories.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(GameMemory memory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        CollectDomainEvents(memory);

        var entity = MapToPersistence(memory);
        DbContext.GameMemories.Update(entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps persistence entity to domain aggregate.
    /// </summary>
    private static GameMemory MapToDomain(GameMemoryEntity entity)
    {
        var memory = GameMemory.Create(entity.GameId, entity.OwnerId);

        // Restore Id and CreatedAt via reflection (factory generates new ones)
        SetPrivateProperty(memory, nameof(GameMemory.Id), entity.Id);
        SetPrivateProperty(memory, nameof(GameMemory.CreatedAt), entity.CreatedAt);

        // Restore house rules from JSONB
        if (entity.HouseRulesJson != null)
        {
            var houseRules = JsonSerializer.Deserialize<List<HouseRuleDto>>(entity.HouseRulesJson);
            if (houseRules != null)
            {
#pragma warning disable S3011 // Reflection needed for domain reconstruction from persistence
                var rules = (List<HouseRule>)typeof(GameMemory)
                    .GetField("_houseRules", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .GetValue(memory)!;
#pragma warning restore S3011

                foreach (var rule in houseRules)
                {
                    rules.Add(HouseRule.Restore(rule.Description, rule.AddedAt, (HouseRuleSource)rule.Source));
                }
            }
        }

        // Restore custom setup from JSONB
        if (entity.CustomSetupJson != null)
        {
            var setup = JsonSerializer.Deserialize<SetupChecklistData>(entity.CustomSetupJson);
            if (setup != null)
            {
                memory.SetCustomSetup(setup);
            }
        }

        // Restore notes from JSONB
        if (entity.NotesJson != null)
        {
            var notes = JsonSerializer.Deserialize<List<MemoryNoteDto>>(entity.NotesJson);
            if (notes != null)
            {
#pragma warning disable S3011 // Reflection needed for domain reconstruction from persistence
                var notesList = (List<MemoryNote>)typeof(GameMemory)
                    .GetField("_notes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .GetValue(memory)!;
#pragma warning restore S3011

                foreach (var note in notes)
                {
                    notesList.Add(MemoryNote.Restore(note.Content, note.AddedAt, note.AddedByUserId));
                }
            }
        }

        return memory;
    }

    /// <summary>
    /// Maps domain aggregate to persistence entity.
    /// </summary>
    private static GameMemoryEntity MapToPersistence(GameMemory memory)
    {
        var entity = new GameMemoryEntity
        {
            Id = memory.Id,
            GameId = memory.GameId,
            OwnerId = memory.OwnerId,
            CreatedAt = memory.CreatedAt,
        };

        // Serialize house rules to JSONB
        if (memory.HouseRules.Count > 0)
        {
            var dtos = memory.HouseRules.Select(r => new HouseRuleDto
            {
                Description = r.Description,
                AddedAt = r.AddedAt,
                Source = (int)r.Source,
            }).ToList();
            entity.HouseRulesJson = JsonSerializer.Serialize(dtos);
        }

        // Serialize custom setup to JSONB
        if (memory.CustomSetup != null)
        {
            entity.CustomSetupJson = JsonSerializer.Serialize(memory.CustomSetup);
        }

        // Serialize notes to JSONB
        if (memory.Notes.Count > 0)
        {
            var dtos = memory.Notes.Select(n => new MemoryNoteDto
            {
                Content = n.Content,
                AddedAt = n.AddedAt,
                AddedByUserId = n.AddedByUserId,
            }).ToList();
            entity.NotesJson = JsonSerializer.Serialize(dtos);
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

    /// <summary>DTO for JSON serialization of HouseRule.</summary>
    private sealed class HouseRuleDto
    {
        public string Description { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
        public int Source { get; set; }
    }

    /// <summary>DTO for JSON serialization of MemoryNote.</summary>
    private sealed class MemoryNoteDto
    {
        public string Content { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; }
        public Guid? AddedByUserId { get; set; }
    }
}
