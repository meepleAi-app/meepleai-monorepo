using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
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
/// EF Core implementation of IGroupMemoryRepository.
/// Maps between domain GroupMemory aggregate and GroupMemoryEntity persistence model.
/// Serializes/deserializes JSONB fields for members, preferences, and stats.
/// </summary>
internal sealed class GroupMemoryRepository : RepositoryBase, IGroupMemoryRepository
{
    public GroupMemoryRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<GroupMemory?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await DbContext.GroupMemories
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<GroupMemory>> GetByCreatorIdAsync(Guid creatorId, CancellationToken ct = default)
    {
        var entities = await DbContext.GroupMemories
            .AsNoTracking()
            .Where(e => e.CreatorId == creatorId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(GroupMemory memory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        CollectDomainEvents(memory);

        var entity = MapToPersistence(memory);
        await DbContext.GroupMemories.AddAsync(entity, ct).ConfigureAwait(false);
    }

    public Task UpdateAsync(GroupMemory memory, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(memory);
        CollectDomainEvents(memory);

        var entity = MapToPersistence(memory);
        DbContext.GroupMemories.Update(entity);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Maps persistence entity to domain aggregate.
    /// </summary>
    private static GroupMemory MapToDomain(GroupMemoryEntity entity)
    {
        var memory = GroupMemory.Create(entity.CreatorId, entity.Name);

        // Restore Id and CreatedAt via reflection (factory generates new ones)
        SetPrivateProperty(memory, nameof(GroupMemory.Id), entity.Id);
        SetPrivateProperty(memory, nameof(GroupMemory.CreatedAt), entity.CreatedAt);

        // Restore members from JSONB
        if (entity.MembersJson != null)
        {
            var members = JsonSerializer.Deserialize<List<GroupMemberDto>>(entity.MembersJson);
            if (members != null)
            {
#pragma warning disable S3011 // Reflection needed for domain reconstruction from persistence
                var membersList = (List<GroupMember>)typeof(GroupMemory)
                    .GetField("_members", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .GetValue(memory)!;
#pragma warning restore S3011

                foreach (var member in members)
                {
                    if (member.UserId.HasValue)
                    {
                        memory.AddMember(member.UserId.Value);
                    }
                    else if (member.GuestName != null)
                    {
                        memory.AddGuestMember(member.GuestName);
                    }

                    membersList[^1].JoinedAt = member.JoinedAt;
                }
            }
        }

        // Restore preferences from JSONB
        if (entity.PreferencesJson != null)
        {
            var prefs = JsonSerializer.Deserialize<GroupPreferencesDto>(entity.PreferencesJson);
            if (prefs != null)
            {
                var preferences = new GroupPreferences
                {
                    MaxDuration = prefs.MaxDurationTicks.HasValue ? TimeSpan.FromTicks(prefs.MaxDurationTicks.Value) : null,
                    PreferredComplexity = prefs.PreferredComplexity.HasValue ? (PreferredComplexity)prefs.PreferredComplexity.Value : null,
                    CustomNotes = prefs.CustomNotes,
                };
                memory.UpdatePreferences(preferences);
            }
        }

        // Restore stats from JSONB
        if (entity.StatsJson != null)
        {
            var stats = JsonSerializer.Deserialize<GroupStats>(entity.StatsJson);
            if (stats != null)
            {
                memory.UpdateStats(stats);
            }
        }

        return memory;
    }

    /// <summary>
    /// Maps domain aggregate to persistence entity.
    /// </summary>
    private static GroupMemoryEntity MapToPersistence(GroupMemory memory)
    {
        var entity = new GroupMemoryEntity
        {
            Id = memory.Id,
            Name = memory.Name,
            CreatorId = memory.CreatorId,
            CreatedAt = memory.CreatedAt,
        };

        // Serialize members to JSONB
        if (memory.Members.Count > 0)
        {
            var dtos = memory.Members.Select(m => new GroupMemberDto
            {
                UserId = m.UserId,
                GuestName = m.GuestName,
                JoinedAt = m.JoinedAt,
            }).ToList();
            entity.MembersJson = JsonSerializer.Serialize(dtos);
        }

        // Serialize preferences to JSONB
        if (memory.Preferences != null)
        {
            var dto = new GroupPreferencesDto
            {
                MaxDurationTicks = memory.Preferences.MaxDuration?.Ticks,
                PreferredComplexity = memory.Preferences.PreferredComplexity.HasValue
                    ? (int)memory.Preferences.PreferredComplexity.Value
                    : null,
                CustomNotes = memory.Preferences.CustomNotes,
            };
            entity.PreferencesJson = JsonSerializer.Serialize(dto);
        }

        // Serialize stats to JSONB
        if (memory.Stats != null)
        {
            entity.StatsJson = JsonSerializer.Serialize(memory.Stats);
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

    /// <summary>DTO for JSON serialization of GroupMember.</summary>
    private sealed class GroupMemberDto
    {
        public Guid? UserId { get; set; }
        public string? GuestName { get; set; }
        public DateTime JoinedAt { get; set; }
    }

    /// <summary>DTO for JSON serialization of GroupPreferences.</summary>
    private sealed class GroupPreferencesDto
    {
        public long? MaxDurationTicks { get; set; }
        public int? PreferredComplexity { get; set; }
        public string? CustomNotes { get; set; }
    }
}
