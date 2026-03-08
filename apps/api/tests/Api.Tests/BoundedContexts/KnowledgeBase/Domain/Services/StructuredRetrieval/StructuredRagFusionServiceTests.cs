using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.StructuredRetrieval;

/// <summary>
/// Tests for StructuredRagFusionService.
/// Issue #5453: Structured RAG fusion.
/// </summary>
[Trait("Category", "Unit")]
public sealed class StructuredRagFusionServiceTests
{
    private readonly StructuredRagFusionService _sut = new(
        NullLogger<StructuredRagFusionService>.Instance);

    private static readonly Guid GameId = Guid.NewGuid();

    #region High-Confidence Bypass

    [Fact]
    public void Fuse_HighConfidenceStructured_BypassesVector()
    {
        // Arrange
        var structuredResponse = CreateStructuredResponse(
            results: [CreateStructuredResult(0.92)],
            confidence: 0.9,
            shouldBypass: true);

        var vectorResults = CreateVectorResults(5);

        // Act
        var result = _sut.Fuse(structuredResponse, vectorResults);

        // Assert
        result.FusionStrategy.Should().Be("structured_bypass");
        result.StructuredContributionPercent.Should().Be(100.0);
        result.VectorContributionPercent.Should().Be(0.0);
        result.Results.Should().HaveCount(1);
    }

    #endregion

    #region Low-Confidence Fallback

    [Fact]
    public void Fuse_LowConfidenceStructured_FallsBackToVector()
    {
        // Arrange
        var structuredResponse = CreateStructuredResponse(
            results: [],
            confidence: 0.3,
            shouldBypass: false);

        var vectorResults = CreateVectorResults(5);

        // Act
        var result = _sut.Fuse(structuredResponse, vectorResults);

        // Assert
        result.FusionStrategy.Should().Be("vector_only");
        result.StructuredContributionPercent.Should().Be(0.0);
        result.VectorContributionPercent.Should().Be(100.0);
        result.Results.Should().HaveCount(5);
    }

    [Fact]
    public void Fuse_NoStructuredResults_FallsBackToVector()
    {
        // Arrange
        var structuredResponse = CreateStructuredResponse(
            results: [],
            confidence: 0.8,
            shouldBypass: false);

        var vectorResults = CreateVectorResults(3);

        // Act
        var result = _sut.Fuse(structuredResponse, vectorResults);

        // Assert
        result.FusionStrategy.Should().Be("vector_only");
    }

    #endregion

    #region Weighted RRF Fusion

    [Fact]
    public void Fuse_MediumConfidence_UsesWeightedRrf()
    {
        // Arrange
        var structuredResponse = CreateStructuredResponse(
            results: [CreateStructuredResult(0.80)],
            confidence: 0.75,
            shouldBypass: false);

        var vectorResults = CreateVectorResults(5);

        // Act
        var result = _sut.Fuse(structuredResponse, vectorResults);

        // Assert
        result.FusionStrategy.Should().Be("weighted_rrf");
        result.StructuredContributionPercent.Should().BeGreaterThan(0.0);
        result.Results.Should().NotBeEmpty();
    }

    [Fact]
    public void Fuse_WeightedRrf_StructuredResultsRankedHigher()
    {
        // Arrange - Structured result with high confidence should rank above vector
        var structuredResponse = CreateStructuredResponse(
            results: [CreateStructuredResult(0.90)],
            confidence: 0.75,
            shouldBypass: false);

        var vectorResults = CreateVectorResults(3);

        // Act
        var result = _sut.Fuse(structuredResponse, vectorResults);

        // Assert
        result.Results.Should().HaveCountGreaterThan(0);
        var topResult = result.Results.First();
        topResult.SearchMethod.Should().StartWith("structured");
    }

    [Fact]
    public void Fuse_WeightedRrf_RespectsMaxResults()
    {
        // Arrange
        var structuredResults = Enumerable.Range(0, 5)
            .Select(_ => CreateStructuredResult(0.80))
            .ToList();

        var structuredResponse = CreateStructuredResponse(
            results: structuredResults,
            confidence: 0.75,
            shouldBypass: false);

        var vectorResults = CreateVectorResults(10);

        // Act
        var result = _sut.Fuse(structuredResponse, vectorResults, maxResults: 5);

        // Assert
        result.Results.Should().HaveCount(5);
    }

    [Fact]
    public void Fuse_WeightedRrf_AssignsSequentialRanks()
    {
        // Arrange
        var structuredResponse = CreateStructuredResponse(
            results: [CreateStructuredResult(0.80), CreateStructuredResult(0.70)],
            confidence: 0.75,
            shouldBypass: false);

        var vectorResults = CreateVectorResults(3);

        // Act
        var result = _sut.Fuse(structuredResponse, vectorResults);

        // Assert
        for (var i = 0; i < result.Results.Count; i++)
        {
            result.Results[i].Rank.Should().Be(i + 1);
        }
    }

    #endregion

    #region Intent Propagation

    [Fact]
    public void Fuse_PropagatesIntentFromClassification()
    {
        // Arrange
        var structuredResponse = CreateStructuredResponse(
            results: [CreateStructuredResult(0.90)],
            confidence: 0.85,
            shouldBypass: false,
            intent: StructuredQueryIntent.VictoryConditions);

        // Act
        var result = _sut.Fuse(structuredResponse, []);

        // Assert
        result.Intent.Should().Be(StructuredQueryIntent.VictoryConditions);
    }

    #endregion

    #region Helpers

    private static StructuredRetrievalResponse CreateStructuredResponse(
        List<StructuredRetrievalResult> results,
        double confidence,
        bool shouldBypass,
        StructuredQueryIntent intent = StructuredQueryIntent.VictoryConditions)
    {
        return new StructuredRetrievalResponse(
            Results: results,
            Classification: new QueryIntentClassification(intent, confidence),
            ShouldBypassVector: shouldBypass,
            StructuredContributionPercent: results.Count > 0 ? 50.0 : 0.0);
    }

    private static StructuredRetrievalResult CreateStructuredResult(double confidence)
    {
        return new StructuredRetrievalResult(
            Content: $"Structured content with confidence {confidence}",
            Confidence: confidence,
            SourceIntent: StructuredQueryIntent.VictoryConditions,
            SourceField: "VictoryConditions",
            SharedGameId: GameId);
    }

    private static List<SearchResult> CreateVectorResults(int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => new SearchResult(
                id: Guid.NewGuid(),
                vectorDocumentId: Guid.NewGuid(),
                textContent: $"Vector search result {i}",
                pageNumber: i,
                relevanceScore: new Confidence(Math.Max(0.1, 1.0 - (i * 0.1))),
                rank: i,
                searchMethod: "hybrid"))
            .ToList();
    }

    #endregion
}
