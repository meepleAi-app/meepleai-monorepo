using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.BoundedContexts.GameToolkit.Domain.Events;
using Api.Middleware.Exceptions;
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

    public Guid? GameId { get; private set; }
    public Guid? PrivateGameId { get; private set; }
    public string Name { get; private set; }
    public int Version { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsPublished { get; private set; }

    // Override flags — when set, the corresponding base session tool is hidden
    public bool OverridesTurnOrder { get; private set; }
    public bool OverridesScoreboard { get; private set; }
    public bool OverridesDiceSet { get; private set; }

    // Tool collections (read-only external access)
    public IReadOnlyList<DiceToolConfig> DiceTools => _diceTools.AsReadOnly();
    public IReadOnlyList<CardToolConfig> CardTools => _cardTools.AsReadOnly();
    public IReadOnlyList<TimerToolConfig> TimerTools => _timerTools.AsReadOnly();
    public IReadOnlyList<CounterToolConfig> CounterTools => _counterTools.AsReadOnly();

    // Template configurations
    public ScoringTemplateConfig? ScoringTemplate { get; private set; }
    public TurnTemplateConfig? TurnTemplate { get; private set; }
    public StateTemplateDefinition? StateTemplate { get; private set; }
    public string? AgentConfig { get; private set; }

    // Template marketplace fields
    public TemplateStatus TemplateStatus { get; private set; } = TemplateStatus.Draft;
    public bool IsTemplate { get; private set; }
    public string? ReviewNotes { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTime? ReviewedAt { get; private set; }

    // Private constructor for EF Core
#pragma warning disable CS8618
    private GameToolkit() : base() { }
#pragma warning restore CS8618

    /// <summary>
    /// Factory constructor - creates a new toolkit linked to either a SharedGame or a PrivateGame.
    /// Exactly one of gameId or privateGameId must be non-empty. Issue #4972.
    /// </summary>
    public GameToolkit(
        Guid id,
        Guid? gameId,
        string name,
        Guid createdByUserId,
        Guid? privateGameId = null,
        bool overridesTurnOrder = false,
        bool overridesScoreboard = false,
        bool overridesDiceSet = false) : base(id)
    {
        bool hasGameId = gameId.HasValue && gameId != Guid.Empty;
        bool hasPrivateGameId = privateGameId.HasValue && privateGameId != Guid.Empty;

        if (hasGameId == hasPrivateGameId)
            throw new ArgumentException(
                "Exactly one of gameId or privateGameId must be provided (mutually exclusive).",
                nameof(gameId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Toolkit name cannot be empty", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Toolkit name cannot exceed 200 characters", nameof(name));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId cannot be empty", nameof(createdByUserId));

        GameId = hasGameId ? gameId : null;
        PrivateGameId = hasPrivateGameId ? privateGameId : null;
        Name = name.Trim();
        Version = 1;
        CreatedByUserId = createdByUserId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
        IsPublished = false;
        OverridesTurnOrder = overridesTurnOrder;
        OverridesScoreboard = overridesScoreboard;
        OverridesDiceSet = overridesDiceSet;

        AddDomainEvent(new ToolkitCreatedEvent(id, GameId, PrivateGameId, Name));
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

    /// <summary>
    /// Updates the override flags that hide base session tools when this custom toolkit is active.
    /// Issue #4972.
    /// </summary>
    public void UpdateOverrideFlags(bool overridesTurnOrder, bool overridesScoreboard, bool overridesDiceSet,
        TimeProvider? timeProvider = null)
    {
        OverridesTurnOrder = overridesTurnOrder;
        OverridesScoreboard = overridesScoreboard;
        OverridesDiceSet = overridesDiceSet;
        UpdatedAt = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime;
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
    // Card tools
    // ========================================================================

    public void AddCardTool(CardToolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (_cardTools.Count >= 20)
            throw new InvalidOperationException("Cannot add more than 20 card tools");

        _cardTools.Add(config);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool RemoveCardTool(string name)
    {
        var tool = _cardTools.Find(c => string.Equals(c.Name, name, StringComparison.Ordinal));
        if (tool == null) return false;

        _cardTools.Remove(tool);
        UpdatedAt = DateTime.UtcNow;
        return true;
    }

    // ========================================================================
    // Timer tools
    // ========================================================================

    public void AddTimerTool(TimerToolConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (_timerTools.Count >= 20)
            throw new InvalidOperationException("Cannot add more than 20 timer tools");

        _timerTools.Add(config);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool RemoveTimerTool(string name)
    {
        var tool = _timerTools.Find(t => string.Equals(t.Name, name, StringComparison.Ordinal));
        if (tool == null) return false;

        _timerTools.Remove(tool);
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

    public void SetStateTemplate(StateTemplateDefinition? stateTemplate)
    {
        StateTemplate = stateTemplate;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetAgentConfig(string? agentConfig)
    {
        AgentConfig = agentConfig;
        UpdatedAt = DateTime.UtcNow;
    }

    // ========================================================================
    // Template marketplace workflow
    // ========================================================================

    public void SubmitForReview()
    {
        if (TemplateStatus != TemplateStatus.Draft && TemplateStatus != TemplateStatus.Rejected)
            throw new ConflictException("Can only submit drafts or rejected templates for review.");
        TemplateStatus = TemplateStatus.PendingReview;
        IsTemplate = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void ApproveTemplate(Guid adminUserId, string? notes = null)
    {
        if (TemplateStatus != TemplateStatus.PendingReview)
            throw new ConflictException("Can only approve templates pending review.");
        TemplateStatus = TemplateStatus.Approved;
        ReviewedByUserId = adminUserId;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RejectTemplate(Guid adminUserId, string notes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notes);
        if (TemplateStatus != TemplateStatus.PendingReview)
            throw new ConflictException("Can only reject templates pending review.");
        TemplateStatus = TemplateStatus.Rejected;
        ReviewedByUserId = adminUserId;
        ReviewedAt = DateTime.UtcNow;
        ReviewNotes = notes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MarkAsTemplate()
    {
        IsTemplate = true;
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

/// <summary>
/// Card tool configuration with deck management.
/// Defines deck structure, available card entries, zones, and allowed operations.
/// </summary>
internal sealed class CardToolConfig
{
    public string Name { get; }
    public string DeckType { get; }
    public int CardCount { get; }
    public bool Shuffleable { get; }
    public CardZone DefaultZone { get; }
    public CardOrientation DefaultOrientation { get; }
    public IReadOnlyList<CardEntry> CardEntries { get; }
    public bool AllowDraw { get; }
    public bool AllowDiscard { get; }
    public bool AllowPeek { get; }
    public bool AllowReturnToDeck { get; }

    public CardToolConfig(
        string name,
        string deckType,
        int cardCount = 52,
        bool shuffleable = true,
        CardZone defaultZone = CardZone.DrawPile,
        CardOrientation defaultOrientation = CardOrientation.FaceDown,
        IReadOnlyList<CardEntry>? cardEntries = null,
        bool allowDraw = true,
        bool allowDiscard = true,
        bool allowPeek = false,
        bool allowReturnToDeck = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Card tool name cannot be empty", nameof(name));
        if (cardCount < 1 || cardCount > 1000)
            throw new ArgumentException("Card count must be between 1 and 1000", nameof(cardCount));

        Name = name.Trim();
        DeckType = deckType ?? "standard";
        CardCount = cardEntries is { Count: > 0 } ? cardEntries.Count : cardCount;
        Shuffleable = shuffleable;
        DefaultZone = defaultZone;
        DefaultOrientation = defaultOrientation;
        CardEntries = cardEntries ?? Array.Empty<CardEntry>();
        AllowDraw = allowDraw;
        AllowDiscard = allowDiscard;
        AllowPeek = allowPeek;
        AllowReturnToDeck = allowReturnToDeck;
    }
}

/// <summary>
/// A single card definition within a deck.
/// </summary>
internal sealed class CardEntry
{
    public string Name { get; }
    public string? Suit { get; }
    public string? Rank { get; }
    public string? CustomData { get; }

    public CardEntry(string name, string? suit = null, string? rank = null, string? customData = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Card entry name cannot be empty", nameof(name));

        Name = name.Trim();
        Suit = suit?.Trim();
        Rank = rank?.Trim();
        CustomData = customData;
    }
}

/// <summary>
/// Timer tool configuration with support for countdown, count-up, and chess-style timers.
/// </summary>
internal sealed class TimerToolConfig
{
    public string Name { get; }
    public int DurationSeconds { get; }
    public TimerType TimerType { get; }
    public bool AutoStart { get; }
    public string? Color { get; }
    public bool IsPerPlayer { get; }
    public int? WarningThresholdSeconds { get; }

    public TimerToolConfig(
        string name,
        int durationSeconds,
        TimerType timerType = TimerType.CountDown,
        bool autoStart = false,
        string? color = null,
        bool isPerPlayer = false,
        int? warningThresholdSeconds = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Timer tool name cannot be empty", nameof(name));
        if (durationSeconds < 1 || durationSeconds > 86400)
            throw new ArgumentException("Timer duration must be between 1 second and 24 hours", nameof(durationSeconds));
        if (warningThresholdSeconds.HasValue && (warningThresholdSeconds.Value < 1 || warningThresholdSeconds.Value >= durationSeconds))
            throw new ArgumentException("Warning threshold must be between 1 and less than duration", nameof(warningThresholdSeconds));
        if (timerType == TimerType.Chess && !isPerPlayer)
            throw new ArgumentException("Chess timer must be per-player", nameof(isPerPlayer));

        Name = name.Trim();
        DurationSeconds = durationSeconds;
        TimerType = timerType;
        AutoStart = autoStart;
        Color = color;
        IsPerPlayer = isPerPlayer;
        WarningThresholdSeconds = warningThresholdSeconds;
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

/// <summary>
/// State template definition - a pre-built toolkit configuration for popular games.
/// Contains a JSON schema defining tools and initial state that can bootstrap a full GameToolkit.
/// </summary>
internal sealed class StateTemplateDefinition
{
    public string Name { get; }
    public string? Description { get; }
    public TemplateCategory Category { get; }
    public string SchemaJson { get; }

    public StateTemplateDefinition(
        string name,
        TemplateCategory category,
        string schemaJson,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name cannot be empty", nameof(name));
        if (name.Length > 200)
            throw new ArgumentException("Template name cannot exceed 200 characters", nameof(name));
        if (string.IsNullOrWhiteSpace(schemaJson))
            throw new ArgumentException("Schema JSON cannot be empty", nameof(schemaJson));

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(schemaJson);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ArgumentException("Schema JSON must be valid JSON", nameof(schemaJson));
        }

        Name = name.Trim();
        Description = description?.Trim();
        Category = category;
        SchemaJson = schemaJson;
    }
}
