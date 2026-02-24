namespace Api.BoundedContexts.GameToolkit.Domain.Enums;

/// <summary>
/// Defines the types of widgets available in a Toolkit (Epic B, Issue #5144).
/// </summary>
public enum WidgetType
{
    /// <summary>Dice roller, card draw, random player selection.</summary>
    RandomGenerator,

    /// <summary>Turn order, round counter, per-turn timer.</summary>
    TurnManager,

    /// <summary>Multi-player, multi-dimension score tracking.</summary>
    ScoreTracker,

    /// <summary>Resource counters (meeple, tokens, deck tracker).</summary>
    ResourceManager,

    /// <summary>Private and public notes per player.</summary>
    NoteManager,

    /// <summary>Collaborative free-draw canvas.</summary>
    Whiteboard,
}
