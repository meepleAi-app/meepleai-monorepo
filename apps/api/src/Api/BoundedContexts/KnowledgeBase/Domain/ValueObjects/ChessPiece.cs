namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing a chess piece with type, color, and position.
/// Issue #3772: Used by Game State Parser for Decisore Agent.
/// </summary>
public sealed record ChessPiece
{
    /// <summary>
    /// Piece type (Pawn, Knight, Bishop, Rook, Queen, King).
    /// </summary>
    public string Type { get; init; }

    /// <summary>
    /// Piece color (White, Black).
    /// </summary>
    public string Color { get; init; }

    /// <summary>
    /// Current position in algebraic notation (e.g., "e4", "a1").
    /// </summary>
    public string Position { get; init; }

    /// <summary>
    /// FEN notation character for this piece.
    /// </summary>
    public char FenChar { get; init; }

    private ChessPiece(string type, string color, string position, char fenChar)
    {
        Type = type;
        Color = color;
        Position = position;
        FenChar = fenChar;
    }

    /// <summary>
    /// Creates a chess piece with validation.
    /// </summary>
    public static ChessPiece Create(string type, string color, string position)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Piece type cannot be empty", nameof(type));

        if (string.IsNullOrWhiteSpace(color))
            throw new ArgumentException("Piece color cannot be empty", nameof(color));

        if (string.IsNullOrWhiteSpace(position))
            throw new ArgumentException("Position cannot be empty", nameof(position));

        var validTypes = new[] { "Pawn", "Knight", "Bishop", "Rook", "Queen", "King" };
        if (!validTypes.Contains(type, StringComparer.Ordinal))
            throw new ArgumentException($"Invalid piece type: {type}", nameof(type));

        var validColors = new[] { "White", "Black" };
        if (!validColors.Contains(color, StringComparer.Ordinal))
            throw new ArgumentException($"Invalid color: {color}", nameof(color));

        // Validate position format (a-h)(1-8)
        if (position.Length != 2 ||
            position[0] < 'a' || position[0] > 'h' ||
            position[1] < '1' || position[1] > '8')
            throw new ArgumentException($"Invalid position: {position}", nameof(position));

        var fenChar = GetFenChar(type, color);

        return new ChessPiece(type, color, position, fenChar);
    }

    /// <summary>
    /// Creates a piece from FEN character.
    /// </summary>
    public static ChessPiece FromFen(char fenChar, string position)
    {
        var (type, color) = fenChar switch
        {
            'P' => ("Pawn", "White"),
            'N' => ("Knight", "White"),
            'B' => ("Bishop", "White"),
            'R' => ("Rook", "White"),
            'Q' => ("Queen", "White"),
            'K' => ("King", "White"),
            'p' => ("Pawn", "Black"),
            'n' => ("Knight", "Black"),
            'b' => ("Bishop", "Black"),
            'r' => ("Rook", "Black"),
            'q' => ("Queen", "Black"),
            'k' => ("King", "Black"),
            _ => throw new ArgumentException($"Invalid FEN character: {fenChar}", nameof(fenChar))
        };

        return new ChessPiece(type, color, position, fenChar);
    }

    private static char GetFenChar(string type, string color)
    {
        var baseChar = type switch
        {
            "Pawn" => 'P',
            "Knight" => 'N',
            "Bishop" => 'B',
            "Rook" => 'R',
            "Queen" => 'Q',
            "King" => 'K',
            _ => throw new ArgumentException($"Unknown piece type: {type}", nameof(type))
        };

        return string.Equals(color, "White", StringComparison.Ordinal) ? baseChar : char.ToLowerInvariant(baseChar);
    }

    /// <summary>
    /// Gets material value for scoring (Pawn=1, Knight=3, Bishop=3, Rook=5, Queen=9, King=0).
    /// </summary>
    public int GetMaterialValue() => Type switch
    {
        "Pawn" => 1,
        "Knight" => 3,
        "Bishop" => 3,
        "Rook" => 5,
        "Queen" => 9,
        "King" => 0,
        _ => 0
    };
}
