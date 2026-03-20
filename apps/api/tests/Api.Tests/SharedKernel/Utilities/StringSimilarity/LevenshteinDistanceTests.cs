using Api.SharedKernel.Utilities.StringSimilarity;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.SharedKernel.Utilities.StringSimilarity;

/// <summary>
/// Unit tests for LevenshteinDistance utility class.
/// Tests edit distance calculation and similarity scoring for fuzzy string matching.
/// Issue #4158: Backend - Duplicate Detection Enhancement
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LevenshteinDistanceTests
{
    [Theory]
    [InlineData("", "", 0)] // Both empty
    [InlineData("abc", "", 3)] // Source to empty
    [InlineData("", "abc", 3)] // Empty to target
    [InlineData("abc", "abc", 0)] // Identical
    [InlineData("ABC", "abc", 0)] // Case insensitive
    [InlineData("kitten", "sitting", 3)] // Classic example: k→s, e→i, insert g
    [InlineData("Saturday", "Sunday", 3)] // atur → un
    [InlineData("Catan", "Catane", 1)] // Game title: insert e
    [InlineData("Monopoly", "Monopoli", 1)] // Game title: y→i
    [InlineData("Chess", "Checkers", 4)] // Very different (4 edits: e→e, s→c, s→k, +ers)
    public void CalculateDistance_WithVariousInputs_ReturnsCorrectEditDistance(
        string source,
        string target,
        int expectedDistance)
    {
        // Act
        var actualDistance = LevenshteinDistance.CalculateDistance(source, target);

        // Assert
        actualDistance.Should().Be(expectedDistance);
    }

    [Theory]
    [InlineData("", "", 100.0)] // Both empty = identical
    [InlineData("abc", "", 0.0)] // Source to empty = no similarity
    [InlineData("", "abc", 0.0)] // Empty to target = no similarity
    [InlineData("abc", "abc", 100.0)] // Identical
    [InlineData("ABC", "abc", 100.0)] // Case insensitive identical
    [InlineData("Catan", "Catane", 83.3)] // 1 edit in 6 chars: (1 - 1/6) * 100 = 83.3%
    [InlineData("Monopoly", "Monopoli", 87.5)] // 1 edit in 8 chars: (1 - 1/8) * 100 = 87.5%
    [InlineData("The Settlers of Catan", "Settlers of Catan", 81.0)] // 4 edits in 21 chars: (1 - 4/21) * 100 = 81.0%
    [InlineData("Chess", "Checkers", 50.0)] // 4 edits in 8 chars: (1 - 4/8) * 100 = 50%
    public void CalculateSimilarityScore_WithVariousInputs_ReturnsCorrectPercentage(
        string source,
        string target,
        double expectedSimilarity)
    {
        // Act
        var actualSimilarity = LevenshteinDistance.CalculateSimilarityScore(source, target);

        // Assert
        actualSimilarity.Should().BeApproximately(expectedSimilarity, 1);
    }

    [Fact]
    public void CalculateSimilarityScore_WithIdenticalStrings_Returns100Percent()
    {
        // Arrange
        var text = "7 Wonders";

        // Act
        var similarity = LevenshteinDistance.CalculateSimilarityScore(text, text);

        // Assert
        similarity.Should().Be(100.0);
    }

    [Fact]
    public void CalculateSimilarityScore_WithCompletelyDifferentStrings_ReturnsLowScore()
    {
        // Arrange
        var source = "Azul";
        var target = "Ticket to Ride";

        // Act
        var similarity = LevenshteinDistance.CalculateSimilarityScore(source, target);

        // Assert
        (similarity < 30.0).Should().BeTrue($"Expected similarity < 30%, got {similarity}%");
    }

    [Theory]
    [InlineData("   ", "   ", 100.0)] // Whitespace = empty = identical
    public void CalculateSimilarityScore_WithWhitespace_HandlesCorrectly(
        string source,
        string target,
        double expectedSimilarity)
    {
        // Act
        var actualSimilarity = LevenshteinDistance.CalculateSimilarityScore(source, target);

        // Assert
        actualSimilarity.Should().BeApproximately(expectedSimilarity, 1);
    }

    [Fact]
    public void CalculateDistance_IsSymmetric()
    {
        // Arrange
        var s1 = "Pandemic";
        var s2 = "Pandemic Legacy";

        // Act
        var distance1 = LevenshteinDistance.CalculateDistance(s1, s2);
        var distance2 = LevenshteinDistance.CalculateDistance(s2, s1);

        // Assert
        distance2.Should().Be(distance1);
    }

    [Theory]
    [InlineData("Carcassonne", "Carcasonne", 90.9)] // Common typo: double s
    [InlineData("Terraforming Mars", "Terra Forming Mars", 94.4)] // 1 space edit in 18 chars
    [InlineData("Gloomhaven", "Gloomhaven: Jaws of the Lion", 35.7)] // 18 edits in 28 chars
    public void CalculateSimilarityScore_WithRealGameTitles_ReturnsReasonableScores(
        string source,
        string target,
        double expectedSimilarity)
    {
        // Act
        var actualSimilarity = LevenshteinDistance.CalculateSimilarityScore(source, target);

        // Assert
        actualSimilarity.Should().BeApproximately(expectedSimilarity, 1);
    }
}
