using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Chess move generator implementing IMoveGeneratorService.
/// Issue #3770: Generates legal moves with heuristic scoring for Decisore Agent.
/// </summary>
/// <remarks>
/// MVP Implementation:
/// - Full support: Pawn (push, capture), Knight, Bishop, Rook, Queen, King (basic)
/// - Deferred: Castling, en passant (Phase 2 iteration)
/// </remarks>
internal sealed class ChessMoveGenerator : IMoveGeneratorService
{
    private readonly ILegalMoveValidator _validator;
    private readonly IMoveScorer _scorer;

    public ChessMoveGenerator(ILegalMoveValidator validator, IMoveScorer scorer)
    {
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _scorer = scorer ?? throw new ArgumentNullException(nameof(scorer));
    }

    public async Task<List<CandidateMove>> GenerateCandidatesAsync(
        ParsedGameState state,
        string playerColor,
        int maxCandidates = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.ChessBoard == null)
            throw new ArgumentException("ChessBoard is required", nameof(state));

        var pieces = state.ChessBoard.GetPiecesByColor(playerColor);
        var pseudoLegalMoves = new List<CandidateMove>();

        // Generate pseudo-legal moves for each piece
        foreach (var piece in pieces)
        {
            var moves = piece.Type switch
            {
                "Pawn" => GeneratePawnMoves(piece, state),
                "Knight" => GenerateKnightMoves(piece, state),
                "Bishop" => GenerateBishopMoves(piece, state),
                "Rook" => GenerateRookMoves(piece, state),
                "Queen" => GenerateQueenMoves(piece, state),
                "King" => GenerateKingMoves(piece, state),
                _ => new List<CandidateMove>()
            };

            pseudoLegalMoves.AddRange(moves);
        }

        // Filter to legal moves only (parallel for performance)
        var legalityTasks = pseudoLegalMoves.Select(async move =>
        {
            var isLegal = await _validator.IsLegalAsync(move, state, cancellationToken).ConfigureAwait(false);
            return (move, isLegal);
        });

        var legalityResults = await Task.WhenAll(legalityTasks).ConfigureAwait(false);
        var legalMoves = legalityResults
            .Where(r => r.isLegal)
            .Select(r => r.move)
            .ToList();

        // Score and prioritize
        var scoredMoves = legalMoves.Select(m =>
        {
            var score = _scorer.Score(m, state, playerColor);
            var priority = MovePriorityExtensions.Classify(score, m.IsCapture, m.IsCheck);
            return m with { Score = score, Priority = priority };
        }).ToList();

        // Rank: priority ascending, then score descending
        var rankedMoves = scoredMoves
            .OrderBy(m => m.Priority)
            .ThenByDescending(m => m.Score.Overall)
            .Take(maxCandidates)
            .ToList();

        return rankedMoves;
    }

    public async Task<bool> IsLegalMoveAsync(
        ParsedGameState state,
        ChessPosition from,
        ChessPosition toPosition,
        CancellationToken cancellationToken = default)
    {
        var piece = state.ChessBoard!.GetPieceAt(from.Notation);
        if (piece == null)
            return false;

        var tempMove = CandidateMove.Create(
            piece, from, toPosition,
            capturedPiece: state.ChessBoard.GetPieceAt(toPosition.Notation),
            score: MoveScore.Zero(),
            priority: MovePriority.Low);

        return await _validator.IsLegalAsync(tempMove, state, cancellationToken).ConfigureAwait(false);
    }

    // ===== PIECE-SPECIFIC MOVE GENERATORS =====

    private List<CandidateMove> GeneratePawnMoves(ChessPiece pawn, ParsedGameState state)
    {
        var moves = new List<CandidateMove>();
        var board = state.ChessBoard!;
        var from = ChessPosition.FromNotation(pawn.Position);
        var direction = string.Equals(pawn.Color, "White", StringComparison.Ordinal) ? 1 : -1;
        var startRank = string.Equals(pawn.Color, "White", StringComparison.Ordinal) ? 1 : 6;

        // Single push forward
        var singlePush = from.Rank + direction;
        if (singlePush >= 0 && singlePush <= 7)
        {
            var to = ChessPosition.FromCoordinates(from.File, singlePush);
            if (board.IsEmpty(to.Notation))
            {
                var isPromotion = singlePush == 7 || singlePush == 0;
                moves.Add(CreateMove(pawn, from, to, null, isPromotion, "Queen"));
            }
        }

        // Double push from starting rank
        if (from.Rank == startRank)
        {
            var doublePush = from.Rank + (2 * direction);
            var intermediateTo = ChessPosition.FromCoordinates(from.File, from.Rank + direction);
            var finalTo = ChessPosition.FromCoordinates(from.File, doublePush);

            if (board.IsEmpty(intermediateTo.Notation) && board.IsEmpty(finalTo.Notation))
            {
                moves.Add(CreateMove(pawn, from, finalTo, null));
            }
        }

        // Diagonal captures
        for (var fileOffset = -1; fileOffset <= 1; fileOffset += 2)  // -1 (left), +1 (right)
        {
            var captureFile = from.File + fileOffset;
            var captureRank = from.Rank + direction;

            if (captureFile < 0 || captureFile > 7 || captureRank < 0 || captureRank > 7)
                continue;

            var captureTo = ChessPosition.FromCoordinates(captureFile, captureRank);
            var targetPiece = board.GetPieceAt(captureTo.Notation);

            if (targetPiece != null && !string.Equals(targetPiece.Color, pawn.Color, StringComparison.Ordinal))
            {
                var isPromotion = captureRank == 7 || captureRank == 0;
                moves.Add(CreateMove(pawn, from, captureTo, targetPiece, isPromotion, "Queen"));
            }

            // En passant (if target square matches state.EnPassantTarget)
            if (state.EnPassantTarget != null &&
                string.Equals(captureTo.Notation, state.EnPassantTarget.Notation, StringComparison.Ordinal))
            {
                var capturedPawnRank = from.Rank;  // En passant captures pawn on same rank
                var capturedPawnPos = ChessPosition.FromCoordinates(captureFile, capturedPawnRank);
                var capturedPawn = board.GetPieceAt(capturedPawnPos.Notation);
                moves.Add(CreateMove(pawn, from, captureTo, capturedPawn));
            }
        }

        return moves;
    }

    private List<CandidateMove> GenerateKnightMoves(ChessPiece knight, ParsedGameState state)
    {
        var moves = new List<CandidateMove>();
        var board = state.ChessBoard!;
        var from = ChessPosition.FromNotation(knight.Position);

        // 8 L-shaped knight moves: (±2,±1), (±1,±2)
        int[] fileOffsets = { -2, -2, -1, -1, 1, 1, 2, 2 };
        int[] rankOffsets = { -1, 1, -2, 2, -2, 2, -1, 1 };

        for (var i = 0; i < 8; i++)
        {
            var toFile = from.File + fileOffsets[i];
            var toRank = from.Rank + rankOffsets[i];

            if (toFile < 0 || toFile > 7 || toRank < 0 || toRank > 7)
                continue;

            var to = ChessPosition.FromCoordinates(toFile, toRank);
            var targetPiece = board.GetPieceAt(to.Notation);

            // Can move to empty square or capture enemy
            if (targetPiece == null || !string.Equals(targetPiece.Color, knight.Color, StringComparison.Ordinal))
            {
                moves.Add(CreateMove(knight, from, to, targetPiece));
            }
        }

        return moves;
    }

    private List<CandidateMove> GenerateBishopMoves(ChessPiece bishop, ParsedGameState state)
    {
        // Diagonal rays in 4 directions
        return GenerateSlidingMoves(bishop, state, diagonal: true, orthogonal: false);
    }

    private List<CandidateMove> GenerateRookMoves(ChessPiece rook, ParsedGameState state)
    {
        // Orthogonal rays in 4 directions
        return GenerateSlidingMoves(rook, state, diagonal: false, orthogonal: true);
    }

    private List<CandidateMove> GenerateQueenMoves(ChessPiece queen, ParsedGameState state)
    {
        // Combination of bishop + rook (8 directions)
        return GenerateSlidingMoves(queen, state, diagonal: true, orthogonal: true);
    }

    private List<CandidateMove> GenerateSlidingMoves(
        ChessPiece piece,
        ParsedGameState state,
        bool diagonal,
        bool orthogonal)
    {
        var moves = new List<CandidateMove>();
        var board = state.ChessBoard!;
        var from = ChessPosition.FromNotation(piece.Position);

        // Direction vectors
        var directions = new List<(int fileDir, int rankDir)>();

        if (diagonal)
        {
            directions.Add((1, 1));   // NE
            directions.Add((1, -1));  // SE
            directions.Add((-1, 1));  // NW
            directions.Add((-1, -1)); // SW
        }

        if (orthogonal)
        {
            directions.Add((0, 1));   // N
            directions.Add((0, -1));  // S
            directions.Add((1, 0));   // E
            directions.Add((-1, 0));  // W
        }

        // Ray-cast in each direction until blocked
        foreach (var (fileDir, rankDir) in directions)
        {
            var file = from.File;
            var rank = from.Rank;

            while (true)
            {
                file += fileDir;
                rank += rankDir;

                // Out of bounds
                if (file < 0 || file > 7 || rank < 0 || rank > 7)
                    break;

                var to = ChessPosition.FromCoordinates(file, rank);
                var targetPiece = board.GetPieceAt(to.Notation);

                if (targetPiece == null)
                {
                    // Empty square - can move here and continue
                    moves.Add(CreateMove(piece, from, to, null));
                }
                else if (!string.Equals(targetPiece.Color, piece.Color, StringComparison.Ordinal))
                {
                    // Enemy piece - can capture, then stop
                    moves.Add(CreateMove(piece, from, to, targetPiece));
                    break;
                }
                else
                {
                    // Friendly piece - blocked, stop
                    break;
                }
            }
        }

        return moves;
    }

    private List<CandidateMove> GenerateKingMoves(ChessPiece king, ParsedGameState state)
    {
        var moves = new List<CandidateMove>();
        var board = state.ChessBoard!;
        var from = ChessPosition.FromNotation(king.Position);

        // 8 adjacent squares
        for (var fileOffset = -1; fileOffset <= 1; fileOffset++)
        {
            for (var rankOffset = -1; rankOffset <= 1; rankOffset++)
            {
                if (fileOffset == 0 && rankOffset == 0)
                    continue;  // Skip same square

                var toFile = from.File + fileOffset;
                var toRank = from.Rank + rankOffset;

                if (toFile < 0 || toFile > 7 || toRank < 0 || toRank > 7)
                    continue;

                var to = ChessPosition.FromCoordinates(toFile, toRank);
                var targetPiece = board.GetPieceAt(to.Notation);

                // Can move to empty or capture enemy
                if (targetPiece == null || !string.Equals(targetPiece.Color, king.Color, StringComparison.Ordinal))
                {
                    moves.Add(CreateMove(king, from, to, targetPiece));
                }
            }
        }

        // NOTE: Castling deferred to Phase 2 iteration (requires rook positions, path clear, not in check)

        return moves;
    }

    private static CandidateMove CreateMove(
        ChessPiece piece,
        ChessPosition from,
        ChessPosition to,
        ChessPiece? capturedPiece,
        bool isPromotion = false,
        string? promotionPiece = null)
    {
        return CandidateMove.Create(
            piece,
            from,
            to,
            capturedPiece,
            score: MoveScore.Zero(),  // Scored later by IMoveScorer
            priority: MovePriority.Low,  // Classified later after scoring
            isCheck: false,  // Detected by validator
            isPromotion: isPromotion,
            promotionPiece: isPromotion ? promotionPiece : null);  // Fix: null when not promoting
    }
}
