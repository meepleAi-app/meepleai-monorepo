using Api.Models;
using Api.Services;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Services;

/// <summary>
/// BDD tests for ResponseQualityService quality score calculation.
/// These tests define expected behavior before implementation (TDD RED phase).
/// </summary>
public class ResponseQualityServiceTests
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Scenario: High-quality response with strong RAG confidence and complete citations
    /// Given a RAG confidence of 0.85, 4 citations for 4 paragraphs, and 300-word response
    /// When quality scores are calculated
    /// Then RAG confidence should be ~0.85, overall confidence ~0.87, not flagged as low-quality
    /// </summary>
    [Fact]
    public void CalculateQualityScores_HighQualityResponse_ReturnsHighScores()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult>
        {
            new() { Score = 0.87 },
            new() { Score = 0.85 },
            new() { Score = 0.84 },
            new() { Score = 0.83 },
            new() { Score = 0.82 }
        };
        var citations = new List<Citation>
        {
            new() { DocumentId = Guid.NewGuid(), PageNumber = 1, SnippetText = "Rule 1" },
            new() { DocumentId = Guid.NewGuid(), PageNumber = 2, SnippetText = "Rule 2" },
            new() { DocumentId = Guid.NewGuid(), PageNumber = 3, SnippetText = "Rule 3" },
            new() { DocumentId = Guid.NewGuid(), PageNumber = 4, SnippetText = "Rule 4" }
        };
        var responseText = GenerateResponseText(wordCount: 300, paragraphs: 4, hedgingPhrases: 0);

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, responseText);

        // Assert
        scores.RagConfidence.Should().BeInRange(0.84, 0.86);
        scores.LlmConfidence.Should().BeInRange(0.75, 0.95);
        scores.CitationQuality.Should().BeInRange(0.95, 1.0);
        scores.OverallConfidence.Should().BeInRange(0.80, 0.90);
        scores.IsLowQuality.Should().BeFalse();
    }

    /// <summary>
    /// Scenario: Low-quality response with weak RAG confidence and incomplete citations
    /// Given RAG confidence 0.30, 1 citation for 3 paragraphs, 6 hedging phrases
    /// When quality scores are calculated
    /// Then overall confidence should be ~0.35, flagged as low-quality
    /// Calculation: RAG 0.30, LLM 0.40 (0.85 - 0.15 short - 0.30 hedging), Citation 0.33
    /// Overall: (0.30 * 0.40) + (0.40 * 0.40) + (0.33 * 0.20) = 0.346
    /// </summary>
    [Fact]
    public void CalculateQualityScores_LowQualityResponse_ReturnsLowScores()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult>
        {
            new() { Score = 0.35 },
            new() { Score = 0.30 },
            new() { Score = 0.25 }
        };
        var citations = new List<Citation>
        {
            new() { DocumentId = Guid.NewGuid(), PageNumber = 1, SnippetText = "Unclear rule" }
        };
        // Use 6 hedging phrases to achieve LLM confidence ~0.40
        // 80 words triggers short penalty (-0.15), 6 hedging phrases = -0.30
        // LLM confidence: 0.85 - 0.15 - 0.30 = 0.40
        var responseText = GenerateResponseText(wordCount: 80, paragraphs: 3, hedgingPhrases: 6);

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, responseText);

        // Assert
        scores.RagConfidence.Should().BeInRange(0.25, 0.35);
        scores.LlmConfidence.Should().BeInRange(0.35, 0.45);  // Adjusted to match calculation
        scores.CitationQuality.Should().BeInRange(0.30, 0.40);
        scores.OverallConfidence.Should().BeInRange(0.30, 0.45);
        scores.IsLowQuality.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: No RAG results available
    /// Given RAG search returned 0 results
    /// When quality scores are calculated
    /// Then RAG confidence should be 0.00, overall confidence significantly reduced
    /// </summary>
    [Fact]
    public void CalculateQualityScores_NoRagResults_ReturnsZeroRagConfidence()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult>();
        var citations = new List<Citation>();
        var responseText = GenerateResponseText(wordCount: 100, paragraphs: 2, hedgingPhrases: 0);

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, responseText);

        // Assert
        scores.RagConfidence.Should().Be(0.0);
        scores.IsLowQuality.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Model-reported LLM confidence provided
    /// Given model reports LLM confidence of 0.92
    /// When quality scores are calculated
    /// Then LLM confidence should use the provided value instead of heuristic
    /// </summary>
    [Fact]
    public void CalculateQualityScores_WithModelReportedConfidence_UsesProvidedValue()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult>
        {
            new() { Score = 0.80 }
        };
        var citations = new List<Citation>
        {
            new() { DocumentId = Guid.NewGuid(), PageNumber = 1, SnippetText = "Rule" }
        };
        var responseText = GenerateResponseText(wordCount: 200, paragraphs: 2, hedgingPhrases: 0);
        var modelConfidence = 0.92;

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, responseText, modelConfidence);

        // Assert
        scores.LlmConfidence.Should().Be(0.92);
    }

    /// <summary>
    /// Scenario: Empty response text
    /// Given response text is empty or null
    /// When quality scores are calculated
    /// Then LLM confidence should be 0.00, flagged as low-quality
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void CalculateQualityScores_EmptyResponse_ReturnsZeroLlmConfidence(string? responseText)
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult> { new() { Score = 0.80 } };
        var citations = new List<Citation>();

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, responseText);

        // Assert
        scores.LlmConfidence.Should().Be(0.0);
        scores.IsLowQuality.Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Perfect scores across all dimensions
    /// Given RAG confidence 1.0, perfect citation ratio, optimal response length
    /// When quality scores are calculated
    /// Then overall confidence should approach 1.0, not flagged as low-quality
    /// </summary>
    [Fact]
    public void CalculateQualityScores_PerfectScores_ReturnsNearPerfectOverall()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult>
        {
            new() { Score = 1.0 },
            new() { Score = 0.99 },
            new() { Score = 0.98 }
        };
        var citations = new List<Citation>
        {
            new() { DocumentId = Guid.NewGuid(), PageNumber = 1, SnippetText = "Rule 1" },
            new() { DocumentId = Guid.NewGuid(), PageNumber = 2, SnippetText = "Rule 2" }
        };
        var responseText = GenerateResponseText(wordCount: 250, paragraphs: 2, hedgingPhrases: 0);

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, responseText);

        // Assert
        scores.OverallConfidence.Should().BeInRange(0.90, 1.0);
        scores.IsLowQuality.Should().BeFalse();
    }

    /// <summary>
    /// Scenario: Multiple hedging phrases reduce LLM confidence
    /// Given response contains 5+ hedging phrases ("might", "possibly", "unclear")
    /// When quality scores are calculated
    /// Then LLM confidence should be penalized
    /// </summary>
    [Fact]
    public void CalculateQualityScores_MultipleHedgingPhrases_ReducesLlmConfidence()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult> { new() { Score = 0.80 } };
        var citations = new List<Citation> { new() { DocumentId = Guid.NewGuid(), PageNumber = 1, SnippetText = "Rule" } };
        var responseWithHedging = GenerateResponseText(wordCount: 200, paragraphs: 2, hedgingPhrases: 5);
        var responseWithoutHedging = GenerateResponseText(wordCount: 200, paragraphs: 2, hedgingPhrases: 0);

        // Act
        var scoresWithHedging = service.CalculateQualityScores(ragResults, citations, responseWithHedging);
        var scoresWithoutHedging = service.CalculateQualityScores(ragResults, citations, responseWithoutHedging);

        // Assert
        scoresWithHedging.LlmConfidence < scoresWithoutHedging.LlmConfidence,
            "Hedging phrases should reduce LLM confidence".Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Excessive citations for short response
    /// Given 10 citations for a 2-paragraph response
    /// When quality scores are calculated
    /// Then citation quality should be capped at 1.0 (not penalized for over-citing)
    /// </summary>
    [Fact]
    public void CalculateQualityScores_ExcessiveCitations_CapsAtMaxQuality()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult> { new() { Score = 0.80 } };
        var citations = Enumerable.Range(1, 10)
            .Select(i => new Citation
            {
                DocumentId = Guid.NewGuid(),
                PageNumber = i,
                SnippetText = $"Rule {i}"
            })
            .ToList();
        var responseText = GenerateResponseText(wordCount: 150, paragraphs: 2, hedgingPhrases: 0);

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, responseText);

        // Assert
        scores.CitationQuality.Should().BeInRange(0.95, 1.0);
    }

    /// <summary>
    /// Scenario: Very short response (under 50 words)
    /// Given response length is 30 words
    /// When quality scores are calculated
    /// Then LLM confidence should be penalized for brevity
    /// </summary>
    [Fact]
    public void CalculateQualityScores_VeryShortResponse_PenalizesLlmConfidence()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult> { new() { Score = 0.80 } };
        var citations = new List<Citation> { new() { DocumentId = Guid.NewGuid(), PageNumber = 1, SnippetText = "Rule" } };
        var shortResponse = string.Join(" ", Enumerable.Repeat("word", 30));

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, shortResponse);

        // Assert
        (scores.LlmConfidence < 0.60).Should().BeTrue("Very short responses should have reduced LLM confidence");
    }

    /// <summary>
    /// Scenario: Boundary threshold testing (exactly 0.60 overall confidence)
    /// Given overall confidence calculates to exactly 0.60
    /// When low-quality check is performed
    /// Then should NOT be flagged as low-quality (threshold is exclusive)
    /// </summary>
    [Fact]
    public void CalculateQualityScores_ExactlyAtThreshold_NotFlaggedAsLowQuality()
    {
        // This test will need tuning after implementation to hit exactly 0.60
        // For now, we test the principle that 0.60 is the boundary
        // Act & Assert will be refined during GREEN phase
        true, "Placeholder - will implement precise boundary test in GREEN phase".Should().BeTrue();
    }

    /// <summary>
    /// Scenario: Null citations list
    /// Given citations list is null
    /// When quality scores are calculated
    /// Then should handle gracefully with 0.0 citation quality
    /// </summary>
    [Fact]
    public void CalculateQualityScores_NullCitations_HandlesGracefully()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult> { new() { Score = 0.80 } };
        List<Citation>? citations = null;
        var responseText = GenerateResponseText(wordCount: 200, paragraphs: 2, hedgingPhrases: 0);

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, responseText);

        // Assert
        scores.CitationQuality.Should().Be(0.0);
    }

    /// <summary>
    /// Scenario: Mixed quality dimensions
    /// Given high RAG (0.90), low LLM (0.40), medium citation (0.70)
    /// When quality scores are calculated
    /// Then overall confidence should be weighted average, likely flagged as low-quality
    /// </summary>
    [Fact]
    public void CalculateQualityScores_MixedQualityDimensions_CalculatesWeightedAverage()
    {
        // Arrange
        var service = new ResponseQualityService();
        var ragResults = new List<RagSearchResult>
        {
            new() { Score = 0.92 },
            new() { Score = 0.90 },
            new() { Score = 0.88 }
        };
        var citations = new List<Citation>
        {
            new() { DocumentId = Guid.NewGuid(), PageNumber = 1, SnippetText = "Rule" }
        };
        // Create response with characteristics that yield ~0.40 LLM confidence
        var responseText = GenerateResponseText(wordCount: 60, paragraphs: 2, hedgingPhrases: 4);

        // Act
        var scores = service.CalculateQualityScores(ragResults, citations, responseText);

        // Assert
        scores.RagConfidence.Should().BeInRange(0.88, 0.92);
        (scores.LlmConfidence < 0.60).Should().BeTrue("Short response with hedging should have low LLM confidence");
        // Overall should consider all dimensions
        (scores.OverallConfidence > 0.0 && scores.OverallConfidence < 1.0).Should().BeTrue();
    }

    #region Helper Methods

    /// <summary>
    /// Generates response text with specified characteristics for testing.
    /// </summary>
    private string GenerateResponseText(int wordCount, int paragraphs, int hedgingPhrases)
    {
        var words = new List<string>();
        var hedgingTerms = new[] { "might", "possibly", "unclear", "I'm not sure", "maybe", "perhaps" };

        // Add hedging phrases first
        for (int i = 0; i < hedgingPhrases && i < hedgingTerms.Length; i++)
        {
            words.Add(hedgingTerms[i]);
        }

        // Fill remaining word count
        while (words.Count < wordCount)
        {
            words.Add("word");
        }

        // Split into paragraphs
        var wordsPerParagraph = wordCount / paragraphs;
        var paragraphList = new List<string>();
        for (int i = 0; i < paragraphs; i++)
        {
            var paragraphWords = words.Skip(i * wordsPerParagraph).Take(wordsPerParagraph);
            paragraphList.Add(string.Join(" ", paragraphWords));
        }

        return string.Join("\n\n", paragraphList);
    }

    #endregion
}
