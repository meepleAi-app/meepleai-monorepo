using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.VectorSearch;

/// <summary>
/// Tests for the VectorSearchDomainService.
/// Issue #3025: Backend 90% Coverage Target - Phase 16
/// </summary>
[Trait("Category", "Unit")]
public sealed class VectorSearchDomainServiceTests
{
    private readonly VectorSearchDomainService _service;

    public VectorSearchDomainServiceTests()
    {
        _service = new VectorSearchDomainService();
    }

    #region Search Tests - Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Search_WithInvalidTopK_ThrowsArgumentException(int topK)
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var candidates = new List<Embedding>();

        // Act
        var action = () => _service.Search(queryVector, candidates, topK, 0.5);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*TopK must be positive*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-1.0)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void Search_WithInvalidMinScore_ThrowsArgumentException(double minScore)
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var candidates = new List<Embedding>();

        // Act
        var action = () => _service.Search(queryVector, candidates, 10, minScore);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MinScore must be between 0 and 1*");
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Search_WithValidMinScore_DoesNotThrow(double minScore)
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var candidates = new List<Embedding>();

        // Act
        var action = () => _service.Search(queryVector, candidates, 10, minScore);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region Search Tests - Empty Results

    [Fact]
    public void Search_WithEmptyCandidates_ReturnsEmptyList()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var candidates = new List<Embedding>();

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.5);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void Search_WithHighMinScore_FiltersBelowThreshold()
    {
        // Arrange - Create candidates with low similarity
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var lowSimilarityVector = CreateVector([0.0f, 1.0f, 0.0f]); // Orthogonal, similarity = 0

        var candidates = new List<Embedding>
        {
            CreateEmbedding(lowSimilarityVector, "Low similarity content")
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.9);

        // Assert - Should filter out due to minScore
        result.Should().BeEmpty();
    }

    #endregion

    #region Search Tests - Similarity Calculation

    [Fact]
    public void Search_WithIdenticalVectors_ReturnsSimilarityOfOne()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var identicalVector = CreateVector([1.0f, 0.0f, 0.0f]);

        var candidates = new List<Embedding>
        {
            CreateEmbedding(identicalVector, "Identical content")
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.0);

        // Assert
        result.Should().HaveCount(1);
        result[0].RelevanceScore.Value.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void Search_WithOrthogonalVectors_ReturnsZeroSimilarity()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var orthogonalVector = CreateVector([0.0f, 1.0f, 0.0f]);

        var candidates = new List<Embedding>
        {
            CreateEmbedding(orthogonalVector, "Orthogonal content")
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.0);

        // Assert
        result.Should().HaveCount(1);
        result[0].RelevanceScore.Value.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void Search_RanksResultsByDescendingSimilarity()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);

        // Create vectors with different similarities
        var highSimilarity = CreateVector([0.9f, 0.1f, 0.0f]); // High similarity
        var mediumSimilarity = CreateVector([0.6f, 0.4f, 0.0f]); // Medium similarity
        var lowSimilarity = CreateVector([0.3f, 0.7f, 0.0f]); // Low similarity

        var candidates = new List<Embedding>
        {
            CreateEmbedding(lowSimilarity, "Low"),
            CreateEmbedding(highSimilarity, "High"),
            CreateEmbedding(mediumSimilarity, "Medium")
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.0);

        // Assert - Should be ordered by descending similarity
        result.Should().HaveCount(3);
        result[0].TextContent.Should().Be("High");
        result[1].TextContent.Should().Be("Medium");
        result[2].TextContent.Should().Be("Low");
    }

    #endregion

    #region Search Tests - TopK

    [Fact]
    public void Search_LimitsResultsToTopK()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);

        var candidates = new List<Embedding>();
        for (int i = 0; i < 10; i++)
        {
            var vector = CreateVector([1.0f - i * 0.1f, i * 0.1f, 0.0f]);
            candidates.Add(CreateEmbedding(vector, $"Content {i}"));
        }

        // Act
        var result = _service.Search(queryVector, candidates, 3, 0.0);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public void Search_ReturnsLessThanTopKIfNotEnoughCandidates()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);

        var candidates = new List<Embedding>
        {
            CreateEmbedding(CreateVector([1.0f, 0.0f, 0.0f]), "Only one")
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.0);

        // Assert
        result.Should().HaveCount(1);
    }

    #endregion

    #region Search Tests - Rank Assignment

    [Fact]
    public void Search_AssignsRanksStartingFromOne()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);

        var candidates = new List<Embedding>
        {
            CreateEmbedding(CreateVector([0.9f, 0.1f, 0.0f]), "First"),
            CreateEmbedding(CreateVector([0.8f, 0.2f, 0.0f]), "Second"),
            CreateEmbedding(CreateVector([0.7f, 0.3f, 0.0f]), "Third")
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.0);

        // Assert
        result[0].Rank.Should().Be(1);
        result[1].Rank.Should().Be(2);
        result[2].Rank.Should().Be(3);
    }

    #endregion

    #region Search Tests - SearchMethod

    [Fact]
    public void Search_SetsSearchMethodToVector()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);

        var candidates = new List<Embedding>
        {
            CreateEmbedding(CreateVector([1.0f, 0.0f, 0.0f]), "Content")
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.0);

        // Assert
        result.Should().AllSatisfy(r => r.SearchMethod.Should().Be("vector"));
    }

    #endregion

    #region Search Tests - Result Properties

    [Fact]
    public void Search_CopiesVectorDocumentIdFromEmbedding()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var documentId = Guid.NewGuid();

        var candidates = new List<Embedding>
        {
            CreateEmbedding(CreateVector([1.0f, 0.0f, 0.0f]), "Content", documentId)
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.0);

        // Assert
        result[0].VectorDocumentId.Should().Be(documentId);
    }

    [Fact]
    public void Search_CopiesTextContentFromEmbedding()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var expectedContent = "Expected text content";

        var candidates = new List<Embedding>
        {
            CreateEmbedding(CreateVector([1.0f, 0.0f, 0.0f]), expectedContent)
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.0);

        // Assert
        result[0].TextContent.Should().Be(expectedContent);
    }

    [Fact]
    public void Search_CopiesPageNumberFromEmbedding()
    {
        // Arrange
        var queryVector = CreateVector([1.0f, 0.0f, 0.0f]);
        var expectedPageNumber = 42;

        var candidates = new List<Embedding>
        {
            CreateEmbedding(CreateVector([1.0f, 0.0f, 0.0f]), "Content", pageNumber: expectedPageNumber)
        };

        // Act
        var result = _service.Search(queryVector, candidates, 10, 0.0);

        // Assert
        result[0].PageNumber.Should().Be(expectedPageNumber);
    }

    #endregion

    #region FilterByScore Tests

    [Fact]
    public void FilterByScore_FiltersOutBelowThreshold()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            CreateSearchResult(0.9),
            CreateSearchResult(0.5),
            CreateSearchResult(0.3)
        };

        // Act
        var filtered = _service.FilterByScore(results, 0.6);

        // Assert
        filtered.Should().HaveCount(1);
        filtered[0].RelevanceScore.Value.Should().Be(0.9);
    }

    [Fact]
    public void FilterByScore_IncludesResultsAtThreshold()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            CreateSearchResult(0.5)
        };

        // Act
        var filtered = _service.FilterByScore(results, 0.5);

        // Assert
        filtered.Should().HaveCount(1);
    }

    [Fact]
    public void FilterByScore_WithZeroThreshold_ReturnsAll()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            CreateSearchResult(0.9),
            CreateSearchResult(0.5),
            CreateSearchResult(0.1)
        };

        // Act
        var filtered = _service.FilterByScore(results, 0.0);

        // Assert
        filtered.Should().HaveCount(3);
    }

    [Fact]
    public void FilterByScore_WithOneThreshold_ReturnsOnlyPerfect()
    {
        // Arrange
        var results = new List<SearchResult>
        {
            CreateSearchResult(1.0),
            CreateSearchResult(0.99),
            CreateSearchResult(0.5)
        };

        // Act
        var filtered = _service.FilterByScore(results, 1.0);

        // Assert
        filtered.Should().HaveCount(1);
        filtered[0].RelevanceScore.Value.Should().Be(1.0);
    }

    [Fact]
    public void FilterByScore_WithEmptyList_ReturnsEmptyList()
    {
        // Act
        var filtered = _service.FilterByScore([], 0.5);

        // Assert
        filtered.Should().BeEmpty();
    }

    #endregion

    #region ValidateSearchParameters Tests

    [Fact]
    public void ValidateSearchParameters_WithValidParameters_DoesNotThrow()
    {
        // Act
        var action = () => _service.ValidateSearchParameters(10, 0.5);

        // Assert
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void ValidateSearchParameters_WithInvalidTopK_ThrowsArgumentException(int topK)
    {
        // Act
        var action = () => _service.ValidateSearchParameters(topK, 0.5);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*TopK must be between 1 and 100*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(-1.0)]
    [InlineData(1.1)]
    [InlineData(2.0)]
    public void ValidateSearchParameters_WithInvalidMinScore_ThrowsArgumentException(double minScore)
    {
        // Act
        var action = () => _service.ValidateSearchParameters(10, minScore);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MinScore must be between 0.0 and 1.0*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public void ValidateSearchParameters_WithValidTopKRange_DoesNotThrow(int topK)
    {
        // Act
        var action = () => _service.ValidateSearchParameters(topK, 0.5);

        // Assert
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void ValidateSearchParameters_WithValidMinScoreRange_DoesNotThrow(double minScore)
    {
        // Act
        var action = () => _service.ValidateSearchParameters(10, minScore);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region Helper Methods

    private static Vector CreateVector(float[] values)
    {
        return new Vector(values);
    }

    private static Embedding CreateEmbedding(
        Vector vector,
        string textContent,
        Guid? documentId = null,
        int pageNumber = 1)
    {
        return new Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: documentId ?? Guid.NewGuid(),
            textContent: textContent,
            vector: vector,
            model: "test-model",
            chunkIndex: 0,
            pageNumber: pageNumber);
    }

    private static SearchResult CreateSearchResult(double relevanceScore)
    {
        return new SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: $"Result with score {relevanceScore}",
            pageNumber: 1,
            relevanceScore: new Confidence(relevanceScore),
            rank: 1,
            searchMethod: "vector");
    }

    #endregion
}
