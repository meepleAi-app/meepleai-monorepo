using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for ChessBoard value object (Issue #3772)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3772")]
public sealed class ChessBoardTests
{
    [Fact]
    public void Empty_ShouldCreate8x8Grid()
    {
        // Act
        var board = ChessBoard.Empty();

        // Assert
        board.Grid.GetLength(0).Should().Be(8);
        board.Grid.GetLength(1).Should().Be(8);
        board.GetAllPieces().Should().BeEmpty();
    }

    [Fact]
    public void GetPieceAt_WithValidPosition_ShouldReturnPiece()
    {
        // Arrange
        var grid = new ChessPiece?[8, 8];
        var piece = ChessPiece.Create("Pawn", "White", "e4");
        grid[3, 4] = piece;  // e4 = rank 4 (index 3), file e (index 4)
        var board = ChessBoard.Create(grid);

        // Act
        var result = board.GetPieceAt("e4");

        // Assert
        result.Should().NotBeNull();
        result!.Type.Should().Be("Pawn");
        result.Color.Should().Be("White");
    }

    [Fact]
    public void GetPiecesByColor_ShouldFilterCorrectly()
    {
        // Arrange
        var grid = new ChessPiece?[8, 8];
        grid[0, 0] = ChessPiece.Create("Rook", "White", "a1");
        grid[0, 7] = ChessPiece.Create("Rook", "White", "h1");
        grid[7, 0] = ChessPiece.Create("Rook", "Black", "a8");
        var board = ChessBoard.Create(grid);

        // Act
        var whitePieces = board.GetPiecesByColor("White");
        var blackPieces = board.GetPiecesByColor("Black");

        // Assert
        whitePieces.Should().HaveCount(2);
        blackPieces.Should().HaveCount(1);
    }

    [Fact]
    public void FindKing_ShouldReturnCorrectPosition()
    {
        // Arrange
        var grid = new ChessPiece?[8, 8];
        grid[0, 4] = ChessPiece.Create("King", "White", "e1");
        grid[7, 4] = ChessPiece.Create("King", "Black", "e8");
        var board = ChessBoard.Create(grid);

        // Act
        var whiteKing = board.FindKing("White");
        var blackKing = board.FindKing("Black");

        // Assert
        whiteKing.Should().NotBeNull();
        whiteKing!.Notation.Should().Be("e1");

        blackKing.Should().NotBeNull();
        blackKing!.Notation.Should().Be("e8");
    }

    [Fact]
    public void FindKing_WhenNoKing_ShouldReturnNull()
    {
        // Arrange
        var board = ChessBoard.Empty();

        // Act
        var king = board.FindKing("White");

        // Assert
        king.Should().BeNull();
    }

    [Fact]
    public void GetMaterialCount_ShouldSumPieceValues()
    {
        // Arrange
        var grid = new ChessPiece?[8, 8];
        grid[0, 0] = ChessPiece.Create("Queen", "White", "a1");   // 9
        grid[0, 1] = ChessPiece.Create("Rook", "White", "b1");    // 5
        grid[0, 2] = ChessPiece.Create("Knight", "White", "c1");  // 3
        grid[0, 3] = ChessPiece.Create("Pawn", "White", "d1");    // 1
        var board = ChessBoard.Create(grid);

        // Act
        var materialCount = board.GetMaterialCount("White");

        // Assert
        materialCount.Should().Be(18);  // 9+5+3+1
    }

    [Fact]
    public void IsEmpty_ShouldReturnCorrectValue()
    {
        // Arrange
        var grid = new ChessPiece?[8, 8];
        grid[3, 4] = ChessPiece.Create("Pawn", "White", "e4");
        var board = ChessBoard.Create(grid);

        // Act & Assert
        board.IsEmpty("e4").Should().BeFalse();
        board.IsEmpty("d4").Should().BeTrue();
    }

    [Fact]
    public void HasPiece_ShouldCheckColorCorrectly()
    {
        // Arrange
        var grid = new ChessPiece?[8, 8];
        grid[3, 4] = ChessPiece.Create("Pawn", "White", "e4");
        var board = ChessBoard.Create(grid);

        // Act & Assert
        board.HasPiece("e4", "White").Should().BeTrue();
        board.HasPiece("e4", "Black").Should().BeFalse();
        board.HasPiece("d4", "White").Should().BeFalse();
    }
}
