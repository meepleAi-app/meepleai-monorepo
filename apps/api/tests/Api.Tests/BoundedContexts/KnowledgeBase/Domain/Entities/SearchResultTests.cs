using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Tests for the SearchResult entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 18
/// </summary>
[Trait("Category", "Unit")]
public sealed class SearchResultTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesSearchResult()
    {
        // Arrange
        var id = Guid.NewGuid();
        var vectorDocumentId = Guid.NewGuid();
        var textContent = "Sample search result content";
        var pageNumber = 5;
        var relevanceScore = new Confidence(0.85);
        var rank = 1;
        var searchMethod = "vector";

        // Act
        var result = new SearchResult(id, vectorDocumentId, textContent, pageNumber, relevanceScore, rank, searchMethod);

        // Assert
        result.Id.Should().Be(id);
        result.VectorDocumentId.Should().Be(vectorDocumentId);
        result.TextContent.Should().Be(textContent);
        result.PageNumber.Should().Be(pageNumber);
        result.RelevanceScore.Value.Should().Be(0.85);
        result.Rank.Should().Be(rank);
        result.SearchMethod.Should().Be(searchMethod);
    }

    [Fact]
    public void Constructor_WithoutSearchMethod_SetsNull()
    {
        // Arrange
        var relevanceScore = new Confidence(0.85);

        // Act
        var result = new SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "content", 1, relevanceScore, 1);

        // Assert
        result.SearchMethod.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithEmptyTextContent_ThrowsArgumentException()
    {
        // Arrange
        var relevanceScore = new Confidence(0.85);

        // Act
        var action = () => new SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "", 1, relevanceScore, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("textContent")
            .WithMessage("*Text content cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespaceTextContent_ThrowsArgumentException()
    {
        // Arrange
        var relevanceScore = new Confidence(0.85);

        // Act
        var action = () => new SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "   ", 1, relevanceScore, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Text content cannot be empty*");
    }

    [Fact]
    public void Constructor_WithZeroPageNumber_ThrowsArgumentException()
    {
        // Arrange
        var relevanceScore = new Confidence(0.85);

        // Act
        var action = () => new SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "content", 0, relevanceScore, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("pageNumber")
            .WithMessage("*Page number must be positive*");
    }

    [Fact]
    public void Constructor_WithNegativePageNumber_ThrowsArgumentException()
    {
        // Arrange
        var relevanceScore = new Confidence(0.85);

        // Act
        var action = () => new SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "content", -1, relevanceScore, 1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Page number must be positive*");
    }

    [Fact]
    public void Constructor_WithZeroRank_ThrowsArgumentException()
    {
        // Arrange
        var relevanceScore = new Confidence(0.85);

        // Act
        var action = () => new SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "content", 1, relevanceScore, 0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("rank")
            .WithMessage("*Rank must be positive*");
    }

    [Fact]
    public void Constructor_WithNegativeRank_ThrowsArgumentException()
    {
        // Arrange
        var relevanceScore = new Confidence(0.85);

        // Act
        var action = () => new SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "content", 1, relevanceScore, -1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Rank must be positive*");
    }

    [Fact]
    public void Constructor_WithNullRelevanceScore_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "content", 1, null!, 1);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("relevanceScore");
    }

    #endregion

    #region MeetsThreshold Tests

    [Fact]
    public void MeetsThreshold_WithScoreAboveThreshold_ReturnsTrue()
    {
        // Arrange
        var result = CreateSearchResult(relevanceScore: 0.85);

        // Act
        var meetsThreshold = result.MeetsThreshold(0.8);

        // Assert
        meetsThreshold.Should().BeTrue();
    }

    [Fact]
    public void MeetsThreshold_WithScoreAtThreshold_ReturnsTrue()
    {
        // Arrange
        var result = CreateSearchResult(relevanceScore: 0.8);

        // Act
        var meetsThreshold = result.MeetsThreshold(0.8);

        // Assert
        meetsThreshold.Should().BeTrue();
    }

    [Fact]
    public void MeetsThreshold_WithScoreBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var result = CreateSearchResult(relevanceScore: 0.7);

        // Act
        var meetsThreshold = result.MeetsThreshold(0.8);

        // Assert
        meetsThreshold.Should().BeFalse();
    }

    [Fact]
    public void MeetsThreshold_WithZeroThreshold_ReturnsTrue()
    {
        // Arrange
        var result = CreateSearchResult(relevanceScore: 0.01);

        // Act
        var meetsThreshold = result.MeetsThreshold(0.0);

        // Assert
        meetsThreshold.Should().BeTrue();
    }

    [Fact]
    public void MeetsThreshold_WithOneThreshold_OnlyPerfectScorePasses()
    {
        // Arrange
        var perfectResult = CreateSearchResult(relevanceScore: 1.0);
        var almostPerfectResult = CreateSearchResult(relevanceScore: 0.99);

        // Act & Assert
        perfectResult.MeetsThreshold(1.0).Should().BeTrue();
        almostPerfectResult.MeetsThreshold(1.0).Should().BeFalse();
    }

    #endregion

    #region Fusion Identity Tests

    [Fact]
    public void SearchResult_CarriesFusionIdentity_WhenProvided()
    {
        var pdf = Guid.NewGuid();
        var r = new Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: Guid.NewGuid(),
            textContent: "text",
            pageNumber: 1,
            relevanceScore: new Confidence(0.9),
            rank: 1,
            searchMethod: "vector",
            pdfDocumentId: pdf,
            chunkIndex: 7,
            roleTags: GameBookRole.Setup);

        r.PdfDocumentId.Should().Be(pdf);
        r.ChunkIndex.Should().Be(7);
        r.RoleTags.Should().Be(GameBookRole.Setup);
    }

    [Fact]
    public void SearchResult_FusionIdentity_DefaultsAreEmpty()
    {
        var r = new Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "text", 1, new Confidence(0.5), 1);
        r.PdfDocumentId.Should().Be(Guid.Empty);
        r.ChunkIndex.Should().Be(0);
        r.RoleTags.Should().Be(GameBookRole.None);
    }

    #endregion

    #region Search Method Tests

    [Theory]
    [InlineData("vector")]
    [InlineData("keyword")]
    [InlineData("hybrid")]
    public void Constructor_WithValidSearchMethod_SetsSearchMethod(string method)
    {
        // Arrange
        var relevanceScore = new Confidence(0.85);

        // Act
        var result = new SearchResult(
            Guid.NewGuid(), Guid.NewGuid(), "content", 1, relevanceScore, 1, method);

        // Assert
        result.SearchMethod.Should().Be(method);
    }

    #endregion

    #region Helper Methods

    private static SearchResult CreateSearchResult(
        string textContent = "Test content",
        int pageNumber = 1,
        double relevanceScore = 0.85)
    {
        return new SearchResult(
            Guid.NewGuid(),
            Guid.NewGuid(),
            textContent,
            pageNumber,
            new Confidence(relevanceScore),
            1,
            "vector");
    }

    #endregion
}
