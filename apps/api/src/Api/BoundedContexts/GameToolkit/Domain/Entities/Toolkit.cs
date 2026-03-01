using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.GameToolkit.Domain.Entities;

/// <summary>
/// Aggregate root representing a user's widget dashboard for a specific game.
///
/// Business Rules:
/// - BR-01: A default Toolkit (OwnerUserId=null, IsDefault=true) is auto-created when a game
///          is added to a user's library. All 6 widgets are enabled by default.
/// - BR-02: The default Toolkit is read-only. To customise, call Override(userId) to clone it
///          into a user-specific Toolkit.
/// - BR-03: There is at most one active Toolkit per (GameId, OwnerUserId) pair.
///
/// Issue #5144 — Epic B1.
/// </summary>
public sealed class Toolkit
{
    private readonly List<ToolkitWidget> _widgets = [];

    public Guid Id { get; private set; }
    public Guid GameId { get; private set; }

    /// <summary>Null for the shared default Toolkit; set to the user ID for per-user overrides.</summary>
    public Guid? OwnerUserId { get; private set; }

    /// <summary>True for the shared default (BR-02). False for per-user clones.</summary>
    public bool IsDefault { get; private set; }

    public string? DisplayName { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<ToolkitWidget> Widgets => _widgets.AsReadOnly();

    // EF Core parameterless constructor
    private Toolkit() { }

    // Canonical display order for all 6 widget types
    private static readonly WidgetType[] AllWidgetTypes =
    [
        WidgetType.RandomGenerator,
        WidgetType.TurnManager,
        WidgetType.ScoreTracker,
        WidgetType.ResourceManager,
        WidgetType.NoteManager,
        WidgetType.Whiteboard,
    ];

    // ========================================================================
    // Factory methods
    // ========================================================================

    /// <summary>
    /// Creates the default, game-level Toolkit with all 6 widgets enabled (BR-01).
    /// </summary>
    public static Toolkit CreateDefault(Guid gameId)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("GameId cannot be empty.", nameof(gameId));

        var toolkit = new Toolkit
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            OwnerUserId = null,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        for (int i = 0; i < AllWidgetTypes.Length; i++)
        {
            toolkit._widgets.Add(ToolkitWidget.Create(toolkit.Id, AllWidgetTypes[i], displayOrder: i, isEnabled: true));
        }

        return toolkit;
    }

    /// <summary>
    /// Creates a user-specific clone of this Toolkit (BR-02).
    /// Only callable on the default Toolkit.
    /// </summary>
    public Toolkit Override(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty.", nameof(userId));

        if (!IsDefault)
            throw new ConflictException("Only the default Toolkit can be overridden.");

        var clone = new Toolkit
        {
            Id = Guid.NewGuid(),
            GameId = GameId,
            OwnerUserId = userId,
            IsDefault = false,
            DisplayName = DisplayName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        foreach (var src in _widgets)
        {
            clone._widgets.Add(ToolkitWidget.Create(clone.Id, src.Type, src.DisplayOrder, src.IsEnabled));
        }

        return clone;
    }

    // ========================================================================
    // Mutation methods
    // ========================================================================

    public void EnableWidget(WidgetType type)
    {
        var widget = _widgets.Find(w => w.Type == type)
            ?? throw new NotFoundException("ToolkitWidget", type.ToString());

        widget.Enable();
        UpdatedAt = DateTime.UtcNow;
    }

    public void DisableWidget(WidgetType type)
    {
        var widget = _widgets.Find(w => w.Type == type)
            ?? throw new NotFoundException("ToolkitWidget", type.ToString());

        widget.Disable();
        UpdatedAt = DateTime.UtcNow;
    }

    public void ConfigureWidget(WidgetType type, string configJson)
    {
        if (string.IsNullOrWhiteSpace(configJson))
            throw new ArgumentException("Config JSON cannot be empty.", nameof(configJson));

        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(configJson);
        }
        catch (System.Text.Json.JsonException)
        {
            throw new ArgumentException("Config must be valid JSON.", nameof(configJson));
        }

        var widget = _widgets.Find(w => w.Type == type)
            ?? throw new NotFoundException("ToolkitWidget", type.ToString());

        widget.UpdateConfig(configJson);
        UpdatedAt = DateTime.UtcNow;
    }

    public void Rename(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName cannot be empty.", nameof(displayName));

        DisplayName = displayName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
