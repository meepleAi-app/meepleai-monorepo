namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing parsed and structured game state for strategic analysis.
/// Issue #3772: Multi-game state representation for Decisore Agent.
/// </summary>
public sealed record ParsedGameState
{
    /// <summary>
    /// Game type identifier (e.g., "Chess", "Catan").
    /// </summary>
    public required string GameType { get; init; }

    /// <summary>
    /// Chess-specific board representation (null for non-chess games).
    /// </summary>
    public ChessBoard? ChessBoard { get; init; }

    /// <summary>
    /// Current active player.
    /// </summary>
    public required string CurrentPlayer { get; init; }

    /// <summary>
    /// Turn/move number.
    /// </summary>
    public required int TurnNumber { get; init; }

    /// <summary>
    /// Chess castling rights (null for non-chess games).
    /// </summary>
    public CastlingRights? CastlingRights { get; init; }

    /// <summary>
    /// Chess en passant target square (null if not available).
    /// </summary>
    public ChessPosition? EnPassantTarget { get; init; }

    /// <summary>
    /// Halfmove clock for 50-move rule (chess-specific).
    /// </summary>
    public int HalfmoveClock { get; init; }

    /// <summary>
    /// List of all pieces on the board (chess-specific).
    /// </summary>
    public List<ChessPiece> Pieces { get; init; } = new();

    /// <summary>
    /// Generic metadata for extensibility (game-specific data).
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Timestamp when state was parsed.
    /// </summary>
    public DateTime ParsedAt { get; init; }

    /// <summary>
    /// Validates chess state integrity (both kings present, valid piece placement).
    /// </summary>
    public bool IsValidChessState()
    {
        if (!string.Equals(GameType, "Chess", StringComparison.Ordinal) || ChessBoard == null)
            return false;

        // Check both kings present
        var whiteKing = ChessBoard.FindKing("White");
        var blackKing = ChessBoard.FindKing("Black");

        if (whiteKing == null || blackKing == null)
            return false;

        // Check no pawns on ranks 1 or 8
        var invalidPawns = Pieces.Any(p =>
            string.Equals(p.Type, "Pawn", StringComparison.Ordinal) && (p.Position[1] == '1' || p.Position[1] == '8'));

        if (invalidPawns)
            return false;

        return true;
    }

    /// <summary>
    /// Gets FEN notation for chess state (if applicable).
    /// </summary>
    public string? GetFen()
    {
        if (!string.Equals(GameType, "Chess", StringComparison.Ordinal) || ChessBoard == null || CastlingRights == null)
            return null;

        // Reconstruct FEN from components
        var placement = BuildPiecePlacement();
        var activeColor = string.Equals(CurrentPlayer, "White", StringComparison.Ordinal) ? "w" : "b";
        var castling = CastlingRights.ToFen();
        var enPassant = EnPassantTarget?.Notation ?? "-";
        var halfmove = HalfmoveClock.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var fullmove = TurnNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return $"{placement} {activeColor} {castling} {enPassant} {halfmove} {fullmove}";
    }

    private string BuildPiecePlacement()
    {
        var ranks = new List<string>();

        // Build from rank 8 to rank 1 (FEN order)
        for (var rank = 7; rank >= 0; rank--)
        {
            var rankBuilder = new System.Text.StringBuilder();
            var emptyCount = 0;

            for (var file = 0; file < 8; file++)
            {
                var piece = ChessBoard!.GetPieceAt(file, rank);

                if (piece == null)
                {
                    emptyCount++;
                }
                else
                {
                    if (emptyCount > 0)
                    {
                        rankBuilder.Append(emptyCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        emptyCount = 0;
                    }
                    rankBuilder.Append(piece.FenChar);
                }
            }

            if (emptyCount > 0)
                rankBuilder.Append(emptyCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

            ranks.Add(rankBuilder.ToString());
        }

        return string.Join('/', ranks);
    }
}
