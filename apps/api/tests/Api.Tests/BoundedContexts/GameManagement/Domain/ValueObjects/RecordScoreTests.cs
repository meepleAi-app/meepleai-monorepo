using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
public class RecordScoreTests
{
    [Fact]
    public void RecordScore_ValidData_CreatesSuccessfully()
    {
        // Act
        var score = new RecordScore("points", 42, "pts");

        // Assert
        Assert.Equal("points", score.Dimension);
        Assert.Equal(42, score.Value);
        Assert.Equal("pts", score.Unit);
    }

    [Fact]
    public void RecordScore_EmptyDimension_ThrowsValidationException()
    {
        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            new RecordScore("", 10));
    }

    [Fact]
    public void RecordScore_DimensionTooLong_ThrowsValidationException()
    {
        // Arrange
        var longDimension = new string('x', 51);

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new RecordScore(longDimension, 10));

        Assert.Contains("cannot exceed 50", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecordScore_NegativeValue_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            new RecordScore("points", -1));

        Assert.Contains("cannot be negative", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Points_FactoryMethod_CreatesCorrectly()
    {
        // Act
        var score = RecordScore.Points(42);

        // Assert
        Assert.Equal("points", score.Dimension);
        Assert.Equal(42, score.Value);
        Assert.Equal("pts", score.Unit);
    }

    [Fact]
    public void Ranking_FactoryMethod_CreatesCorrectly()
    {
        // Act
        var score = RecordScore.Ranking(1);

        // Assert
        Assert.Equal("ranking", score.Dimension);
        Assert.Equal(1, score.Value);
        Assert.Equal("1º", score.Unit);
    }

    [Fact]
    public void Wins_FactoryMethod_CreatesCorrectly()
    {
        // Act
        var score = RecordScore.Wins(3);

        // Assert
        Assert.Equal("wins", score.Dimension);
        Assert.Equal(3, score.Value);
        Assert.Equal("W", score.Unit);
    }

    [Fact]
    public void RecordScore_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var score1 = RecordScore.Points(42);
        var score2 = RecordScore.Points(42);
        var score3 = RecordScore.Points(10);

        // Assert
        Assert.Equal(score1, score2);
        Assert.NotEqual(score1, score3);
    }

    [Fact]
    public void RecordScore_ToString_FormatsCorrectly()
    {
        // Arrange & Act
        var scoreWithUnit = RecordScore.Points(42);
        var scoreWithoutUnit = new RecordScore("custom", 100);

        // Assert
        Assert.Equal("42 pts", scoreWithUnit.ToString());
        Assert.Equal("100", scoreWithoutUnit.ToString());
    }
}
