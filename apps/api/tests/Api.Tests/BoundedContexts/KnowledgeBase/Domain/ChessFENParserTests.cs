using Api.BoundedContexts.KnowledgeBase.Domain.Exceptions;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for ChessFENParser (Issue #3772)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3772")]
public sealed class ChessFENParserTests
{
    private readonly ChessFENParser _parser = new();

    // ===== VALID FEN PARSING =====

    [Fact]
    public void Parse_StartingPosition_ShouldParseCorrectly()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.GameType.Should().Be("Chess");
        state.CurrentPlayer.Should().Be("White");
        state.TurnNumber.Should().Be(1);
        state.HalfmoveClock.Should().Be(0);
        state.CastlingRights.Should().NotBeNull();
        state.CastlingRights!.WhiteKingside.Should().BeTrue();
        state.CastlingRights.WhiteQueenside.Should().BeTrue();
        state.CastlingRights.BlackKingside.Should().BeTrue();
        state.CastlingRights.BlackQueenside.Should().BeTrue();
        state.EnPassantTarget.Should().BeNull();
        state.Pieces.Should().HaveCount(32);
        state.ChessBoard.Should().NotBeNull();
        state.IsValidChessState().Should().BeTrue();
    }

    [Fact]
    public void Parse_AfterE4Move_ShouldParseCorrectly()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.CurrentPlayer.Should().Be("Black");
        state.EnPassantTarget.Should().NotBeNull();
        state.EnPassantTarget!.Notation.Should().Be("e3");
        state.ChessBoard!.GetPieceAt("e4")!.Type.Should().Be("Pawn");
        state.ChessBoard.GetPieceAt("e4")!.Color.Should().Be("White");
        state.ChessBoard.GetPieceAt("e2").Should().BeNull();
    }

    [Fact]
    public void Parse_AfterCastling_ShouldUpdateRights()
    {
        // Arrange - White castled kingside
        var fen = "r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/3P1N2/PPP2PPP/RNBQ1RK1 b kq - 0 6";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.CastlingRights!.WhiteKingside.Should().BeFalse();
        state.CastlingRights.WhiteQueenside.Should().BeFalse();
        state.CastlingRights.BlackKingside.Should().BeTrue();
        state.CastlingRights.BlackQueenside.Should().BeTrue();
        state.ChessBoard!.GetPieceAt("g1")!.Type.Should().Be("King");
        state.ChessBoard.GetPieceAt("f1")!.Type.Should().Be("Rook");
    }

    [Fact]
    public void Parse_Middlegame_ShouldParseAllPieces()
    {
        // Arrange
        var fen = "r1bq1rk1/pp2ppbp/2np1np1/8/2BPP3/2N2N2/PPP2PPP/R1BQ1RK1 w - - 4 8";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.Pieces.Should().HaveCount(28);  // 4 pieces captured
        state.CastlingRights!.ToFen().Should().Be("-");
        state.TurnNumber.Should().Be(8);
        state.HalfmoveClock.Should().Be(4);
    }

    [Fact]
    public void Parse_Endgame_ShouldHandleFewPieces()
    {
        // Arrange
        var fen = "8/5k2/8/8/8/3K4/8/8 w - - 50 75";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.Pieces.Should().HaveCount(2);  // Only 2 kings
        state.HalfmoveClock.Should().Be(50);  // 50-move rule approaching
        state.TurnNumber.Should().Be(75);
    }

    [Fact]
    public void Parse_SicilianOpening_ShouldParseCorrectly()
    {
        // Arrange
        var fen = "rnbqkbnr/pp1ppppp/8/2p5/4P3/8/PPPP1PPP/RNBQKBNR w KQkq c6 0 2";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.TurnNumber.Should().Be(2);
        state.EnPassantTarget!.Notation.Should().Be("c6");
        state.ChessBoard!.GetPieceAt("c5")!.Type.Should().Be("Pawn");
        state.ChessBoard.GetPieceAt("c5")!.Color.Should().Be("Black");
    }

    [Fact]
    public void Parse_QueensGambit_ShouldParseCorrectly()
    {
        // Arrange
        var fen = "rnbqkb1r/ppp1pppp/5n2/3p4/2PP4/8/PP2PPPP/RNBQKBNR w KQkq - 2 3";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.TurnNumber.Should().Be(3);
        state.ChessBoard!.GetPieceAt("d4")!.Type.Should().Be("Pawn");
        state.ChessBoard.GetPieceAt("c4")!.Type.Should().Be("Pawn");
        state.ChessBoard.GetPieceAt("d5")!.Color.Should().Be("Black");
    }

    // ===== INVALID FEN ERROR HANDLING =====

    [Fact]
    public void Parse_EmptyString_ShouldThrowInvalidGameStateException()
    {
        // Act
        var act = () => _parser.Parse("");

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*cannot be empty*");
    }

    [Fact]
    public void Parse_MissingComponent_ShouldThrowInvalidGameStateException()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq";  // Missing 3 components

        // Act
        var act = () => _parser.Parse(fen);

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*expected 6 components*");
    }

    [Fact]
    public void Parse_InvalidRankCount_ShouldThrowInvalidGameStateException()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP w KQkq - 0 1";  // Only 7 ranks

        // Act
        var act = () => _parser.Parse(fen);

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*expected 8 ranks*");
    }

    [Fact]
    public void Parse_RankTooLong_ShouldThrowInvalidGameStateException()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/9/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";  // Rank has 9 squares

        // Act
        var act = () => _parser.Parse(fen);

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*exceeds 8 squares*");
    }

    [Fact]
    public void Parse_MissingWhiteKing_ShouldThrowInvalidGameStateException()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQ1BNR w KQkq - 0 1";  // No White king

        // Act
        var act = () => _parser.Parse(fen);

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*White king not found*");
    }

    [Fact]
    public void Parse_MissingBlackKing_ShouldThrowInvalidGameStateException()
    {
        // Arrange
        var fen = "rnbq1bnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";  // No Black king

        // Act
        var act = () => _parser.Parse(fen);

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*Black king not found*");
    }

    [Fact]
    public void Parse_PawnOnRank1_ShouldThrowInvalidGameStateException()
    {
        // Arrange
        var fen = "rnbqkbnP/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";  // White pawn on rank 8

        // Act
        var act = () => _parser.Parse(fen);

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*Pawns on invalid ranks*");
    }

    [Fact]
    public void Parse_InvalidActiveColor_ShouldThrowInvalidGameStateException()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR x KQkq - 0 1";  // Invalid color

        // Act
        var act = () => _parser.Parse(fen);

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*Invalid active color*");
    }

    [Fact]
    public void Parse_InvalidEnPassant_ShouldThrowInvalidGameStateException()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e1 0 1";  // e1 invalid for en passant

        // Act
        var act = () => _parser.Parse(fen);

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*Invalid en passant rank*");
    }

    [Fact]
    public void Parse_InvalidFENCharacter_ShouldThrowInvalidGameStateException()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQXBNR w KQkq - 0 1";  // X is invalid

        // Act
        var act = () => _parser.Parse(fen);

        // Assert
        act.Should().Throw<InvalidGameStateException>().WithMessage("*Failed to parse FEN*");
    }

    // ===== SPECIAL STATES =====

    [Fact]
    public void Parse_WithEnPassant_ShouldStoreTarget()
    {
        // Arrange
        var fen = "rnbqkbnr/ppp1pppp/8/3pP3/8/8/PPPP1PPP/RNBQKBNR w KQkq d6 0 3";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.EnPassantTarget.Should().NotBeNull();
        state.EnPassantTarget!.Notation.Should().Be("d6");
        state.EnPassantTarget.Rank.Should().Be(5);  // Rank 6 = index 5
    }

    [Fact]
    public void Parse_PromotionPosition_ShouldHandleQueens()
    {
        // Arrange - Pawn promoted to queen
        var fen = "rnbqkbnr/ppppQppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        var queens = state.Pieces.Where(p => p.Type == "Queen" && p.Color == "White").ToList();
        queens.Should().HaveCount(2);  // Original + promoted
    }

    [Fact]
    public void Parse_NoCastlingRights_ShouldParseHyphen()
    {
        // Arrange
        var fen = "r1bq1rk1/pp2ppbp/2np1np1/8/2BPP3/2N2N2/PPP2PPP/R1BQ1RK1 w - - 4 8";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.CastlingRights!.WhiteKingside.Should().BeFalse();
        state.CastlingRights.WhiteQueenside.Should().BeFalse();
        state.CastlingRights.BlackKingside.Should().BeFalse();
        state.CastlingRights.BlackQueenside.Should().BeFalse();
        state.CastlingRights.ToFen().Should().Be("-");
    }

    [Fact]
    public void Parse_PartialCastlingRights_ShouldParseCorrectly()
    {
        // Arrange
        var fen = "r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.CastlingRights!.WhiteKingside.Should().BeTrue();
        state.CastlingRights.BlackQueenside.Should().BeTrue();
    }

    [Fact]
    public void Parse_ComplexMiddlegame_ShouldCountPiecesCorrectly()
    {
        // Arrange
        var fen = "r2q1rk1/ppp2ppp/2n1bn2/3p4/3P4/2NBPN2/PPP2PPP/R1BQ1RK1 w - - 0 10";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.Pieces.Should().HaveCount(30);  // 2 pieces captured
        state.ChessBoard!.GetMaterialCount("White").Should().Be(35);  // Approximate
        state.ChessBoard.GetMaterialCount("Black").Should().BeGreaterThan(20);
    }

    [Fact]
    public void Parse_Endgame_TwoKings_ShouldValidate()
    {
        // Arrange
        var fen = "8/5k2/8/8/8/3K4/8/8 w - - 50 75";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.Pieces.Should().HaveCount(2);
        state.HalfmoveClock.Should().Be(50);
        state.IsValidChessState().Should().BeTrue();
    }

    [Fact]
    public void Parse_KingAndPawnEndgame_ShouldParse()
    {
        // Arrange
        var fen = "8/8/8/5k2/5P2/4K3/8/8 w - - 0 60";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.Pieces.Should().HaveCount(3);
        state.ChessBoard!.GetPieceAt("f4")!.Type.Should().Be("Pawn");
        state.ChessBoard.GetPieceAt("e3")!.Type.Should().Be("King");
        state.ChessBoard.GetPieceAt("f5")!.Type.Should().Be("King");
    }

    // ===== BOARD STATE QUERIES =====

    [Fact]
    public void Parse_ShouldAllowBoardQueries()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.ChessBoard!.IsEmpty("d4").Should().BeTrue();
        state.ChessBoard.IsEmpty("e4").Should().BeFalse();
        state.ChessBoard.HasPiece("e4", "White").Should().BeTrue();
        state.ChessBoard.HasPiece("e4", "Black").Should().BeFalse();
        state.ChessBoard.GetPiecesByColor("White").Should().HaveCount(16);
        state.ChessBoard.GetPiecesByColor("Black").Should().HaveCount(16);
    }

    [Fact]
    public void Parse_ShouldFindKings()
    {
        // Arrange
        var fen = "r1bq1rk1/pp2ppbp/2np1np1/8/2BPP3/2N2N2/PPP2PPP/R1BQ1RK1 w - - 4 8";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        var whiteKing = state.ChessBoard!.FindKing("White");
        var blackKing = state.ChessBoard.FindKing("Black");

        whiteKing.Should().NotBeNull();
        whiteKing!.Notation.Should().Be("g1");

        blackKing.Should().NotBeNull();
        blackKing!.Notation.Should().Be("g8");
    }

    // ===== METADATA =====

    [Fact]
    public void Parse_ShouldIncludeMetadata()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        // Act
        var state = _parser.Parse(fen);

        // Assert
        state.Metadata.Should().ContainKey("Format");
        state.Metadata["Format"].Should().Be("FEN");
        state.Metadata.Should().ContainKey("OriginalFEN");
        state.Metadata["OriginalFEN"].Should().Be(fen);
        state.Metadata.Should().ContainKey("Valid");
        state.Metadata["Valid"].Should().Be(true);
        state.ParsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    // ===== ROUND-TRIP =====

    [Fact]
    public void Parse_ThenGetFen_ShouldRoundTrip()
    {
        // Arrange
        var originalFen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1";

        // Act
        var state = _parser.Parse(originalFen);
        var reconstructedFen = state.GetFen();

        // Assert
        reconstructedFen.Should().Be(originalFen);
    }

    [Fact]
    public void Parse_ComplexPosition_RoundTrip()
    {
        // Arrange
        var originalFen = "r1bq1rk1/pp2ppbp/2np1np1/8/2BPP3/2N2N2/PPP2PPP/R1BQ1RK1 w - - 4 8";

        // Act
        var state = _parser.Parse(originalFen);
        var reconstructedFen = state.GetFen();

        // Assert
        reconstructedFen.Should().Be(originalFen);
    }
}
