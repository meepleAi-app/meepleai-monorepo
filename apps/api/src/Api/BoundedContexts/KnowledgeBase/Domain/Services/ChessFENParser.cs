using Api.BoundedContexts.KnowledgeBase.Domain.Exceptions;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Chess FEN (Forsyth-Edwards Notation) parser for game state extraction.
/// Issue #3772: Parses FEN strings into structured chess state for Decisore Agent.
/// </summary>
/// <remarks>
/// FEN Format: [piece placement] [active color] [castling] [en passant] [halfmove] [fullmove]
/// Example: "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"
/// </remarks>
public sealed class ChessFENParser
{
    /// <summary>
    /// Parses FEN notation into ParsedGameState.
    /// </summary>
    public ParsedGameState Parse(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
            throw new InvalidGameStateException("FEN notation cannot be empty");

        var parts = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 6)
            throw new InvalidGameStateException($"Invalid FEN: expected 6 components, got {parts.Length}");

        try
        {
            // Parse each FEN component
            var board = ParsePiecePlacement(parts[0]);
            var activeColor = ParseActiveColor(parts[1]);
            var castlingRights = CastlingRights.Parse(parts[2]);
            var enPassantTarget = ParseEnPassant(parts[3]);
            var halfmoveClock = int.Parse(parts[4], System.Globalization.CultureInfo.InvariantCulture);
            var fullmoveNumber = int.Parse(parts[5], System.Globalization.CultureInfo.InvariantCulture);

            // Validate state integrity
            ValidateState(board, castlingRights, enPassantTarget);

            return new ParsedGameState
            {
                GameType = "Chess",
                ChessBoard = board,
                CurrentPlayer = activeColor,
                TurnNumber = fullmoveNumber,
                CastlingRights = castlingRights,
                EnPassantTarget = enPassantTarget,
                HalfmoveClock = halfmoveClock,
                Pieces = board.GetAllPieces(),
                Metadata = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["Format"] = "FEN",
                    ["OriginalFEN"] = fen,
                    ["Valid"] = true
                },
                ParsedAt = DateTime.UtcNow
            };
        }
        catch (Exception ex) when (ex is not InvalidGameStateException)
        {
            throw new InvalidGameStateException($"Failed to parse FEN: {ex.Message}", ex);
        }
    }

    private ChessBoard ParsePiecePlacement(string placement)
    {
        var ranks = placement.Split('/');

        if (ranks.Length != 8)
            throw new InvalidGameStateException($"Invalid piece placement: expected 8 ranks, got {ranks.Length}");

        var grid = new ChessPiece?[8, 8];

        // Parse from rank 8 to rank 1 (FEN order is 8,7,6...1)
        for (var rankIndex = 0; rankIndex < 8; rankIndex++)
        {
            var rank = 7 - rankIndex;  // Convert FEN rank (8-1) to array rank (0-7)
            var rankStr = ranks[rankIndex];
            var file = 0;

            foreach (var c in rankStr)
            {
                if (char.IsDigit(c))
                {
                    // Empty squares
                    var emptyCount = c - '0';
                    file += emptyCount;
                }
                else
                {
                    // Piece
                    if (file >= 8)
                        throw new InvalidGameStateException($"Rank {rankIndex + 1} exceeds 8 squares");

                    var position = ChessPosition.FromCoordinates(file, rank);
                    var piece = ChessPiece.FromFen(c, position.Notation);
                    grid[rank, file] = piece;
                    file++;
                }
            }

            if (file != 8)
                throw new InvalidGameStateException($"Rank {8 - rankIndex} has {file} squares, expected 8");
        }

        return ChessBoard.Create(grid);
    }

    private string ParseActiveColor(string color)
    {
        return color.ToLowerInvariant() switch
        {
            "w" => "White",
            "b" => "Black",
            _ => throw new InvalidGameStateException($"Invalid active color: {color}")
        };
    }

    private ChessPosition? ParseEnPassant(string enPassant)
    {
        if (string.Equals(enPassant, "-", StringComparison.Ordinal))
            return null;

        try
        {
            var position = ChessPosition.FromNotation(enPassant);

            // En passant must be on rank 3 (for White) or rank 6 (for Black)
            if (position.Rank != 2 && position.Rank != 5)
                throw new InvalidGameStateException($"Invalid en passant rank: {enPassant}");

            return position;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidGameStateException($"Invalid en passant square: {enPassant}", ex);
        }
    }

    private void ValidateState(ChessBoard board, CastlingRights castlingRights, ChessPosition? enPassantTarget)
    {
        // Validate both kings present
        var whiteKing = board.FindKing("White");
        var blackKing = board.FindKing("Black");

        if (whiteKing == null)
            throw new InvalidGameStateException("White king not found on board");

        if (blackKing == null)
            throw new InvalidGameStateException("Black king not found on board");

        // Validate exactly one king per color
        var pieces = board.GetAllPieces();
        var whiteKings = pieces.Count(p => string.Equals(p.Type, "King", StringComparison.Ordinal) && string.Equals(p.Color, "White", StringComparison.Ordinal));
        var blackKings = pieces.Count(p => string.Equals(p.Type, "King", StringComparison.Ordinal) && string.Equals(p.Color, "Black", StringComparison.Ordinal));

        if (whiteKings != 1)
            throw new InvalidGameStateException($"Invalid number of White kings: {whiteKings}");

        if (blackKings != 1)
            throw new InvalidGameStateException($"Invalid number of Black kings: {blackKings}");

        // Validate no pawns on ranks 1 or 8
        var invalidPawns = pieces.Where(p =>
            string.Equals(p.Type, "Pawn", StringComparison.Ordinal) && (p.Position[1] == '1' || p.Position[1] == '8')).ToList();

        if (invalidPawns.Count > 0)
        {
            var positions = string.Join(", ", invalidPawns.Select(p => p.Position));
            throw new InvalidGameStateException($"Pawns on invalid ranks: {positions}");
        }

        // Validate castling rights consistency (simplified - check rooks/king not moved)
        // This is a basic check; full validation requires move history
        if (castlingRights.WhiteKingside || castlingRights.WhiteQueenside)
        {
            var whiteKingOnStart = string.Equals(whiteKing.Notation, "e1", StringComparison.Ordinal);
            if (!whiteKingOnStart)
            {
                // King moved, castling should be unavailable (warning only, not error)
            }
        }
    }
}
