namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing the classification of game rules for Arbitro agent.
/// </summary>
/// <remarks>
/// RuleType determines how rules are prioritized and applied during move validation.
/// Issue #3759: Rules Arbitration Engine
/// </remarks>
internal sealed record RuleType
{
    /// <summary>
    /// Movement rules: How pieces can move on the board.
    /// </summary>
    public static RuleType Movement => new("Movement", "Piece movement and positioning rules", precedence: 10);

    /// <summary>
    /// Capture rules: How pieces can capture opponents.
    /// </summary>
    public static RuleType Capture => new("Capture", "Piece capture mechanics and conditions", precedence: 20);

    /// <summary>
    /// Special move rules: Castling, en passant, promotion, etc.
    /// </summary>
    public static RuleType SpecialMove => new("SpecialMove", "Special move mechanics (castling, promotion, etc.)", precedence: 30);

    /// <summary>
    /// Turn order rules: Turn progression and phase transitions.
    /// </summary>
    public static RuleType TurnOrder => new("TurnOrder", "Turn sequence and phase progression", precedence: 5);

    /// <summary>
    /// Game end conditions: Win, loss, draw, stalemate.
    /// </summary>
    public static RuleType GameEnd => new("GameEnd", "Victory and termination conditions", precedence: 50);

    /// <summary>
    /// Setup rules: Initial board configuration.
    /// </summary>
    public static RuleType Setup => new("Setup", "Initial game state and piece placement", precedence: 1);

    /// <summary>
    /// Constraint rules: Global constraints (e.g., check, illegal positions).
    /// </summary>
    public static RuleType Constraint => new("Constraint", "Global game constraints and illegal states", precedence: 40);

    public string Value { get; }
    public string Description { get; }
    public int Precedence { get; }

    private RuleType(string value, string description, int precedence)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("RuleType value cannot be empty", nameof(value));

        Value = value;
        Description = description;
        Precedence = precedence;
    }

    /// <summary>
    /// Parses string to RuleType. Returns null if not recognized.
    /// </summary>
    public static RuleType? FromString(string value)
    {
        return value?.Trim() switch
        {
            "Movement" => Movement,
            "Capture" => Capture,
            "SpecialMove" => SpecialMove,
            "TurnOrder" => TurnOrder,
            "GameEnd" => GameEnd,
            "Setup" => Setup,
            "Constraint" => Constraint,
            _ => null
        };
    }

    public static IEnumerable<RuleType> All()
    {
        yield return Setup;
        yield return TurnOrder;
        yield return Movement;
        yield return Capture;
        yield return SpecialMove;
        yield return Constraint;
        yield return GameEnd;
    }
}
