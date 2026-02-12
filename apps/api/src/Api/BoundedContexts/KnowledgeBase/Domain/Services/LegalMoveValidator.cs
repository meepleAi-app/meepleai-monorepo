using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Validates chess move legality by checking if move exposes king to check.
/// Issue #3770: Filters illegal moves for Decisore Agent.
/// </summary>
internal sealed class LegalMoveValidator : ILegalMoveValidator
{
    public Task<bool> IsLegalAsync(
        CandidateMove move,
        ParsedGameState state,
        CancellationToken cancellationToken = default)
    {
        // Simulate move on temporary board
        var tempBoard = ApplyMove(state.ChessBoard!, move);

        // Find player's king after move
        var playerKing = tempBoard.FindKing(move.Piece.Color);
        if (playerKing == null)
            return Task.FromResult(false);  // King captured (illegal)

        // Check if king is attacked by opponent
        var opponentColor = GetOpponentColor(move.Piece.Color);
        var isInCheck = IsSquareAttacked(tempBoard, playerKing, opponentColor);

        return Task.FromResult(!isInCheck);  // Legal if NOT in check
    }

    public bool IsSquareAttacked(ChessBoard board, ChessPosition square, string attackerColor)
    {
        var attackers = board.GetPiecesByColor(attackerColor);

        foreach (var attacker in attackers)
        {
            if (CanPieceAttackSquare(attacker, square, board))
                return true;
        }

        return false;
    }

    private bool CanPieceAttackSquare(ChessPiece attacker, ChessPosition target, ChessBoard board)
    {
        var from = ChessPosition.FromNotation(attacker.Position);

        return attacker.Type switch
        {
            "Pawn" => CanPawnAttack(from, target, attacker.Color),
            "Knight" => CanKnightAttack(from, target),
            "Bishop" => CanBishopAttack(from, target, board),
            "Rook" => CanRookAttack(from, target, board),
            "Queen" => CanQueenAttack(from, target, board),
            "King" => CanKingAttack(from, target),
            _ => false
        };
    }

    private static bool CanPawnAttack(ChessPosition from, ChessPosition target, string pawnColor)
    {
        var direction = string.Equals(pawnColor, "White", StringComparison.Ordinal) ? 1 : -1;
        var expectedRank = from.Rank + direction;

        // Pawn attacks diagonally
        return target.Rank == expectedRank && Math.Abs(target.File - from.File) == 1;
    }

    private static bool CanKnightAttack(ChessPosition from, ChessPosition target)
    {
        var fileDiff = Math.Abs(target.File - from.File);
        var rankDiff = Math.Abs(target.Rank - from.Rank);

        // L-shape: (2,1) or (1,2)
        return (fileDiff == 2 && rankDiff == 1) || (fileDiff == 1 && rankDiff == 2);
    }

    private static bool CanBishopAttack(ChessPosition from, ChessPosition target, ChessBoard board)
    {
        return IsDiagonalPath(from, target) && IsPathClear(from, target, board);
    }

    private static bool CanRookAttack(ChessPosition from, ChessPosition target, ChessBoard board)
    {
        return IsOrthogonalPath(from, target) && IsPathClear(from, target, board);
    }

    private static bool CanQueenAttack(ChessPosition from, ChessPosition target, ChessBoard board)
    {
        return (IsDiagonalPath(from, target) || IsOrthogonalPath(from, target)) &&
               IsPathClear(from, target, board);
    }

    private static bool CanKingAttack(ChessPosition from, ChessPosition target)
    {
        var fileDiff = Math.Abs(target.File - from.File);
        var rankDiff = Math.Abs(target.Rank - from.Rank);

        // Adjacent square (max 1 square in any direction)
        return fileDiff <= 1 && rankDiff <= 1 && (fileDiff != 0 || rankDiff != 0);
    }

    private static bool IsDiagonalPath(ChessPosition from, ChessPosition to)
    {
        return Math.Abs(to.File - from.File) == Math.Abs(to.Rank - from.Rank);
    }

    private static bool IsOrthogonalPath(ChessPosition from, ChessPosition to)
    {
        return from.File == to.File || from.Rank == to.Rank;
    }

    private static bool IsPathClear(ChessPosition from, ChessPosition to, ChessBoard board)
    {
        var fileStep = Math.Sign(to.File - from.File);
        var rankStep = Math.Sign(to.Rank - from.Rank);

        var file = from.File + fileStep;
        var rank = from.Rank + rankStep;

        // Check all squares between from and to (exclusive)
        while (file != to.File || rank != to.Rank)
        {
            var pos = ChessPosition.FromCoordinates(file, rank);
            if (!board.IsEmpty(pos.Notation))
                return false;  // Blocked

            file += fileStep;
            rank += rankStep;
        }

        return true;  // Path clear
    }

    private static ChessBoard ApplyMove(ChessBoard originalBoard, CandidateMove move)
    {
        // Create new board with move applied (immutable pattern)
        var grid = new ChessPiece?[8, 8];

        // Copy all pieces except the moving piece
        for (var rank = 0; rank < 8; rank++)
        {
            for (var file = 0; file < 8; file++)
            {
                var piece = originalBoard.GetPieceAt(file, rank);
                if (piece != null && !string.Equals(piece.Position, move.From.Notation, StringComparison.Ordinal))
                {
                    grid[rank, file] = piece;
                }
            }
        }

        // Place moving piece at destination
        var movedPiece = ChessPiece.Create(move.Piece.Type, move.Piece.Color, move.To.Notation);
        grid[move.To.Rank, move.To.File] = movedPiece;

        return ChessBoard.Create(grid);
    }

    private static string GetOpponentColor(string color)
    {
        return string.Equals(color, "White", StringComparison.Ordinal) ? "Black" : "White";
    }
}
