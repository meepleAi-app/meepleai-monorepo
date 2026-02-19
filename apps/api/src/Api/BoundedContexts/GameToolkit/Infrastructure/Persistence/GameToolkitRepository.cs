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

        if (!string.IsNullOrEmpty(entity.CardToolsJson))
        {
            var cardConfigs = JsonSerializer.Deserialize<List<CardToolJsonModel>>(entity.CardToolsJson, JsonOptions);
            if (cardConfigs != null)
            {
                var cardList = GetPrivateField<List<CardToolConfig>>(toolkit, "_cardTools");
                foreach (var c in cardConfigs)
                {
                    var entries = c.CardEntries?.Select(e => new CardEntry(e.Name, e.Suit, e.Rank, e.CustomData)).ToList();
                    cardList.Add(new CardToolConfig(
                        c.Name, c.DeckType, c.CardCount, c.Shuffleable,
                        c.DefaultZone, c.DefaultOrientation, entries,
                        c.AllowDraw, c.AllowDiscard, c.AllowPeek, c.AllowReturnToDeck));
                }
            }
        }

        if (!string.IsNullOrEmpty(entity.TimerToolsJson))
        {
            var timerConfigs = JsonSerializer.Deserialize<List<TimerToolJsonModel>>(entity.TimerToolsJson, JsonOptions);
            if (timerConfigs != null)
            {
                var timerList = GetPrivateField<List<TimerToolConfig>>(toolkit, "_timerTools");
                foreach (var t in timerConfigs)
                    timerList.Add(new TimerToolConfig(
                        t.Name, t.DurationSeconds, t.TimerType, t.AutoStart, t.Color,
                        t.IsPerPlayer, t.WarningThresholdSeconds));
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

        if (!string.IsNullOrEmpty(entity.StateTemplate))
        {
            var stateTemplate = JsonSerializer.Deserialize<StateTemplateJsonModel>(entity.StateTemplate, JsonOptions);
            if (stateTemplate != null)
                SetPrivateProperty(toolkit, "StateTemplate", new StateTemplateDefinition(
                    stateTemplate.Name, stateTemplate.Category, stateTemplate.SchemaJson, stateTemplate.Description));
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
            AgentConfig = toolkit.AgentConfig,
            DiceToolsJson = toolkit.DiceTools.Count > 0
                ? JsonSerializer.Serialize(toolkit.DiceTools.Select(d => new DiceToolJsonModel
                {
                    Name = d.Name, DiceType = d.DiceType, Quantity = d.Quantity,
                    CustomFaces = d.CustomFaces, IsInteractive = d.IsInteractive, Color = d.Color
                }).ToList(), JsonOptions)
                : null,
            CardToolsJson = toolkit.CardTools.Count > 0
                ? JsonSerializer.Serialize(toolkit.CardTools.Select(c => new CardToolJsonModel
                {
                    Name = c.Name, DeckType = c.DeckType, CardCount = c.CardCount, Shuffleable = c.Shuffleable,
                    DefaultZone = c.DefaultZone, DefaultOrientation = c.DefaultOrientation,
                    CardEntries = c.CardEntries.Select(e => new CardEntryJsonModel
                    {
                        Name = e.Name, Suit = e.Suit, Rank = e.Rank, CustomData = e.CustomData
                    }).ToList(),
                    AllowDraw = c.AllowDraw, AllowDiscard = c.AllowDiscard,
                    AllowPeek = c.AllowPeek, AllowReturnToDeck = c.AllowReturnToDeck
                }).ToList(), JsonOptions)
                : null,
            TimerToolsJson = toolkit.TimerTools.Count > 0
                ? JsonSerializer.Serialize(toolkit.TimerTools.Select(t => new TimerToolJsonModel
                {
                    Name = t.Name, DurationSeconds = t.DurationSeconds, TimerType = t.TimerType,
                    AutoStart = t.AutoStart, Color = t.Color, IsPerPlayer = t.IsPerPlayer,
                    WarningThresholdSeconds = t.WarningThresholdSeconds
                }).ToList(), JsonOptions)
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
            StateTemplate = toolkit.StateTemplate != null
                ? JsonSerializer.Serialize(new StateTemplateJsonModel
                {
                    Name = toolkit.StateTemplate.Name,
                    Description = toolkit.StateTemplate.Description,
                    Category = toolkit.StateTemplate.Category,
                    SchemaJson = toolkit.StateTemplate.SchemaJson
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

    private sealed class CardToolJsonModel
    {
        public string Name { get; set; } = default!;
        public string DeckType { get; set; } = default!;
        public int CardCount { get; set; }
        public bool Shuffleable { get; set; }
        public CardZone DefaultZone { get; set; }
        public CardOrientation DefaultOrientation { get; set; }
        public List<CardEntryJsonModel>? CardEntries { get; set; }
        public bool AllowDraw { get; set; }
        public bool AllowDiscard { get; set; }
        public bool AllowPeek { get; set; }
        public bool AllowReturnToDeck { get; set; }
    }

    private sealed class CardEntryJsonModel
    {
        public string Name { get; set; } = default!;
        public string? Suit { get; set; }
        public string? Rank { get; set; }
        public string? CustomData { get; set; }
    }

    private sealed class TimerToolJsonModel
    {
        public string Name { get; set; } = default!;
        public int DurationSeconds { get; set; }
        public TimerType TimerType { get; set; }
        public bool AutoStart { get; set; }
        public string? Color { get; set; }
        public bool IsPerPlayer { get; set; }
        public int? WarningThresholdSeconds { get; set; }
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

    private sealed class StateTemplateJsonModel
    {
        public string Name { get; set; } = default!;
        public string? Description { get; set; }
        public TemplateCategory Category { get; set; }
        public string SchemaJson { get; set; } = default!;
    }
}
