using Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;

/// <summary>
/// Unit tests for HybridSearchEngine.
/// Issue #3492: Hybrid Search with Reranking Pipeline.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "HybridSearch")]
public class HybridSearchEngineTests
{
    private readonly Mock<IHybridSearchService> _mockHybridSearchService;
    private readonly Mock<IRerankedRetrievalService> _mockRerankerService;
    private readonly Mock<ILogger<HybridSearchEngine>> _mockLogger;
    private readonly HybridSearchEngineConfig _defaultConfig;

    public HybridSearchEngineTests()
    {
        _mockHybridSearchService = new Mock<IHybridSearchService>();
        _mockRerankerService = new Mock<IRerankedRetrievalService>();
        _mockLogger = new Mock<ILogger<HybridSearchEngine>>();
        _defaultConfig = new HybridSearchEngineConfig();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullHybridSearchService_ShouldThrow()
    {
        // Arrange & Act
        var act = () => new HybridSearchEngine(
            null!,
            _mockRerankerService.Object,
            _mockLogger.Object,
            _defaultConfig);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("hybridSearchService");
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange & Act
        var act = () => new HybridSearchEngine(
            _mockHybridSearchService.Object,
            _mockRerankerService.Object,
            null!,
            _defaultConfig);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullConfig_ShouldUseDefaults()
    {
        // Arrange & Act
        var engine = new HybridSearchEngine(
            _mockHybridSearchService.Object,
            _mockRerankerService.Object,
            _mockLogger.Object,
            config: null);

        // Assert
        var config = engine.GetConfiguration();
        config.Should().NotBeNull();
        config.DefaultVectorWeight.Should().Be(0.6f);
        config.DefaultKeywordWeight.Should().Be(0.4f);
    }

    [Fact]
    public void Constructor_WithNullRerankerService_ShouldSucceed()
    {
        // Arrange & Act
        var engine = new HybridSearchEngine(
            _mockHybridSearchService.Object,
            rerankerService: null,
            _mockLogger.Object,
            _defaultConfig);

        // Assert
        engine.Should().NotBeNull();
    }

    #endregion

    #region SearchAsync Tests

    [Fact]
    public async Task SearchAsync_WithValidRequest_ShouldReturnResults()
    {
        // Arrange
        var gameId = Guid.NewGuid();
        var request = new HybridSearchEngineRequest
        {
            Query = "test query",
            GameId = gameId,
            MaxResults = 10,
            EnableReranking = false
        };

        var hybridResults = new List<HybridSearchResult>
        {
            new()
            {
                ChunkId = "chunk1",
                PdfDocumentId = Guid.NewGuid().ToString(),
                Content = "Test content 1",
                HybridScore = 0.9f,
                VectorScore = 0.85f,
                KeywordScore = 0.95f,
                VectorRank = 1,
                KeywordRank = 2,
                GameId = gameId,
                PageNumber = 1,
                ChunkIndex = 0,
                MatchedTerms = new List<string> { "test" },
                Mode = SearchMode.Hybrid
            },
            new()
            {
                ChunkId = "chunk2",
                PdfDocumentId = Guid.NewGuid().ToString(),
                Content = "Test content 2",
                HybridScore = 0.7f,
                VectorScore = 0.75f,
                KeywordScore = 0.65f,
                VectorRank = 2,
                KeywordRank = 3,
                GameId = gameId,
                PageNumber = 2,
                ChunkIndex = 1,
                MatchedTerms = new List<string> { "test", "content" },
                Mode = SearchMode.Hybrid
            }
        };

        _mockHybridSearchService
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(hybridResults);

        var engine = new HybridSearchEngine(
            _mockHybridSearchService.Object,
            null,
            _mockLogger.Object,
            _defaultConfig);

        // Act
        var result = await engine.SearchAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2);
        result.Items[0].Content.Should().Be("Test content 1");
        result.Items[1].Content.Should().Be("Test content 2");
        result.Metrics.Should().NotBeNull();
        result.Metrics.ResultCount.Should().Be(2);
    }

    [Fact]
    public async Task SearchAsync_WithEmptyResults_ShouldReturnEmptyList()
    {
        // Arrange
        var request = new HybridSearchEngineRequest
        {
            Query = "no results query",
            GameId = Guid.NewGuid(),
            MaxResults = 10,
            EnableReranking = false
        };

        _mockHybridSearchService
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        var engine = new HybridSearchEngine(
            _mockHybridSearchService.Object,
            null,
            _mockLogger.Object,
            _defaultConfig);

        // Act
        var result = await engine.SearchAsync(request);

        // Assert
        result.Items.Should().BeEmpty();
        result.Metrics.ResultCount.Should().Be(0);
    }

    [Fact]
    public async Task SearchAsync_WithException_ShouldReturnErrorResult()
    {
        // Arrange
        var request = new HybridSearchEngineRequest
        {
            Query = "error query",
            GameId = Guid.NewGuid(),
            MaxResults = 10
        };

        _mockHybridSearchService
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var engine = new HybridSearchEngine(
            _mockHybridSearchService.Object,
            null,
            _mockLogger.Object,
            _defaultConfig);

        // Act
        var result = await engine.SearchAsync(request);

        // Assert
        result.Items.Should().BeEmpty();
        result.Metrics.Error.Should().Contain("Test error");
    }

    #endregion

    #region A/B Testing Tests

    [Theory]
    [InlineData(null, 0.6f, 0.4f)]
    [InlineData("CONTROL", 0.6f, 0.4f)]
    [InlineData("SEMANTIC_HEAVY", 0.8f, 0.2f)]
    [InlineData("BALANCED", 0.5f, 0.5f)]
    [InlineData("KEYWORD_HEAVY", 0.3f, 0.7f)]
    public async Task SearchAsync_WithAbTestVariant_ShouldUseCorrectWeights(
        string? variant, float expectedVector, float expectedKeyword)
    {
        // Arrange
        float capturedVectorWeight = 0;
        float capturedKeywordWeight = 0;

        var request = new HybridSearchEngineRequest
        {
            Query = "test",
            GameId = Guid.NewGuid(),
            MaxResults = 5,
            EnableReranking = false,
            AbTestVariant = variant
        };

        _mockHybridSearchService
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Guid, SearchMode, int, List<Guid>?, float, float, CancellationToken>(
                (q, g, m, l, d, vw, kw, ct) =>
                {
                    capturedVectorWeight = vw;
                    capturedKeywordWeight = kw;
                })
            .ReturnsAsync(new List<HybridSearchResult>());

        var engine = new HybridSearchEngine(
            _mockHybridSearchService.Object,
            null,
            _mockLogger.Object,
            _defaultConfig);

        // Act
        var result = await engine.SearchAsync(request);

        // Assert
        capturedVectorWeight.Should().Be(expectedVector);
        capturedKeywordWeight.Should().Be(expectedKeyword);
        result.VectorWeight.Should().Be(expectedVector);
        result.KeywordWeight.Should().Be(expectedKeyword);
        result.AbTestVariant.Should().Be(variant);
    }

    [Fact]
    public async Task SearchAsync_WithUnknownVariant_ShouldUseDefaults()
    {
        // Arrange
        float capturedVectorWeight = 0;

        var request = new HybridSearchEngineRequest
        {
            Query = "test",
            GameId = Guid.NewGuid(),
            MaxResults = 5,
            AbTestVariant = "UNKNOWN_VARIANT"
        };

        _mockHybridSearchService
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, Guid, SearchMode, int, List<Guid>?, float, float, CancellationToken>(
                (q, g, m, l, d, vw, kw, ct) => capturedVectorWeight = vw)
            .ReturnsAsync(new List<HybridSearchResult>());

        var engine = new HybridSearchEngine(
            _mockHybridSearchService.Object,
            null,
            _mockLogger.Object,
            _defaultConfig);

        // Act
        await engine.SearchAsync(request);

        // Assert
        capturedVectorWeight.Should().Be(0.6f);
    }

    #endregion

    #region Metrics Tests

    [Fact]
    public async Task GetMetrics_AfterMultipleSearches_ShouldTrackMetrics()
    {
        // Arrange
        var gameId = Guid.NewGuid();

        _mockHybridSearchService
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>
            {
                new()
                {
                    ChunkId = "1",
                    Content = "Test",
                    HybridScore = 0.8f,
                    GameId = gameId,
                    PdfDocumentId = Guid.NewGuid().ToString(),
                    PageNumber = 1,
                    ChunkIndex = 0,
                    MatchedTerms = new List<string>(),
                    Mode = SearchMode.Hybrid
                }
            });

        var engine = new HybridSearchEngine(
            _mockHybridSearchService.Object,
            null,
            _mockLogger.Object,
            _defaultConfig);

        // Act
        await engine.SearchAsync(new HybridSearchEngineRequest
        {
            Query = "test1",
            GameId = gameId,
            MaxResults = 5,
            AbTestVariant = "CONTROL"
        });

        await engine.SearchAsync(new HybridSearchEngineRequest
        {
            Query = "test2",
            GameId = gameId,
            MaxResults = 5,
            AbTestVariant = "CONTROL"
        });

        await engine.SearchAsync(new HybridSearchEngineRequest
        {
            Query = "test3",
            GameId = gameId,
            MaxResults = 5,
            AbTestVariant = "SEMANTIC_HEAVY"
        });

        var metrics = engine.GetMetrics();

        // Assert
        metrics.Should().NotBeNull();
        metrics.VariantMetrics.Should().ContainKey("CONTROL");
        metrics.VariantMetrics.Should().ContainKey("SEMANTIC_HEAVY");
        metrics.VariantMetrics["CONTROL"].TotalSearches.Should().Be(2);
        metrics.VariantMetrics["SEMANTIC_HEAVY"].TotalSearches.Should().Be(1);
    }

    [Fact]
    public void GetConfiguration_ShouldReturnConfig()
    {
        // Arrange
        var customConfig = new HybridSearchEngineConfig
        {
            DefaultVectorWeight = 0.7f,
            DefaultKeywordWeight = 0.3f,
            MaxFetchLimit = 30,
            RerankTimeoutMs = 3000,
            AbTestingEnabled = true
        };

        var engine = new HybridSearchEngine(
            _mockHybridSearchService.Object,
            null,
            _mockLogger.Object,
            customConfig);

        // Act
        var config = engine.GetConfiguration();

        // Assert
        config.DefaultVectorWeight.Should().Be(0.7f);
        config.DefaultKeywordWeight.Should().Be(0.3f);
        config.MaxFetchLimit.Should().Be(30);
        config.RerankTimeoutMs.Should().Be(3000);
        config.AbTestingEnabled.Should().BeTrue();
    }

    #endregion

    #region Request/Response Records Tests

    [Fact]
    public void HybridSearchEngineRequest_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var request = new HybridSearchEngineRequest
        {
            Query = "test"
        };

        // Assert
        request.MaxResults.Should().Be(10);
        request.EnableReranking.Should().BeTrue();
        request.AbTestVariant.Should().BeNull();
        request.DocumentIds.Should().BeNull();
    }

    [Fact]
    public void HybridSearchEngineConfig_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var config = new HybridSearchEngineConfig();

        // Assert
        config.DefaultVectorWeight.Should().Be(0.6f);
        config.DefaultKeywordWeight.Should().Be(0.4f);
        config.MaxFetchLimit.Should().Be(20);
        config.RerankTimeoutMs.Should().Be(5000);
        config.AbTestingEnabled.Should().BeFalse();
    }

    [Fact]
    public void SearchExecutionMetrics_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var metrics = new SearchExecutionMetrics
        {
            TotalDurationMs = 100,
            SearchDurationMs = 80,
            RerankDurationMs = 20,
            ResultCount = 5,
            RerankerUsed = true
        };

        // Assert
        metrics.TotalDurationMs.Should().Be(100);
        metrics.SearchDurationMs.Should().Be(80);
        metrics.RerankDurationMs.Should().Be(20);
        metrics.ResultCount.Should().Be(5);
        metrics.RerankerUsed.Should().BeTrue();
        metrics.Error.Should().BeNull();
    }

    [Fact]
    public void VariantMetrics_AverageCalculations_ShouldBeCorrect()
    {
        // Arrange
        var metrics = new VariantMetrics
        {
            TotalSearches = 10,
            TotalSearchTimeMs = 500,
            TotalRerankTimeMs = 200,
            TotalResults = 50,
            RerankingUsedCount = 5
        };

        // Act & Assert
        metrics.AverageSearchTimeMs.Should().Be(50);
        metrics.AverageRerankTimeMs.Should().Be(40);
        metrics.AverageResults.Should().Be(5);
    }

    [Fact]
    public void VariantMetrics_WithZeroSearches_ShouldReturnZeroAverages()
    {
        // Arrange
        var metrics = new VariantMetrics();

        // Act & Assert
        metrics.AverageSearchTimeMs.Should().Be(0);
        metrics.AverageRerankTimeMs.Should().Be(0);
        metrics.AverageResults.Should().Be(0);
    }

    #endregion
}
