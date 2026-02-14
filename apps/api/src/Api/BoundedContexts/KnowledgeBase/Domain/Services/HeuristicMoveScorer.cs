using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Heuristic move scorer for rapid evaluation without LLM.
/// Issue #3770: Provides material, positional, tactical, and development scoring.
/// </summary>
internal sealed class HeuristicMoveScorer : IMoveScorer
{
    // Piece-square table for positional scoring (center = higher value)
    private static readonly double[,] s_centerControlTable = new double[,]
    {
        { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 },  // Rank 1
        { 0.0, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.0 },  // Rank 2
        { 0.0, 0.1, 0.2, 0.2, 0.2, 0.2, 0.1, 0.0 },  // Rank 3
        { 0.0, 0.1, 0.2, 0.3, 0.3, 0.2, 0.1, 0.0 },  // Rank 4 (center)
        { 0.0, 0.1, 0.2, 0.3, 0.3, 0.2, 0.1, 0.0 },  // Rank 5 (center)
        { 0.0, 0.1, 0.2, 0.2, 0.2, 0.2, 0.1, 0.0 },  // Rank 6
        { 0.0, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.0 },  // Rank 7
        { 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0 }   // Rank 8
    };

    public MoveScore Score(CandidateMove move, ParsedGameState state, string playerColor)
    {
        ArgumentNullException.ThrowIfNull(move);
        ArgumentNullException.ThrowIfNull(state);

        var material = CalculateMaterialScore(move);
        var positional = CalculatePositionalScore(move);
        var tactical = CalculateTacticalScore(move, state, playerColor);
        var development = CalculateDevelopmentScore(move, state);

        return MoveScore.Create(material, positional, tactical, development);
    }

    private static double CalculateMaterialScore(CandidateMove move)
    {
        if (!move.IsCapture || move.CapturedPiece == null)
            return 0.0;

        var captureValue = move.CapturedPiece.GetMaterialValue();
        var attackerValue = move.Piece.GetMaterialValue();

        // Winning capture: positive, losing capture: negative
        return captureValue - attackerValue;
    }

    private static double CalculatePositionalScore(CandidateMove move)
    {
        var fromScore = s_centerControlTable[move.From.Rank, move.From.File];
        var toScore = s_centerControlTable[move.To.Rank, move.To.File];

        // Bonus for knights and bishops in center
        var centerBonus = 0.0;
        if (move.To.IsCenter() &&
            (string.Equals(move.Piece.Type, "Knight", StringComparison.Ordinal) ||
             string.Equals(move.Piece.Type, "Bishop", StringComparison.Ordinal)))
        {
            centerBonus = 0.2;
        }

        return (toScore - fromScore) + centerBonus;
    }

    private double CalculateTacticalScore(CandidateMove move, ParsedGameState state, string playerColor)
    {
        var score = 0.0;

        // Check bonus
        if (move.IsCheck)
            score += 0.5;

        // Threat detection (attacks high-value pieces)
        var opponentColor = string.Equals(playerColor, "White", StringComparison.Ordinal) ? "Black" : "White";
        var threatenedPiece = FindThreatenedPiece(move.To, state.ChessBoard!, opponentColor);
        if (threatenedPiece != null)
        {
            var threatValue = threatenedPiece.GetMaterialValue();
            if (threatValue >= 5)  // Threatens Rook or Queen
                score += 0.3;
            else if (threatValue >= 3)  // Threatens minor piece
                score += 0.2;
        }

        // Defensive bonus (protects attacked piece)
        var protectedPiece = FindProtectedPiece(move.To, state.ChessBoard!, playerColor);
        if (protectedPiece != null && IsAttacked(protectedPiece, state.ChessBoard!, opponentColor))
        {
            score += 0.2;
        }

        return Math.Clamp(score, -1.0, 1.0);
    }

    private static double CalculateDevelopmentScore(CandidateMove move, ParsedGameState state)
    {
        // Development only matters in opening (first 10 moves)
        if (state.TurnNumber > 10)
            return 0.0;

        var score = 0.0;

        // Minor piece development from back rank
        if ((string.Equals(move.Piece.Type, "Knight", StringComparison.Ordinal) ||
             string.Equals(move.Piece.Type, "Bishop", StringComparison.Ordinal)) &&
            (move.From.Rank == 0 || move.From.Rank == 7))
        {
            score += 0.2;
        }

        // Center control bonus
        if (move.To.IsCenter())
            score += 0.1;

        return Math.Clamp(score, 0.0, 0.5);
    }

    private static ChessPiece? FindThreatenedPiece(ChessPosition from, ChessBoard board, string targetColor)
    {
        // Simplified: check adjacent squares for threatened pieces
        for (var fileOffset = -1; fileOffset <= 1; fileOffset++)
        {
            for (var rankOffset = -1; rankOffset <= 1; rankOffset++)
            {
                var file = from.File + fileOffset;
                var rank = from.Rank + rankOffset;

                if (file < 0 || file > 7 || rank < 0 || rank > 7)
                    continue;

                var piece = board.GetPieceAt(file, rank);
                if (piece != null && string.Equals(piece.Color, targetColor, StringComparison.Ordinal))
                    return piece;
            }
        }

        return null;
    }

    private static ChessPiece? FindProtectedPiece(ChessPosition from, ChessBoard board, string allyColor)
    {
        // Similar to FindThreatenedPiece but for friendly pieces
        return FindThreatenedPiece(from, board, allyColor);
    }

    private static bool IsAttacked(ChessPiece piece, ChessBoard board, string attackerColor)
    {
        // Simplified: assume piece is attacked if opponent has piece nearby
        // Full implementation would use CanPieceAttackSquare for all opponent pieces
        return board.GetPiecesByColor(attackerColor).Count > 0;
    }
}
