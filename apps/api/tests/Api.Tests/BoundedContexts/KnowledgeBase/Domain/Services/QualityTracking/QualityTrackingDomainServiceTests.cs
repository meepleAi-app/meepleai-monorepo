using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.QualityTracking;

/// <summary>
/// Tests for the QualityTrackingDomainService.
/// Issue #3025: Backend 90% Coverage Target - Phase 14
/// </summary>
[Trait("Category", "Unit")]
public sealed class QualityTrackingDomainServiceTests
{
    private readonly QualityTrackingDomainService _service;

    public QualityTrackingDomainServiceTests()
    {
        _service = new QualityTrackingDomainService();
    }

    #region CalculateSearchConfidence Tests

    [Fact]
    public void CalculateSearchConfidence_WithNullResults_ReturnsZero()
    {
        // Act
        var result = _service.CalculateSearchConfidence(null!);

        // Assert
        result.Value.Should().Be(0);
    }

    [Fact]
    public void CalculateSearchConfidence_WithEmptyResults_ReturnsZero()
    {
        // Act
        var result = _service.CalculateSearchConfidence(new List<SearchResult>());

        // Assert
        result.Value.Should().Be(0);
    }

    [Fact]
    public void CalculateSearchConfidence_WithSingleResult_ReturnsResultScore()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            CreateSearchResult(1, 0.8)
        };

        // Act
        var result = _service.CalculateSearchConfidence(results);

        // Assert
        result.Value.Should().BeApproximately(0.8, 0.01);
    }

    [Fact]
    public void CalculateSearchConfidence_WithMultipleResults_ReturnsWeightedAverage()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            CreateSearchResult(1, 0.9), // weight = 1
            CreateSearchResult(2, 0.8), // weight = 0.5
            CreateSearchResult(3, 0.7)  // weight = 0.33
        };

        // Act
        var result = _service.CalculateSearchConfidence(results);

        // Assert
        // Weighted average: (0.9*1 + 0.8*0.5 + 0.7*0.33) / (1 + 0.5 + 0.33)
        result.Value.Should().BeGreaterThan(0.7);
        result.Value.Should().BeLessThan(0.9);
    }

    [Fact]
    public void CalculateSearchConfidence_WithMoreThanFiveResults_OnlyUsesTopFive()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            CreateSearchResult(1, 0.9),
            CreateSearchResult(2, 0.85),
            CreateSearchResult(3, 0.8),
            CreateSearchResult(4, 0.75),
            CreateSearchResult(5, 0.7),
            CreateSearchResult(6, 0.1), // Should be ignored
            CreateSearchResult(7, 0.05) // Should be ignored
        };

        // Act
        var result = _service.CalculateSearchConfidence(results);

        // Assert - score should be based on top 5 only (high scores)
        result.Value.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void CalculateSearchConfidence_WeightsHigherRankedResultsMore()
    {
        // Arrange - All same score but different ranks
        var results = new List<SearchResult>
        {
            CreateSearchResult(1, 0.9),
            CreateSearchResult(2, 0.5),
            CreateSearchResult(3, 0.3)
        };

        // Act
        var result = _service.CalculateSearchConfidence(results);

        // Assert - Should be closer to 0.9 due to weighting
        result.Value.Should().BeGreaterThan(0.6);
    }

    #endregion

    #region CalculateLlmConfidence Tests

    [Fact]
    public void CalculateLlmConfidence_WithEmptyResponse_ReturnsZero()
    {
        // Act
        var result = _service.CalculateLlmConfidence("", new List<SearchResult>());

        // Assert
        result.Value.Should().Be(0);
    }

    [Fact]
    public void CalculateLlmConfidence_WithWhitespaceResponse_ReturnsZero()
    {
        // Act
        var result = _service.CalculateLlmConfidence("   ", new List<SearchResult>());

        // Assert
        result.Value.Should().Be(0);
    }

    [Fact]
    public void CalculateLlmConfidence_WithNullResponse_ReturnsZero()
    {
        // Act
        var result = _service.CalculateLlmConfidence(null!, new List<SearchResult>());

        // Assert
        result.Value.Should().Be(0);
    }

    [Fact]
    public void CalculateLlmConfidence_WithBasicResponse_ReturnsBaseConfidence()
    {
        // Arrange
        var response = "This is a basic response without citations.";

        // Act
        var result = _service.CalculateLlmConfidence(response, new List<SearchResult>());

        // Assert
        // Base confidence is 0.5
        result.Value.Should().BeApproximately(0.5, 0.1);
    }

    [Fact]
    public void CalculateLlmConfidence_WithCitationsInBrackets_IncreasesConfidence()
    {
        // Arrange
        var response = "According to the rulebook [1], the player should...";

        // Act
        var result = _service.CalculateLlmConfidence(response, new List<SearchResult>());

        // Assert
        // Base 0.5 + 0.2 for citations = 0.7
        result.Value.Should().BeGreaterThanOrEqualTo(0.7);
    }

    [Fact]
    public void CalculateLlmConfidence_WithPageReference_IncreasesConfidence()
    {
        // Arrange
        var response = "As stated (Page 15), the rules indicate...";

        // Act
        var result = _service.CalculateLlmConfidence(response, new List<SearchResult>());

        // Assert
        result.Value.Should().BeGreaterThanOrEqualTo(0.7);
    }

    [Fact]
    public void CalculateLlmConfidence_WithHighQualitySources_IncreasesConfidence()
    {
        // Arrange
        var response = "This is a response with citations [1].";
        var sources = new List<SearchResult>
        {
            CreateSearchResult(1, 0.95),
            CreateSearchResult(2, 0.90)
        };

        // Act
        var result = _service.CalculateLlmConfidence(response, sources);

        // Assert
        // Base 0.5 + 0.2 (citations) + search contribution
        result.Value.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void CalculateLlmConfidence_CapsAtOne()
    {
        // Arrange - Maximum everything
        var response = "Response with [1] citations and (Page 1) references.";
        var sources = new List<SearchResult>
        {
            CreateSearchResult(1, 1.0),
            CreateSearchResult(2, 1.0)
        };

        // Act
        var result = _service.CalculateLlmConfidence(response, sources);

        // Assert
        result.Value.Should().BeLessThanOrEqualTo(1.0);
    }

    #endregion

    #region CalculateOverallConfidence Tests

    [Fact]
    public void CalculateOverallConfidence_CombinesSearchAndLlmConfidence()
    {
        // Arrange
        var searchConfidence = new Confidence(0.8);
        var llmConfidence = new Confidence(0.6);

        // Act
        var result = _service.CalculateOverallConfidence(searchConfidence, llmConfidence);

        // Assert
        // Weighted: 0.8 * 0.7 + 0.6 * 0.3 = 0.56 + 0.18 = 0.74
        result.Value.Should().BeApproximately(0.74, 0.01);
    }

    [Fact]
    public void CalculateOverallConfidence_WeightsSearchHigher()
    {
        // Arrange
        var searchConfidence = new Confidence(1.0);
        var llmConfidence = new Confidence(0.0);

        // Act
        var result = _service.CalculateOverallConfidence(searchConfidence, llmConfidence);

        // Assert
        // Should be 0.7 (70% search weight)
        result.Value.Should().BeApproximately(0.7, 0.01);
    }

    [Fact]
    public void CalculateOverallConfidence_WithBothZero_ReturnsZero()
    {
        // Arrange
        var searchConfidence = Confidence.Zero;
        var llmConfidence = Confidence.Zero;

        // Act
        var result = _service.CalculateOverallConfidence(searchConfidence, llmConfidence);

        // Assert
        result.Value.Should().Be(0);
    }

    [Fact]
    public void CalculateOverallConfidence_WithBothHigh_ReturnsHigh()
    {
        // Arrange
        var searchConfidence = new Confidence(0.9);
        var llmConfidence = new Confidence(0.9);

        // Act
        var result = _service.CalculateOverallConfidence(searchConfidence, llmConfidence);

        // Assert
        result.Value.Should().BeApproximately(0.9, 0.01);
    }

    #endregion

    #region IsLowQuality Tests

    [Fact]
    public void IsLowQuality_WithConfidenceBelowThreshold_ReturnsTrue()
    {
        // Arrange
        var confidence = new Confidence(0.4);

        // Act
        var result = _service.IsLowQuality(confidence);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsLowQuality_WithConfidenceAtThreshold_ReturnsFalse()
    {
        // Arrange
        var confidence = new Confidence(0.5);

        // Act
        var result = _service.IsLowQuality(confidence);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsLowQuality_WithHighConfidence_ReturnsFalse()
    {
        // Arrange
        var confidence = new Confidence(0.9);

        // Act
        var result = _service.IsLowQuality(confidence);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsLowQuality_WithZeroConfidence_ReturnsTrue()
    {
        // Act
        var result = _service.IsLowQuality(Confidence.Zero);

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region IsHighQuality Tests

    [Fact]
    public void IsHighQuality_WithConfidenceAboveThreshold_ReturnsTrue()
    {
        // Arrange
        var confidence = new Confidence(0.85);

        // Act
        var result = _service.IsHighQuality(confidence);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsHighQuality_WithConfidenceAtThreshold_ReturnsTrue()
    {
        // Arrange
        var confidence = new Confidence(0.8);

        // Act
        var result = _service.IsHighQuality(confidence);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsHighQuality_WithConfidenceBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var confidence = new Confidence(0.79);

        // Act
        var result = _service.IsHighQuality(confidence);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsHighQuality_WithMediumConfidence_ReturnsFalse()
    {
        // Arrange
        var confidence = new Confidence(0.6);

        // Act
        var result = _service.IsHighQuality(confidence);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region CalculateCitationQuality Tests

    [Fact]
    public void CalculateCitationQuality_WithNullCitations_ReturnsZero()
    {
        // Act
        var result = _service.CalculateCitationQuality(null!, "response");

        // Assert
        result.Value.Should().Be(0);
    }

    [Fact]
    public void CalculateCitationQuality_WithEmptyCitations_ReturnsZero()
    {
        // Act
        var result = _service.CalculateCitationQuality(new List<Citation>(), "response");

        // Assert
        result.Value.Should().Be(0);
    }

    [Fact]
    public void CalculateCitationQuality_WithAllCitationsReferenced_ReturnsOne()
    {
        // Arrange
        var citations = new List<Citation>
        {
            CreateCitation(1),
            CreateCitation(2)
        };
        var response = "According to [1] and [2], the rules state...";

        // Act
        var result = _service.CalculateCitationQuality(citations, response);

        // Assert
        result.Value.Should().Be(1.0);
    }

    [Fact]
    public void CalculateCitationQuality_WithPageReferences_ReturnsAccuracy()
    {
        // Arrange
        var citations = new List<Citation>
        {
            CreateCitation(5),
            CreateCitation(10)
        };
        var response = "As shown on Page 5, the game proceeds...";

        // Act
        var result = _service.CalculateCitationQuality(citations, response);

        // Assert
        // Only page 5 is referenced
        result.Value.Should().BeApproximately(0.5, 0.01);
    }

    [Fact]
    public void CalculateCitationQuality_WithNoCitationsReferenced_ReturnsZero()
    {
        // Arrange
        var citations = new List<Citation>
        {
            CreateCitation(1),
            CreateCitation(2)
        };
        var response = "The rules are straightforward.";

        // Act
        var result = _service.CalculateCitationQuality(citations, response);

        // Assert
        result.Value.Should().Be(0);
    }

    [Fact]
    public void CalculateCitationQuality_WithPartialReferences_ReturnsPartialScore()
    {
        // Arrange
        var citations = new List<Citation>
        {
            CreateCitation(1),
            CreateCitation(2),
            CreateCitation(3),
            CreateCitation(4)
        };
        var response = "According to [1] and [2], the game works...";

        // Act
        var result = _service.CalculateCitationQuality(citations, response);

        // Assert
        // 2 out of 4 referenced
        result.Value.Should().BeApproximately(0.5, 0.01);
    }

    #endregion

    #region Helper Methods

    private static SearchResult CreateSearchResult(int rank, double score)
    {
        return new SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: $"Test content for rank {rank}",
            pageNumber: rank,
            relevanceScore: new Confidence(score),
            rank: rank,
            searchMethod: "vector");
    }

    private static Citation CreateCitation(int pageNumber)
    {
        return new Citation(
            documentId: Guid.NewGuid(),
            pageNumber: pageNumber,
            snippet: $"Snippet from page {pageNumber}",
            relevanceScore: 0.8);
    }

    #endregion
}
