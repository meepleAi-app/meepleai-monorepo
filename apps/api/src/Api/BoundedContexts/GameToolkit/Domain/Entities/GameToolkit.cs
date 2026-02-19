using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.GameToolkit.Domain.Entities;

/// <summary>
/// GameToolkit aggregate root - a modular toolbox associated with a game in the catalog.
/// Admin/Editor only creation. Versioned for session immutability.
/// </summary>
internal sealed class GameToolkit : AggregateRoot<Guid>
{
    private readonly List<DiceToolConfig> _diceTools = new();
    private readonly List<CardToolConfig> _cardTools = new();
    private readonly List<TimerToolConfig> _timerTools = new();
    private readonly List<CounterToolConfig> _counterTools = new();

    public Guid GameId { get; private set; }
    public string Name { get; private set; }
    public int Version { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsPublished { get; private set; }

    // Tool collections (read-only external access)
    public IReadOnlyList<DiceToolConfig> DiceTools => _diceTools.AsReadOnly();
    public IReadOnlyList<CardToolConfig> CardTools => _cardTools.AsReadOnly();
    public IReadOnlyList<TimerToolConfig> TimerTools => _timerTools.AsReadOnly();
    public IReadOnlyList<CounterToolConfig> CounterTools => _counterTools.AsReadOnly();

    // Template configurations (stored as JSON)
    public ScoringTemplateConfig? ScoringTemplate { get; private set; }
    public TurnTemplateConfig? TurnTemplate { get; private set; }
    public string? StateTemplate { get; private set; }
    public string? AgentConfig { get; private set; }

    // Private constructor for EF Core
#pragma warning disable CS8618
    private GameToolkit() : base() { }
#pragma warning restore CS8618

    /// <summary>
    /// Factory constructor - creates a new toolkit for a game.
    /// </summary>
    public GameToolkit(
        Guid id,
        Guid gameId,
        string name,
        Guid createdByUserId) : base(id)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty", nameof(gameId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Toolkit name cannot be empty", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Toolkit name cannot exceed 200 characters", nameof(name));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId cannot be empty", nameof(createdByUserId));

        GameId = gameId;
        Name = name.Trim();
        Version = 1;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsPublished = false;

        AddDomainEvent(new ToolkitCreatedEvent(id, gameId, Name));
    }

    // ========================================================================
    // Mutation methods
    // ========================================================================

    public void UpdateDetails(string? name)
    {
        if (name == null) return;

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Toolkit name cannot be empty", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Toolkit name cannot exceed 200 characters", nameof(name));

        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ToolkitUpdatedEvent(Id, Name));
    }

    public void Publish()
    {
        if (IsPublished)
            throw new InvalidOperationException("Toolkit is already published");

        IsPublished = true;
        Version++;
        UpdatedAt = DateTime.UtcNow;
        AddDomainEvent(new ToolkitPublishedEvent(Id, Version));
    }

    // ========================================================================
    // Dice tools
    // ========================================================================

    public void AddDiceTool(DiceToolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (_diceTools.Count >= 20)
            throw new InvalidOperationException("Cannot add more than 20 dice tools");

        _diceTools.Add(config);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool RemoveDiceTool(string name)
    {
        var tool = _diceTools.Find(d => string.Equals(d.Name, name, StringComparison.Ordinal));
        if (tool == null) return false;

        _diceTools.Remove(tool);
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    // ========================================================================
    // Counter tools
    // ========================================================================

    public void AddCounterTool(CounterToolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (_counterTools.Count >= 20)
            throw new InvalidOperationException("Cannot add more than 20 counter tools");

        _counterTools.Add(config);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool RemoveCounterTool(string name)
    {
        var tool = _counterTools.Find(c => string.Equals(c.Name, name, StringComparison.Ordinal));
        if (tool == null) return false;

        _counterTools.Remove(tool);
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    // ========================================================================
    // Templates
    // ========================================================================

    public void SetScoringTemplate(ScoringTemplateConfig template)
    {
        ArgumentNullException.ThrowIfNull(template);
        ScoringTemplate = template;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetTurnTemplate(TurnTemplateConfig template)
    {
        ArgumentNullException.ThrowIfNull(template);
        TurnTemplate = template;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetStateTemplate(string? stateTemplate)
    {
        StateTemplate = stateTemplate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAgentConfig(string? agentConfig)
    {
        AgentConfig = agentConfig;
        UpdatedAt = DateTime.UtcNow;
    }
}

// ============================================================================
// Tool Config Value Objects (embedded in aggregate)
// ============================================================================

/// <summary>Dice tool configuration.</summary>
internal sealed class DiceToolConfig
{
    public string Name { get; }
    public DiceType DiceType { get; }
    public int Quantity { get; }
    public string[]? CustomFaces { get; }
    public bool IsInteractive { get; }
    public string? Color { get; }

    public DiceToolConfig(
        string name,
        DiceType diceType,
        int quantity = 1,
        string[]? customFaces = null,
        bool isInteractive = true,
        string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Dice tool name cannot be empty", nameof(name));
        if (quantity < 1 || quantity > 100)
            throw new ArgumentException("Dice quantity must be between 1 and 100", nameof(quantity));
        if (diceType == DiceType.Custom && (customFaces == null || customFaces.Length == 0))
            throw new ArgumentException("Custom dice must have faces defined", nameof(customFaces));

        Name = name.Trim();
        DiceType = diceType;
        Quantity = quantity;
        CustomFaces = customFaces;
        IsInteractive = isInteractive;
        Color = color;
    }
}

/// <summary>Card tool configuration (placeholder for future card-based tools).</summary>
internal sealed class CardToolConfig
{
    public string Name { get; }
    public string DeckType { get; }
    public int CardCount { get; }
    public bool Shuffleable { get; }

    public CardToolConfig(string name, string deckType, int cardCount = 52, bool shuffleable = true)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Card tool name cannot be empty", nameof(name));
        if (cardCount < 1 || cardCount > 1000)
            throw new ArgumentException("Card count must be between 1 and 1000", nameof(cardCount));

        Name = name.Trim();
        DeckType = deckType ?? "standard";
        CardCount = cardCount;
        Shuffleable = shuffleable;
    }
}

/// <summary>Timer tool configuration.</summary>
internal sealed class TimerToolConfig
{
    public string Name { get; }
    public int DurationSeconds { get; }
    public bool IsCountdown { get; }
    public bool AutoStart { get; }
    public string? Color { get; }

    public TimerToolConfig(
        string name,
        int durationSeconds,
        bool isCountdown = true,
        bool autoStart = false,
        string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Timer tool name cannot be empty", nameof(name));
        if (durationSeconds < 1 || durationSeconds > 86400)
            throw new ArgumentException("Timer duration must be between 1 second and 24 hours", nameof(durationSeconds));

        Name = name.Trim();
        DurationSeconds = durationSeconds;
        IsCountdown = isCountdown;
        AutoStart = autoStart;
        Color = color;
    }
}

/// <summary>Counter tool configuration.</summary>
internal sealed class CounterToolConfig
{
    public string Name { get; }
    public int MinValue { get; }
    public int MaxValue { get; }
    public int DefaultValue { get; }
    public bool IsPerPlayer { get; }
    public string? Icon { get; }
    public string? Color { get; }

    public CounterToolConfig(
        string name,
        int minValue = 0,
        int maxValue = 999,
        int defaultValue = 0,
        bool isPerPlayer = false,
        string? icon = null,
        string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Counter tool name cannot be empty", nameof(name));
        if (minValue > maxValue)
            throw new ArgumentException("MinValue cannot be greater than MaxValue", nameof(minValue));
        if (defaultValue < minValue || defaultValue > maxValue)
            throw new ArgumentException("DefaultValue must be between MinValue and MaxValue", nameof(defaultValue));

        Name = name.Trim();
        MinValue = minValue;
        MaxValue = maxValue;
        DefaultValue = defaultValue;
        IsPerPlayer = isPerPlayer;
        Icon = icon;
        Color = color;
    }
}

// ============================================================================
// Template Config Value Objects
// ============================================================================

/// <summary>Scoring template configuration.</summary>
internal sealed class ScoringTemplateConfig
{
    public string[] Dimensions { get; }
    public string DefaultUnit { get; }
    public ScoreType ScoreType { get; }

    public ScoringTemplateConfig(string[] dimensions, string defaultUnit = "points", ScoreType scoreType = ScoreType.Points)
    {
        if (dimensions == null || dimensions.Length == 0)
            throw new ArgumentException("At least one scoring dimension is required", nameof(dimensions));

        Dimensions = dimensions;
        DefaultUnit = defaultUnit ?? "points";
        ScoreType = scoreType;
    }
}

/// <summary>Turn template configuration.</summary>
internal sealed class TurnTemplateConfig
{
    public TurnOrderType TurnOrderType { get; }
    public string[] Phases { get; }

    public TurnTemplateConfig(TurnOrderType turnOrderType = TurnOrderType.RoundRobin, string[]? phases = null)
    {
        TurnOrderType = turnOrderType;
        Phases = phases ?? Array.Empty<string>();
    }
}
