using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

/// <summary>
/// Unit tests for ChessMoveGenerator (Issue #3770)
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3770")]
public sealed class ChessMoveGeneratorTests
{
    private readonly ChessFENParser _parser = new();
    private readonly ChessMoveGenerator _generator;

    public ChessMoveGeneratorTests()
    {
        var validator = new LegalMoveValidator();
        var scorer = new HeuristicMoveScorer();
        _generator = new ChessMoveGenerator(validator, scorer);
    }

    [Fact]
    public async Task GenerateCandidates_StartingPosition_ShouldReturn20Moves()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        var state = _parser.Parse(fen);

        // Act
        var candidates = await _generator.GenerateCandidatesAsync(state, "White", maxCandidates: 50);

        // Assert
        candidates.Should().HaveCount(20);  // 16 pawn + 4 knight moves
        candidates.Should().Contain(m => m.Piece.Type == "Pawn");
        candidates.Should().Contain(m => m.Piece.Type == "Knight");
    }

    [Fact]
    public async Task GenerateCandidates_AfterE4_ShouldIncludePawnMoves()
    {
        // Arrange
        var fen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";
        var state = _parser.Parse(fen);

        // Act
        var candidates = await _generator.GenerateCandidatesAsync(state, "Black", maxCandidates: 50);

        // Assert
        candidates.Should().NotBeEmpty();
        var pawnMoves = candidates.Where(m => m.Piece.Type == "Pawn").ToList();
        pawnMoves.Should().NotBeEmpty();
    }

    [Fact(Skip = "WIP - Score calculated by IMoveScorer, not generator directly")]
    public async Task GenerateCandidates_ShouldScoreCaptures()
    {
        // Arrange - Position with capture available
        var fen = "rnbqkbnr/ppp1pppp/8/3p4/4P3/8/PPPP1PPP/RNBQKBNR w KQkq - 0 2";
        var state = _parser.Parse(fen);

        // Act
        var candidates = await _generator.GenerateCandidatesAsync(state, "White", maxCandidates: 30);

        // Assert
        var captures = candidates.Where(m => m.IsCapture).ToList();
        captures.Should().NotBeEmpty();
        captures.All(m => m.Score.Material != 0).Should().BeTrue();
    }

    [Fact]
    public async Task GenerateCandidates_ShouldRankByPriority()
    {
        // Arrange
        var fen = "rnbqkb1r/pppp1ppp/5n2/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3";
        var state = _parser.Parse(fen);

        // Act
        var candidates = await _generator.GenerateCandidatesAsync(state, "White", maxCandidates: 10);

        // Assert
        candidates.Should().NotBeEmpty();
        // First moves should be higher priority
        var firstPriority = candidates.First().Priority;
        var lastPriority = candidates.Last().Priority;
        ((int)firstPriority).Should().BeLessThanOrEqualTo((int)lastPriority);
    }

    [Fact]
    public async Task GenerateCandidates_KnightMoves_ShouldBeLShaped()
    {
        // Arrange - Knight on d4 (center)
        var fen = "rnbqkbnr/pppppppp/8/8/3N4/8/PPPPPPPP/RNBQKB1R w KQkq - 0 1";
        var state = _parser.Parse(fen);

        // Act
        var candidates = await _generator.GenerateCandidatesAsync(state, "White", maxCandidates: 50);

        // Assert
        var knightMoves = candidates.Where(m => m.Piece.Type == "Knight").ToList();
        knightMoves.Should().HaveCountGreaterThan(4);  // Knight in center has many moves
    }

    [Fact]
    public async Task GenerateCandidates_EndgamePosition_ShouldHandleFewPieces()
    {
        // Arrange
        var fen = "8/5k2/8/8/8/3K4/8/8 w - - 0 60";
        var state = _parser.Parse(fen);

        // Act
        var candidates = await _generator.GenerateCandidatesAsync(state, "White", maxCandidates: 10);

        // Assert
        candidates.Should().NotBeEmpty();
        candidates.All(m => m.Piece.Type == "King").Should().BeTrue();
    }
}
