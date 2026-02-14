namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing an 8x8 chess board with piece placement.
/// Issue #3772: Core game state representation for Decisore Agent.
/// </summary>
public sealed record ChessBoard
{
    /// <summary>
    /// 8x8 grid of pieces (null for empty squares).
    /// Grid[rank, file] where rank 0 = rank 1, file 0 = file 'a'.
    /// </summary>
    public ChessPiece?[,] Grid { get; init; }

    private ChessBoard(ChessPiece?[,] grid)
    {
        if (grid.GetLength(0) != 8 || grid.GetLength(1) != 8)
            throw new ArgumentException("Chess board must be 8x8", nameof(grid));

        Grid = grid;
    }

    /// <summary>
    /// Creates an empty chess board.
    /// </summary>
    public static ChessBoard Empty()
    {
        var grid = new ChessPiece?[8, 8];
        return new ChessBoard(grid);
    }

    /// <summary>
    /// Creates board from 8x8 piece array (internal use only).
    /// </summary>
    internal static ChessBoard Create(ChessPiece?[,] grid)
    {
        return new ChessBoard(grid);
    }

    /// <summary>
    /// Gets piece at algebraic position (e.g., "e4").
    /// </summary>
    public ChessPiece? GetPieceAt(string position)
    {
        var pos = ChessPosition.FromNotation(position);
        return Grid[pos.Rank, pos.File];
    }

    /// <summary>
    /// Gets piece at file/rank coordinates.
    /// </summary>
    public ChessPiece? GetPieceAt(int file, int rank)
    {
        if (file < 0 || file > 7 || rank < 0 || rank > 7)
            return null;

        return Grid[rank, file];
    }

    /// <summary>
    /// Checks if a square is empty.
    /// </summary>
    public bool IsEmpty(string position)
    {
        return GetPieceAt(position) == null;
    }

    /// <summary>
    /// Checks if a square contains a piece of specific color.
    /// </summary>
    public bool HasPiece(string position, string color)
    {
        var piece = GetPieceAt(position);
        return piece != null && string.Equals(piece.Color, color, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets all pieces of a specific color.
    /// </summary>
    public List<ChessPiece> GetPiecesByColor(string color)
    {
        var pieces = new List<ChessPiece>();

        for (var rank = 0; rank < 8; rank++)
        {
            for (var file = 0; file < 8; file++)
            {
                var piece = Grid[rank, file];
                if (piece != null && string.Equals(piece.Color, color, StringComparison.Ordinal))
                    pieces.Add(piece);
            }
        }

        return pieces;
    }

    /// <summary>
    /// Gets all pieces on the board.
    /// </summary>
    public List<ChessPiece> GetAllPieces()
    {
        var pieces = new List<ChessPiece>();

        for (var rank = 0; rank < 8; rank++)
        {
            for (var file = 0; file < 8; file++)
            {
                var piece = Grid[rank, file];
                if (piece != null)
                    pieces.Add(piece);
            }
        }

        return pieces;
    }

    /// <summary>
    /// Finds the king position for a color.
    /// </summary>
    public ChessPosition? FindKing(string color)
    {
        for (var rank = 0; rank < 8; rank++)
        {
            for (var file = 0; file < 8; file++)
            {
                var piece = Grid[rank, file];
                if (piece != null &&
                    string.Equals(piece.Type, "King", StringComparison.Ordinal) &&
                    string.Equals(piece.Color, color, StringComparison.Ordinal))
                    return ChessPosition.FromCoordinates(file, rank);
            }
        }

        return null;
    }

    /// <summary>
    /// Counts total material value for a color.
    /// </summary>
    public int GetMaterialCount(string color)
    {
        return GetPiecesByColor(color).Sum(p => p.GetMaterialValue());
    }
}
