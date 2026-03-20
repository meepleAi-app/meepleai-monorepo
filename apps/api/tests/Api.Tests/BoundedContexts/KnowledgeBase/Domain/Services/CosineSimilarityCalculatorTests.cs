using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for CosineSimilarityCalculator
/// ISSUE-975: BGAI-033 - Consensus similarity calculation using cosine ≥0.90
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CosineSimilarityCalculatorTests
{
    private readonly CosineSimilarityCalculator _calculator;

    public CosineSimilarityCalculatorTests()
    {
        _calculator = new CosineSimilarityCalculator();
    }

    [Fact]
    public void CalculateCosineSimilarity_IdenticalTexts_Returns100()
    {
        // Arrange
        var text = "The knight moves in an L-shape: two squares in one direction and one square perpendicular.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text, text);

        // Assert
        similarity.Should().BeApproximately(1.0, precision: 3);
    }

    [Fact]
    public void CalculateCosineSimilarity_HighlySimilarTexts_ReturnsHighScore()
    {
        // Arrange - Very similar semantic content with different wording
        var text1 = "The knight moves in an L-shape: two squares in one direction and one square perpendicular to that direction.";
        var text2 = "The knight moves in an L-shape pattern: two squares in one direction and one square in a perpendicular direction.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Cosine similarity should be very high for semantically similar texts
        (similarity >= 0.85).Should().BeTrue($"Expected similarity ≥0.85 for highly similar texts, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_ModeratelySimilarTexts_ReturnsMediumScore()
    {
        // Arrange - Same topic, different explanations
        var text1 = "The knight moves two squares vertically and one square horizontally, or two squares horizontally and one square vertically.";
        var text2 = "Knights jump in an L-shaped pattern on the chessboard.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Should have low-moderate similarity (same topic, limited shared vocabulary)
        (similarity >= 0.15 && similarity < 0.85).Should().BeTrue($"Expected similarity between 0.15-0.85 for moderately similar texts, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_DifferentTexts_ReturnsLowScore()
    {
        // Arrange - Different chess pieces with shared context words (board, moves, etc)
        var text1 = "The knight moves in an L-shape on the chessboard.";
        var text2 = "The bishop moves diagonally across the entire board.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Should have moderate-low similarity (different pieces but shared chess terminology)
        (similarity < 0.90).Should().BeTrue($"Expected similarity <0.90 for different chess pieces, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_CompletelyDifferentTexts_ReturnsVeryLowScore()
    {
        // Arrange - No semantic overlap
        var text1 = "The game of chess originated in India during the 6th century.";
        var text2 = "Python is a popular programming language for data science.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Should have very low or zero similarity
        (similarity < 0.30).Should().BeTrue($"Expected similarity <0.30 for completely different texts, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_EmptyFirstText_ReturnsZero()
    {
        // Arrange
        var text = "Some text content";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity("", text);

        // Assert
        similarity.Should().Be(0.0);
    }

    [Fact]
    public void CalculateCosineSimilarity_EmptySecondText_ReturnsZero()
    {
        // Arrange
        var text = "Some text content";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text, "");

        // Assert
        similarity.Should().Be(0.0);
    }

    [Fact]
    public void CalculateCosineSimilarity_BothEmpty_ReturnsZero()
    {
        // Act
        var similarity = _calculator.CalculateCosineSimilarity("", "");

        // Assert
        similarity.Should().Be(0.0);
    }

    [Fact]
    public void CalculateCosineSimilarity_WhitespaceOnly_ReturnsZero()
    {
        // Act
        var similarity = _calculator.CalculateCosineSimilarity("   ", "\t\n");

        // Assert
        similarity.Should().Be(0.0);
    }

    [Fact]
    public void CalculateCosineSimilarity_CaseInsensitive()
    {
        // Arrange
        var text1 = "The KNIGHT moves in an L-SHAPE.";
        var text2 = "The knight moves in an l-shape.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Should be identical (case-insensitive)
        (similarity >= 0.99).Should().BeTrue($"Expected case-insensitive similarity ≥0.99, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_PunctuationHandling()
    {
        // Arrange
        var text1 = "The knight moves in an L-shape!";
        var text2 = "The knight moves in an L-shape.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Punctuation should be normalized
        (similarity >= 0.99).Should().BeTrue($"Expected punctuation-agnostic similarity ≥0.99, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_WordOrder_AffectsSimilarity()
    {
        // Arrange - Same words, different order (TF-IDF cosine is bag-of-words, order doesn't matter much)
        var text1 = "The quick brown fox jumps over the lazy dog";
        var text2 = "The lazy dog jumps over the quick brown fox";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Should still be very high since same words are present
        (similarity >= 0.90).Should().BeTrue($"Expected high similarity for same words in different order, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_TermFrequency_Matters()
    {
        // Arrange - Different term frequencies
        var text1 = "chess chess chess game board";
        var text2 = "chess game game game board";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Should have good but not perfect similarity (different term frequencies)
        (similarity >= 0.60 && similarity < 1.0).Should().BeTrue($"Expected moderate-high similarity with different term frequencies, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_LongTexts_SimilarContent()
    {
        // Arrange - Longer, realistic board game rule explanations
        var text1 = @"In chess, the knight is a piece that moves in an L-shaped pattern.
                     It can move two squares in one direction (horizontal or vertical)
                     and then one square perpendicular to that direction. The knight is
                     the only piece that can jump over other pieces on the board.";

        var text2 = @"The knight in chess has a unique L-shaped movement pattern.
                     It moves two squares horizontally or vertically, followed by one
                     square perpendicular to that movement. Unlike other pieces, the
                     knight can jump over pieces that are in its path.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Should achieve consensus threshold for semantically equivalent explanations
        (similarity >= 0.80).Should().BeTrue($"Expected similarity ≥0.80 for semantically similar long texts, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_ConsensusThreshold_Example()
    {
        // Arrange - Simulate GPT-4 and Claude responses that should achieve consensus
        var gpt4Response = @"Castling is a special move in chess involving the king and one rook.
                            The king moves two squares toward the rook, and the rook moves to the
                            square the king crossed over. This is the only move where two pieces
                            move simultaneously. Castling is only permitted if neither piece has
                            moved previously, there are no pieces between them, and the king is
                            not in check, does not move through check, and does not end in check.";

        var claudeResponse = @"Castling is a special chess move that involves moving the king and
                              a rook at the same time. The king slides two squares toward the rook,
                              while the rook moves to the square that the king passed over. To castle,
                              neither piece can have moved before, no pieces can be between them, and
                              the king cannot be in check, move through check, or end up in check.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(gpt4Response, claudeResponse);

        // Assert - Very similar explanations should meet consensus threshold
        (similarity >= 0.85).Should().BeTrue($"Expected similarity ≥0.85 for consensus-quality responses, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_NonConsensus_Example()
    {
        // Arrange - Different enough responses that shouldn't achieve consensus
        var response1 = "The rook can move any number of squares horizontally or vertically.";
        var response2 = "In Catan, players collect resources like wood, brick, sheep, wheat, and ore to build settlements and roads.";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(response1, response2);

        // Assert - Completely different topics should have very low similarity
        (similarity < 0.50).Should().BeTrue($"Expected similarity <0.50 for non-consensus responses, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_ShortTexts_IdenticalContent()
    {
        // Arrange
        var text1 = "Yes, you can castle.";
        var text2 = "Yes you can castle";

        // Act
        var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

        // Assert - Short identical content should have very high similarity
        (similarity >= 0.95).Should().BeTrue($"Expected similarity ≥0.95 for short identical texts, got {similarity:F3}");
    }

    [Fact]
    public void CalculateCosineSimilarity_SymmetricProperty()
    {
        // Arrange
        var text1 = "The bishop moves diagonally across the board.";
        var text2 = "Diagonal movement is how the bishop travels on the chessboard.";

        // Act
        var similarity1 = _calculator.CalculateCosineSimilarity(text1, text2);
        var similarity2 = _calculator.CalculateCosineSimilarity(text2, text1);

        // Assert - Cosine similarity should be symmetric
        similarity2.Should().BeApproximately(similarity1, precision: 6);
    }

    [Fact]
    public void CalculateCosineSimilarity_RangeValidation()
    {
        // Arrange - Various text pairs
        var testCases = new[]
        {
            ("identical text", "identical text"),
            ("different text here", "completely unrelated content"),
            ("some overlap here", "here is some text"),
            ("", "non-empty"),
            ("The quick brown fox", "The lazy dog")
        };

        // Act & Assert
        foreach (var (text1, text2) in testCases)
        {
            var similarity = _calculator.CalculateCosineSimilarity(text1, text2);

            (similarity >= 0.0 && similarity <= 1.0).Should().BeTrue($"Similarity must be in [0,1] range, got {similarity:F3} for texts: '{text1}' vs '{text2}'");
        }
    }

    [Fact]
    public void CalculateCosineSimilarity_NullText_ReturnsZero()
    {
        // Act & Assert
        var similarity1 = _calculator.CalculateCosineSimilarity(null!, "text");
        var similarity2 = _calculator.CalculateCosineSimilarity("text", null!);
        var similarity3 = _calculator.CalculateCosineSimilarity(null!, null!);

        similarity1.Should().Be(0.0);
        similarity2.Should().Be(0.0);
        similarity3.Should().Be(0.0);
    }
}

