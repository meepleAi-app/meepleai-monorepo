using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.VectorSearch;

/// <summary>
/// Tests for the RrfFusionDomainService.
/// Issue #3025: Backend 90% Coverage Target - Phase 14
/// </summary>
[Trait("Category", "Unit")]
public sealed class RrfFusionDomainServiceTests
{
    private readonly RrfFusionDomainService _service;

    public RrfFusionDomainServiceTests()
    {
        _service = new RrfFusionDomainService();
    }

    #region FuseResults Tests

    [Fact]
    public void FuseResults_WithBothResultSets_ReturnsRankedResults()
    {
        // Arrange
        var docId1 = Guid.NewGuid();
        var docId2 = Guid.NewGuid();
        var docId3 = Guid.NewGuid();

        var vectorResults = new List<SearchResult>
        {
            CreateSearchResult(docId1, 1, "vector"),
            CreateSearchResult(docId2, 2, "vector")
        };

        var keywordResults = new List<SearchResult>
        {
            CreateSearchResult(docId2, 1, "keyword"),
            CreateSearchResult(docId3, 2, "keyword")
        };

        // Act
        var result = _service.FuseResults(vectorResults, keywordResults);

        // Assert
        result.Should().HaveCount(3);
        result.Should().AllSatisfy(r => r.SearchMethod.Should().Be("hybrid"));
        result.First().Rank.Should().Be(1);
    }

    [Fact]
    public void FuseResults_PreservesRealRelevanceScore_NotRankBasedRrf()
    {
        // Issue #2712: the fused RelevanceScore must reflect the SOURCE result's real relevance
        // (cosine similarity for vector results), NOT the rank-based RRF score. The RRF score for a
        // single rank-1 result is NormalizeRrfScore(1/61) = 30/61 ≈ 0.4918; feeding that into
        // confidence made it degenerate (~0.53). The real cosine (0.87) must be preserved.
        var docId = Guid.NewGuid();
        var vectorResults = new List<SearchResult>
        {
            new SearchResult(
                id: Guid.NewGuid(),
                vectorDocumentId: docId,
                textContent: "Relevant chunk",
                pageNumber: 1,
                relevanceScore: new Confidence(0.87),
                rank: 1,
                searchMethod: "vector"),
        };
        var keywordResults = new List<SearchResult>();

        var result = _service.FuseResults(vectorResults, keywordResults);

        result.Should().HaveCount(1);
        result.First().RelevanceScore.Value.Should().BeApproximately(0.87, 0.0001,
            "the real cosine must survive fusion, not be overwritten by the ~0.49 rank-based RRF score");
    }

    [Fact]
    public void FuseResults_BothArms_KeepsCosineInRelevanceScore_NotHybridScore()
    {
        // Issue #3270 Task 5 / #2712: RrfFusionDomainService now delegates to HybridFusionCore for
        // scoring, but RelevanceScore must still be the carried cosine, never the hybrid/RRF score.
        var pdf = Guid.NewGuid();
        var vector = new List<SearchResult>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "content", 1, new Confidence(0.91), 1, "vector",
                pdfDocumentId: pdf, chunkIndex: 0, roleTags: GameBookRole.None)
        };
        var keyword = new List<SearchResult>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "content", 1, new Confidence(0.20), 1, "keyword",
                pdfDocumentId: pdf, chunkIndex: 0, roleTags: GameBookRole.None)
        };

        var fused = _service.FuseResults(vector, keyword);

        fused.Should().HaveCount(1);
        // #2712: RelevanceScore is the carried cosine (0.91), NOT an RRF/hybrid value.
        fused[0].RelevanceScore.Value.Should().BeApproximately(0.91, 1e-6);
        fused[0].SearchMethod.Should().Be("hybrid");
    }

    [Fact]
    public void FuseResults_RoleHint_ReordersByRoleBoost_OrderOnly()
    {
        // Issue #3270 Task 5: the new trailing queryRoleHint param routes through to
        // HybridFusionCore's role-boost, reordering results while leaving RelevanceScore untouched.
        var setupPdf = Guid.NewGuid();
        var plainPdf = Guid.NewGuid();
        var vector = new List<SearchResult>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "plain", 1, new Confidence(0.90), 1, "vector",
                pdfDocumentId: plainPdf, chunkIndex: 0, roleTags: GameBookRole.None),
            new(Guid.NewGuid(), Guid.NewGuid(), "setup", 1, new Confidence(0.80), 2, "vector",
                pdfDocumentId: setupPdf, chunkIndex: 0, roleTags: GameBookRole.Setup)
        };
        var keyword = new List<SearchResult>();

        var fused = _service.FuseResults(vector, keyword, queryRoleHint: GameBookRole.Setup);

        fused[0].TextContent.Should().Be("setup");            // role-boost lifted it
        fused[0].RelevanceScore.Value.Should().BeApproximately(0.80, 1e-6); // cosine preserved
    }

    [Fact]
    public void FuseResults_QueryTermMatchesHeading_ReordersByHeadingBoost_OrderOnly()
    {
        // Issue #3270: query terms matching a chunk's heading route through HybridFusionCore's
        // heading-boost, lifting the matching chunk over a better-ranked peer while leaving
        // RelevanceScore (the carried cosine) untouched.
        var setupPdf = Guid.NewGuid();
        var plainPdf = Guid.NewGuid();
        var vector = new List<SearchResult>
        {
            // plain has the better raw rank (1); only the heading boost can reorder them.
            new(Guid.NewGuid(), Guid.NewGuid(), "plain", 1, new Confidence(0.90), 1, "vector",
                pdfDocumentId: plainPdf, chunkIndex: 0, roleTags: GameBookRole.None, heading: "Scoring"),
            new(Guid.NewGuid(), Guid.NewGuid(), "setup rules", 1, new Confidence(0.80), 2, "vector",
                pdfDocumentId: setupPdf, chunkIndex: 0, roleTags: GameBookRole.None, heading: "Setup")
        };
        var keyword = new List<SearchResult>();

        var fused = _service.FuseResults(vector, keyword, queryTerms: new[] { "setup" });

        fused[0].TextContent.Should().Be("setup rules");      // heading-boost lifted it
        fused[0].Heading.Should().Be("Setup");                // heading carried through fusion
        fused[0].RelevanceScore.Value.Should().BeApproximately(0.80, 1e-6); // cosine preserved
    }

    [Fact]
    public void FuseResults_NoQueryTerms_HeadingBoostIsNoOp()
    {
        var plainPdf = Guid.NewGuid();
        var setupPdf = Guid.NewGuid();
        var vector = new List<SearchResult>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "plain", 1, new Confidence(0.90), 1, "vector",
                pdfDocumentId: plainPdf, chunkIndex: 0, roleTags: GameBookRole.None, heading: "Scoring"),
            new(Guid.NewGuid(), Guid.NewGuid(), "setup", 1, new Confidence(0.80), 2, "vector",
                pdfDocumentId: setupPdf, chunkIndex: 0, roleTags: GameBookRole.None, heading: "Setup")
        };

        // No queryTerms → heading boost never fires → pure RRF (rank 1 wins).
        var fused = _service.FuseResults(vector, new List<SearchResult>());

        fused[0].TextContent.Should().Be("plain");
    }

    [Fact]
    public void FuseResults_WithOverlappingDocuments_CombinesScores()
    {
        // Arrange
        var docId = Guid.NewGuid();

        var vectorResults = new List<SearchResult>
        {
            CreateSearchResult(docId, 1, "vector")
        };

        var keywordResults = new List<SearchResult>
        {
            CreateSearchResult(docId, 1, "keyword")
        };

        // Act
        var result = _service.FuseResults(vectorResults, keywordResults);

        // Assert
        result.Should().HaveCount(1);
        // Document appearing in both lists should have higher score
        result.First().RelevanceScore.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public void FuseResults_WithEmptyVectorResults_ReturnsKeywordResults()
    {
        // Arrange
        var docId = Guid.NewGuid();

        var vectorResults = new List<SearchResult>();
        var keywordResults = new List<SearchResult>
        {
            CreateSearchResult(docId, 1, "keyword")
        };

        // Act
        var result = _service.FuseResults(vectorResults, keywordResults);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void FuseResults_WithEmptyKeywordResults_ReturnsVectorResults()
    {
        // Arrange
        var docId = Guid.NewGuid();

        var vectorResults = new List<SearchResult>
        {
            CreateSearchResult(docId, 1, "vector")
        };
        var keywordResults = new List<SearchResult>();

        // Act
        var result = _service.FuseResults(vectorResults, keywordResults);

        // Assert
        result.Should().HaveCount(1);
    }

    [Fact]
    public void FuseResults_WithBothEmpty_ReturnsEmptyList()
    {
        // Act
        var result = _service.FuseResults(
            new List<SearchResult>(),
            new List<SearchResult>());

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void FuseResults_WithCustomRrfK_UsesCustomValue()
    {
        // Arrange
        var docId = Guid.NewGuid();

        var vectorResults = new List<SearchResult>
        {
            CreateSearchResult(docId, 1, "vector")
        };

        // Act
        var resultDefault = _service.FuseResults(vectorResults, new List<SearchResult>(), 60);
        var resultCustom = _service.FuseResults(vectorResults, new List<SearchResult>(), 10);

        // Assert
        // Different K values should produce different normalized scores
        resultDefault.Should().HaveCount(1);
        resultCustom.Should().HaveCount(1);
    }

    [Fact]
    public void FuseResults_WithZeroRrfK_ThrowsArgumentException()
    {
        // Arrange
        var vectorResults = new List<SearchResult>
        {
            CreateSearchResult(Guid.NewGuid(), 1, "vector")
        };

        // Act
        var action = () => _service.FuseResults(
            vectorResults,
            new List<SearchResult>(),
            0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*RRF K must be positive*");
    }

    [Fact]
    public void FuseResults_WithNegativeRrfK_ThrowsArgumentException()
    {
        // Act
        var action = () => _service.FuseResults(
            new List<SearchResult>(),
            new List<SearchResult>(),
            -1);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*RRF K must be positive*");
    }

    [Fact]
    public void FuseResults_AssignsCorrectRanks()
    {
        // Arrange
        var docId1 = Guid.NewGuid();
        var docId2 = Guid.NewGuid();
        var docId3 = Guid.NewGuid();

        var vectorResults = new List<SearchResult>
        {
            CreateSearchResult(docId1, 1, "vector"),
            CreateSearchResult(docId2, 2, "vector"),
            CreateSearchResult(docId3, 3, "vector")
        };

        // Act
        var result = _service.FuseResults(vectorResults, new List<SearchResult>());

        // Assert
        result.Should().HaveCount(3);
        result.Select(r => r.Rank).Should().BeEquivalentTo(new[] { 1, 2, 3 });
    }

    [Fact]
    public void FuseResults_SortsResultsByScore()
    {
        // Arrange - create results where higher ranked vector results should come first
        var docId1 = Guid.NewGuid();
        var docId2 = Guid.NewGuid();

        // docId1 has rank 1 in vector (highest RRF score)
        // docId2 has rank 10 in vector (lower RRF score)
        var vectorResults = new List<SearchResult>
        {
            CreateSearchResult(docId1, 1, "vector"),
            CreateSearchResult(docId2, 10, "vector")
        };

        // Act
        var result = _service.FuseResults(vectorResults, new List<SearchResult>());

        // Assert
        result.Should().HaveCount(2);
        result[0].VectorDocumentId.Should().Be(docId1); // Higher score should be first
    }

    #endregion

    #region CalculateRrfScore Tests

    [Fact]
    public void CalculateRrfScore_WithRank1_ReturnsExpectedScore()
    {
        // Act
        var score = _service.CalculateRrfScore(1);

        // Assert
        // With default k=60, score = 1/(60+1) = ~0.0164
        score.Should().BeApproximately(1.0 / 61, 0.0001);
    }

    [Fact]
    public void CalculateRrfScore_WithRank10_ReturnsLowerScore()
    {
        // Act
        var scoreRank1 = _service.CalculateRrfScore(1);
        var scoreRank10 = _service.CalculateRrfScore(10);

        // Assert
        scoreRank10.Should().BeLessThan(scoreRank1);
        // With default k=60, score = 1/(60+10) = ~0.0143
        scoreRank10.Should().BeApproximately(1.0 / 70, 0.0001);
    }

    [Fact]
    public void CalculateRrfScore_WithCustomK_UsesCustomValue()
    {
        // Act
        var scoreK60 = _service.CalculateRrfScore(1, 60);
        var scoreK10 = _service.CalculateRrfScore(1, 10);

        // Assert
        scoreK10.Should().BeGreaterThan(scoreK60);
        // k=10: 1/(10+1) = ~0.0909
        // k=60: 1/(60+1) = ~0.0164
        scoreK10.Should().BeApproximately(1.0 / 11, 0.0001);
    }

    [Fact]
    public void CalculateRrfScore_WithZeroRank_ThrowsArgumentException()
    {
        // Act
        var action = () => _service.CalculateRrfScore(0);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Rank must be positive*");
    }

    [Fact]
    public void CalculateRrfScore_WithNegativeRank_ThrowsArgumentException()
    {
        // Act
        var action = () => _service.CalculateRrfScore(-5);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Rank must be positive*");
    }

    [Theory]
    [InlineData(1, 60, 0.01639)]
    [InlineData(2, 60, 0.01613)]
    [InlineData(5, 60, 0.01538)]
    [InlineData(1, 30, 0.03226)]
    [InlineData(1, 100, 0.00990)]
    public void CalculateRrfScore_WithVariousInputs_ReturnsExpectedScore(
        int rank, int rrfK, double expectedScore)
    {
        // Act
        var score = _service.CalculateRrfScore(rank, rrfK);

        // Assert
        score.Should().BeApproximately(expectedScore, 0.001);
    }

    #endregion

    #region Helper Methods

    private static SearchResult CreateSearchResult(Guid documentId, int rank, string method)
    {
        // #3270 Task 5: GetChunkKey is now "{PdfDocumentId}_{ChunkIndex}" (unified identity across
        // vector/keyword arms). Reuse documentId as PdfDocumentId (chunkIndex fixed at 0) so the key
        // stays as distinct/overlapping as it was under the old VectorDocumentId-based key: distinct
        // documentId values across calls -> distinct chunks; the SAME documentId reused across a
        // vector+keyword pair -> same chunk, fusing together as each test's arrange already implies.
        return new SearchResult(
            id: Guid.NewGuid(),
            vectorDocumentId: documentId,
            textContent: $"Test content for document {documentId}",
            pageNumber: 1,
            relevanceScore: new Confidence(0.8),
            rank: rank,
            searchMethod: method,
            pdfDocumentId: documentId,
            chunkIndex: 0);
    }

    #endregion
}
