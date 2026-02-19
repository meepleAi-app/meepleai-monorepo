using System.Text.Json;
using Api.BoundedContexts.GameToolkit.Domain.Entities;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameToolkit;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameToolkit.Infrastructure.Persistence;

internal class GameToolkitRepository : RepositoryBase, IGameToolkitRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public GameToolkitRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<Domain.Entities.GameToolkit?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<GameToolkitEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<Domain.Entities.GameToolkit>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<GameToolkitEntity>()
            .AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Domain.Entities.GameToolkit>> GetByGameIdAsync(
        Guid gameId, CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<GameToolkitEntity>()
            .AsNoTracking()
            .Where(t => t.GameId == gameId)
            .OrderByDescending(t => t.Version)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Domain.Entities.GameToolkit>> GetPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<GameToolkitEntity>()
            .AsNoTracking()
            .Where(t => t.IsPublished)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(Domain.Entities.GameToolkit toolkit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        CollectDomainEvents(toolkit);
        var entity = MapToPersistence(toolkit);
        await DbContext.Set<GameToolkitEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public Task UpdateAsync(Domain.Entities.GameToolkit toolkit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        CollectDomainEvents(toolkit);
        var entity = MapToPersistence(toolkit);
        DbContext.Set<GameToolkitEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Domain.Entities.GameToolkit toolkit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(toolkit);
        var entity = MapToPersistence(toolkit);
        DbContext.Set<GameToolkitEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<GameToolkitEntity>()
            .AsNoTracking()
            .AnyAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    // ========================================================================
    // Mapping
    // ========================================================================

    private static Domain.Entities.GameToolkit MapToDomain(GameToolkitEntity entity)
    {
        // Use GetUninitializedObject to bypass the factory constructor entirely,
        // avoiding spurious ToolkitCreatedEvent during reconstitution (DDD best practice).
        var toolkit = (Domain.Entities.GameToolkit)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(Domain.Entities.GameToolkit));

        // Restore scalar properties via reflection
        SetPrivateProperty(toolkit, "Id", entity.Id);
        SetPrivateProperty(toolkit, "GameId", entity.GameId);
        SetPrivateProperty(toolkit, "Name", entity.Name);
        SetPrivateProperty(toolkit, "CreatedByUserId", entity.CreatedByUserId);
        SetPrivateProperty(toolkit, "Version", entity.Version);
        SetPrivateProperty(toolkit, "IsPublished", entity.IsPublished);
        SetPrivateProperty(toolkit, "CreatedAt", entity.CreatedAt);
        SetPrivateProperty(toolkit, "UpdatedAt", entity.UpdatedAt);
        SetPrivateProperty(toolkit, "StateTemplate", entity.StateTemplate);
        SetPrivateProperty(toolkit, "AgentConfig", entity.AgentConfig);

        // Initialize backing fields for collections (GetUninitializedObject leaves them null)
        // Base class AggregateRoot<Guid> needs _domainEvents initialized for ClearDomainEvents/AddDomainEvent
        SetPrivateField(toolkit, "_domainEvents", new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>());
        SetPrivateField(toolkit, "_diceTools", new List<DiceToolConfig>());
        SetPrivateField(toolkit, "_cardTools", new List<CardToolConfig>());
        SetPrivateField(toolkit, "_timerTools", new List<TimerToolConfig>());
        SetPrivateField(toolkit, "_counterTools", new List<CounterToolConfig>());

        // Deserialize JSONB tool configs directly into backing lists
        if (!string.IsNullOrEmpty(entity.DiceToolsJson))
        {
            var diceConfigs = JsonSerializer.Deserialize<List<DiceToolJsonModel>>(entity.DiceToolsJson, JsonOptions);
            if (diceConfigs != null)
            {
                var diceList = GetPrivateField<List<DiceToolConfig>>(toolkit, "_diceTools");
                foreach (var d in diceConfigs)
                    diceList.Add(new DiceToolConfig(d.Name, d.DiceType, d.Quantity, d.CustomFaces, d.IsInteractive, d.Color));
            }
        }

        if (!string.IsNullOrEmpty(entity.CounterToolsJson))
        {
            var counterConfigs = JsonSerializer.Deserialize<List<CounterToolJsonModel>>(entity.CounterToolsJson, JsonOptions);
            if (counterConfigs != null)
            {
                var counterList = GetPrivateField<List<CounterToolConfig>>(toolkit, "_counterTools");
                foreach (var c in counterConfigs)
                    counterList.Add(new CounterToolConfig(c.Name, c.MinValue, c.MaxValue, c.DefaultValue, c.IsPerPlayer, c.Icon, c.Color));
            }
        }

        if (!string.IsNullOrEmpty(entity.ScoringTemplateJson))
        {
            var scoring = JsonSerializer.Deserialize<ScoringTemplateJsonModel>(entity.ScoringTemplateJson, JsonOptions);
            if (scoring != null)
                SetPrivateProperty(toolkit, "ScoringTemplate", new ScoringTemplateConfig(scoring.Dimensions, scoring.DefaultUnit, scoring.ScoreType));
        }

        if (!string.IsNullOrEmpty(entity.TurnTemplateJson))
        {
            var turn = JsonSerializer.Deserialize<TurnTemplateJsonModel>(entity.TurnTemplateJson, JsonOptions);
            if (turn != null)
                SetPrivateProperty(toolkit, "TurnTemplate", new TurnTemplateConfig(turn.TurnOrderType, turn.Phases));
        }

        return toolkit;
    }

    private static GameToolkitEntity MapToPersistence(Domain.Entities.GameToolkit toolkit)
    {
        return new GameToolkitEntity
        {
            Id = toolkit.Id,
            GameId = toolkit.GameId,
            Name = toolkit.Name,
            Version = toolkit.Version,
            CreatedByUserId = toolkit.CreatedByUserId,
            IsPublished = toolkit.IsPublished,
            CreatedAt = toolkit.CreatedAt,
            UpdatedAt = toolkit.UpdatedAt,
            StateTemplate = toolkit.StateTemplate,
            AgentConfig = toolkit.AgentConfig,
            DiceToolsJson = toolkit.DiceTools.Count > 0
                ? JsonSerializer.Serialize(toolkit.DiceTools.Select(d => new DiceToolJsonModel
                {
                    Name = d.Name, DiceType = d.DiceType, Quantity = d.Quantity,
                    CustomFaces = d.CustomFaces, IsInteractive = d.IsInteractive, Color = d.Color
                }).ToList(), JsonOptions)
                : null,
            CardToolsJson = toolkit.CardTools.Count > 0
                ? JsonSerializer.Serialize(toolkit.CardTools.Select(c => new { c.Name, c.DeckType, c.CardCount, c.Shuffleable }).ToList(), JsonOptions)
                : null,
            TimerToolsJson = toolkit.TimerTools.Count > 0
                ? JsonSerializer.Serialize(toolkit.TimerTools.Select(t => new { t.Name, t.DurationSeconds, t.IsCountdown, t.AutoStart, t.Color }).ToList(), JsonOptions)
                : null,
            CounterToolsJson = toolkit.CounterTools.Count > 0
                ? JsonSerializer.Serialize(toolkit.CounterTools.Select(c => new CounterToolJsonModel
                {
                    Name = c.Name, MinValue = c.MinValue, MaxValue = c.MaxValue,
                    DefaultValue = c.DefaultValue, IsPerPlayer = c.IsPerPlayer, Icon = c.Icon, Color = c.Color
                }).ToList(), JsonOptions)
                : null,
            ScoringTemplateJson = toolkit.ScoringTemplate != null
                ? JsonSerializer.Serialize(new ScoringTemplateJsonModel
                {
                    Dimensions = toolkit.ScoringTemplate.Dimensions,
                    DefaultUnit = toolkit.ScoringTemplate.DefaultUnit,
                    ScoreType = toolkit.ScoringTemplate.ScoreType
                }, JsonOptions)
                : null,
            TurnTemplateJson = toolkit.TurnTemplate != null
                ? JsonSerializer.Serialize(new TurnTemplateJsonModel
                {
                    TurnOrderType = toolkit.TurnTemplate.TurnOrderType,
                    Phases = toolkit.TurnTemplate.Phases
                }, JsonOptions)
                : null,
        };
    }

    // Reflection helpers for DDD aggregate reconstitution from persistence layer.
    // S3011 suppressed: accessibility bypass is intentional for restoring aggregate state
    // without invoking the factory constructor (which raises domain events).
#pragma warning disable S3011
    private static void SetPrivateProperty(object obj, string propertyName, object? value)
    {
        var type = obj.GetType();
        var prop = type.GetProperty(propertyName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Property '{propertyName}' not found on type '{type.Name}'");
        prop.SetValue(obj, value);
    }

    private static System.Reflection.FieldInfo FindField(Type type, string fieldName)
    {
        var current = type;
        while (current != null)
        {
            var field = current.GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (field != null) return field;
            current = current.BaseType;
        }
        throw new InvalidOperationException($"Field '{fieldName}' not found on type '{type.Name}' or its base types");
    }

    private static void SetPrivateField(object obj, string fieldName, object? value)
    {
        FindField(obj.GetType(), fieldName).SetValue(obj, value);
    }

    private static T GetPrivateField<T>(object obj, string fieldName) where T : class
    {
        var field = FindField(obj.GetType(), fieldName);
        return (T)(field.GetValue(obj) ?? throw new InvalidOperationException($"Field '{fieldName}' returned null"));
    }
#pragma warning restore S3011

    // JSON models for serialization
    private sealed class DiceToolJsonModel
    {
        public string Name { get; set; } = default!;
        public DiceType DiceType { get; set; }
        public int Quantity { get; set; }
        public string[]? CustomFaces { get; set; }
        public bool IsInteractive { get; set; }
        public string? Color { get; set; }
    }

    private sealed class CounterToolJsonModel
    {
        public string Name { get; set; } = default!;
        public int MinValue { get; set; }
        public int MaxValue { get; set; }
        public int DefaultValue { get; set; }
        public bool IsPerPlayer { get; set; }
        public string? Icon { get; set; }
        public string? Color { get; set; }
    }

    private sealed class ScoringTemplateJsonModel
    {
        public string[] Dimensions { get; set; } = default!;
        public string DefaultUnit { get; set; } = default!;
        public ScoreType ScoreType { get; set; }
    }

    private sealed class TurnTemplateJsonModel
    {
        public TurnOrderType TurnOrderType { get; set; }
        public string[] Phases { get; set; } = default!;
    }
}
