namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing heuristic evaluation score for a chess move.
/// Issue #3770: Breaks down move quality into components for decision making.
/// </summary>
public sealed record MoveScore
{
    public required double Material { get; init; }      // -9 to +9 (capture value)
    public required double Positional { get; init; }    // -1 to +1 (position improvement)
    public required double Tactical { get; init; }      // -1 to +1 (threats, checks, pins)
    public required double Development { get; init; }   // 0 to +0.5 (opening development)
    public required double Overall { get; init; }       // Weighted sum

    private MoveScore() { }

    /// <summary>
    /// Creates a move score with component validation.
    /// </summary>
    public static MoveScore Create(double material, double positional, double tactical, double development)
    {
        // Material: -9 (lose Queen for Pawn) to +9 (Queen takes Pawn)
        if (material < -9.0 || material > 9.0)
            throw new ArgumentException("Material must be between -9 and +9", nameof(material));

        // Positional: normalized to -1 (bad position) to +1 (good position)
        if (positional < -1.0 || positional > 1.0)
            throw new ArgumentException("Positional must be between -1 and +1", nameof(positional));

        // Tactical: normalized to -1 (bad tactics) to +1 (good tactics)
        if (tactical < -1.0 || tactical > 1.0)
            throw new ArgumentException("Tactical must be between -1 and +1", nameof(tactical));

        // Development: 0 (no development) to +0.5 (strong development)
        if (development < 0.0 || development > 0.5)
            throw new ArgumentException("Development must be between 0 and 0.5", nameof(development));

        // Overall: weighted sum
        var overall = (0.5 * material) + (0.2 * positional) + (0.2 * tactical) + (0.1 * development);

        return new MoveScore
        {
            Material = material,
            Positional = positional,
            Tactical = tactical,
            Development = development,
            Overall = overall
        };
    }

    /// <summary>
    /// Zero score (neutral move).
    /// </summary>
    public static MoveScore Zero() => Create(0, 0, 0, 0);

    /// <summary>
    /// Formats score for display.
    /// </summary>
    public override string ToString() =>
        $"Overall: {Overall:F2} (Mat: {Material:F1}, Pos: {Positional:F2}, Tac: {Tactical:F2}, Dev: {Development:F2})";
}
