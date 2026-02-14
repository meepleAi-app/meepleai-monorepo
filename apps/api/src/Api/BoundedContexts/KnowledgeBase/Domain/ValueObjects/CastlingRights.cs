namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing castling availability for both sides.
/// Issue #3772: Chess game state component for Decisore Agent.
/// </summary>
public sealed record CastlingRights
{
    public bool WhiteKingside { get; init; }
    public bool WhiteQueenside { get; init; }
    public bool BlackKingside { get; init; }
    public bool BlackQueenside { get; init; }

    private CastlingRights(bool whiteKingside, bool whiteQueenside, bool blackKingside, bool blackQueenside)
    {
        WhiteKingside = whiteKingside;
        WhiteQueenside = whiteQueenside;
        BlackKingside = blackKingside;
        BlackQueenside = blackQueenside;
    }

    /// <summary>
    /// Parses castling rights from FEN notation (e.g., "KQkq", "K", "-").
    /// </summary>
    public static CastlingRights Parse(string fenCastling)
    {
        if (string.IsNullOrWhiteSpace(fenCastling))
            throw new ArgumentException("Castling notation cannot be empty", nameof(fenCastling));

        if (string.Equals(fenCastling, "-", StringComparison.Ordinal))
            return None();

        var whiteKingside = fenCastling.Contains('K');
        var whiteQueenside = fenCastling.Contains('Q');
        var blackKingside = fenCastling.Contains('k');
        var blackQueenside = fenCastling.Contains('q');

        // Validate only contains valid characters
        foreach (var c in fenCastling)
        {
            if (c != 'K' && c != 'Q' && c != 'k' && c != 'q')
                throw new ArgumentException($"Invalid castling character: {c}", nameof(fenCastling));
        }

        return new CastlingRights(whiteKingside, whiteQueenside, blackKingside, blackQueenside);
    }

    /// <summary>
    /// No castling rights available.
    /// </summary>
    public static CastlingRights None() => new(false, false, false, false);

    /// <summary>
    /// All castling rights available (starting position).
    /// </summary>
    public static CastlingRights All() => new(true, true, true, true);

    /// <summary>
    /// Converts to FEN notation.
    /// </summary>
    public string ToFen()
    {
        if (!WhiteKingside && !WhiteQueenside && !BlackKingside && !BlackQueenside)
            return "-";

        var fen = string.Empty;
        if (WhiteKingside) fen += 'K';
        if (WhiteQueenside) fen += 'Q';
        if (BlackKingside) fen += 'k';
        if (BlackQueenside) fen += 'q';

        return fen;
    }

    /// <summary>
    /// Checks if any castling is available for a color.
    /// </summary>
    public bool CanCastle(string color) => string.Equals(color, "White", StringComparison.Ordinal)
        ? WhiteKingside || WhiteQueenside
        : BlackKingside || BlackQueenside;
}
