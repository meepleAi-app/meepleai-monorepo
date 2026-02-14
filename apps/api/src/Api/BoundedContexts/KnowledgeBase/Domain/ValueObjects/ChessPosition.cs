namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing a chess board position in algebraic notation.
/// Issue #3772: Coordinate system for Decisore Agent.
/// </summary>
public sealed record ChessPosition
{
    public string Notation { get; init; }  // "e4", "a1", etc.
    public int File { get; init; }         // 0-7 (a-h)
    public int Rank { get; init; }         // 0-7 (1-8)

    private ChessPosition(string notation, int file, int rank)
    {
        Notation = notation;
        File = file;
        Rank = rank;
    }

    /// <summary>
    /// Creates position from algebraic notation (e.g., "e4").
    /// </summary>
    public static ChessPosition FromNotation(string notation)
    {
        if (string.IsNullOrWhiteSpace(notation) || notation.Length != 2)
            throw new ArgumentException("Position must be 2 characters (e.g., 'e4')", nameof(notation));

        var file = notation[0] - 'a';  // 'a'=0, 'h'=7
        var rank = notation[1] - '1';  // '1'=0, '8'=7

        if (file < 0 || file > 7)
            throw new ArgumentException($"Invalid file: {notation[0]}", nameof(notation));

        if (rank < 0 || rank > 7)
            throw new ArgumentException($"Invalid rank: {notation[1]}", nameof(notation));

        return new ChessPosition(notation.ToLowerInvariant(), file, rank);
    }

    /// <summary>
    /// Creates position from file/rank coordinates.
    /// </summary>
    public static ChessPosition FromCoordinates(int file, int rank)
    {
        if (file < 0 || file > 7)
            throw new ArgumentException("File must be 0-7", nameof(file));

        if (rank < 0 || rank > 7)
            throw new ArgumentException("Rank must be 0-7", nameof(rank));

        var notation = $"{(char)('a' + file)}{rank + 1}";
        return new ChessPosition(notation, file, rank);
    }

    /// <summary>
    /// Checks if position is in center (d4, d5, e4, e5).
    /// </summary>
    public bool IsCenter() => File is >= 3 and <= 4 && Rank is >= 3 and <= 4;

    /// <summary>
    /// Checks if position is on edge of board.
    /// </summary>
    public bool IsEdge() => File == 0 || File == 7 || Rank == 0 || Rank == 7;
}
