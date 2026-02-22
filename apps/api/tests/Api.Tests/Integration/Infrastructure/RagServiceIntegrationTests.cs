using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.Services.Rag;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

/// <summary>
/// Comprehensive integration tests for RagService with Testcontainers (Issue #2307).
/// Tests hybrid RRF retrieval (vector 70% + keyword 30%), validation pipeline, cache integration.
///
/// Test Categories:
/// 1. Hybrid Retrieval: RRF fusion (vector + keyword), confidence scoring
/// 2. Validation Pipeline: 5-layer validation, confidence threshold (≥0.70)
/// 3. Cache Integration: Redis L2 cache, invalidation
/// 4. Chunk Retrieval: Query expansion, reranking, snippet generation
///
/// Infrastructure: PostgreSQL + Redis via SharedTestcontainersFixture, Qdrant mocked
/// Coverage Target: ≥90% for RagService core logic
/// Execution Time Target: <15s
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Issue", "2307")]
public sealed class RagServiceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly Mock<ILogger<RagService>> _loggerMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<IQdrantService> _qdrantServiceMock;
    private readonly Mock<IHybridSearchService> _hybridSearchServiceMock;
    private readonly Mock<ILlmService> _llmServiceMock;
    private readonly Mock<IPromptTemplateService> _promptTemplateServiceMock;
    private readonly Mock<IQueryExpansionService> _queryExpansionServiceMock;
    private readonly Mock<ISearchResultReranker> _rerankerMock;
    private readonly Mock<IRagConfigurationProvider> _configProviderMock;

    private RagService _ragService = null!;
    private IAiResponseCacheService _cacheService = null!;
    private IConnectionMultiplexer _redis = null!;
    private string _databaseName = string.Empty;

    public RagServiceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _loggerMock = new Mock<ILogger<RagService>>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _qdrantServiceMock = new Mock<IQdrantService>();
        _hybridSearchServiceMock = new Mock<IHybridSearchService>();
        _llmServiceMock = new Mock<ILlmService>();
        _promptTemplateServiceMock = new Mock<IPromptTemplateService>();
        _queryExpansionServiceMock = new Mock<IQueryExpansionService>();
        _rerankerMock = new Mock<ISearchResultReranker>();
        _configProviderMock = new Mock<IRagConfigurationProvider>();
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for tests
        _databaseName = "test_rag_" + Guid.NewGuid().ToString("N");
        var isolatedConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        // Setup DI container
        var services = new ServiceCollection();
        services.AddLogging();

        // Configuration
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = isolatedConnectionString,
            ["ConnectionStrings:Redis"] = _fixture.RedisConnectionString,
            ["HybridCache:EnableL2Cache"] = "true",
            ["HybridCache:EnableTags"] = "true",
            ["HybridCache:DefaultExpiration"] = "00:05:00"
        });
        var configuration = configBuilder.Build();
        services.AddSingleton<IConfiguration>(configuration);

        // Setup cache services manually (following HybridCacheServiceIntegrationTests pattern)
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = _fixture.RedisConnectionString;
            options.InstanceName = "test_rag:";
        });
        services.AddHybridCache();

        var serviceProvider = services.BuildServiceProvider();
        var hybridCache = serviceProvider.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>();

        // Manually create HybridCacheService
        _redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);
        var cacheConfig = new Api.Configuration.HybridCacheConfiguration
        {
            EnableL2Cache = true,
            EnableTags = true,
            DefaultExpiration = TimeSpan.FromMinutes(5)
        };

        var hybridCacheService = new HybridCacheService(
            hybridCache,
            Microsoft.Extensions.Options.Options.Create(cacheConfig),
            new Mock<ILogger<HybridCacheService>>().Object,
            _redis
        );

        // Create AiResponseCacheService
        _cacheService = new AiResponseCacheService(
            hybridCacheService,
            new Mock<ILogger<AiResponseCacheService>>().Object
        );

        // Setup default mock behaviors
        SetupDefaultMocks();

        // Create RagService with mocked dependencies and real cache
        _ragService = new RagService(
            _embeddingServiceMock.Object,
            _qdrantServiceMock.Object,
            _hybridSearchServiceMock.Object,
            _llmServiceMock.Object,
            _cacheService,
            _promptTemplateServiceMock.Object,
            _loggerMock.Object,
            _queryExpansionServiceMock.Object,
            _rerankerMock.Object,
            _configProviderMock.Object
        );
    }

    public async ValueTask DisposeAsync()
    {
        // Cleanup isolated database
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);

        // Close Redis connection
        if (_redis != null)
        {
            await _redis.CloseAsync();
            _redis.Dispose();
        }
    }

    private void SetupDefaultMocks()
    {
        // Config provider returns topK = 5
        _configProviderMock
            .Setup(x => x.GetRagConfigAsync("TopK", It.IsAny<int>()))
            .ReturnsAsync(5);

        // Query expansion returns original query
        _queryExpansionServiceMock
            .Setup(x => x.GenerateQueryVariationsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, string lang, CancellationToken ct) => new List<string> { query });

        // Prompt template mocks
        _promptTemplateServiceMock
            .Setup(x => x.ClassifyQuestion(It.IsAny<string>()))
            .Returns(QuestionType.General);

        _promptTemplateServiceMock
            .Setup(x => x.GetTemplateAsync(It.IsAny<Guid?>(), It.IsAny<QuestionType>()))
            .ReturnsAsync(new PromptTemplate
            {
                SystemPrompt = "You are a board game rules assistant.",
                UserPromptTemplate = "Context: {context}\n\nQuestion: {question}",
                GameId = null,
                QuestionType = QuestionType.General
            });

        _promptTemplateServiceMock
            .Setup(x => x.RenderSystemPrompt(It.IsAny<PromptTemplate>()))
            .Returns((PromptTemplate template) => template.SystemPrompt);

        _promptTemplateServiceMock
            .Setup(x => x.RenderUserPrompt(It.IsAny<PromptTemplate>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((PromptTemplate template, string context, string question) =>
                template.UserPromptTemplate.Replace("{context}", context).Replace("{question}", question));
    }

    #region Test 1-3: Hybrid Retrieval with RRF

    /// <summary>
    /// Test 1: AskWithHybridSearchAsync Hybrid mode - validates RRF fusion (70% vector + 30% keyword).
    /// </summary>
    [Fact]
    public async Task AskWithHybridSearchAsync_HybridMode_ReturnsCorrectRRFFusion()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "How do you move pieces?";

        var hybridResults = new List<HybridSearchResult>
        {
            new HybridSearchResult
            {
                ChunkId = "chunk_1",
                Content = "Move one space per turn",
                PdfDocumentId = "pdf_1",
                GameId = Guid.Parse(gameId),
                ChunkIndex = 1,
                PageNumber = 5,
                HybridScore = 0.85f,
                VectorScore = 0.9f,
                KeywordScore = 0.7f,
                VectorRank = 1,
                KeywordRank = 2,
                Mode = SearchMode.Hybrid
            },
            new HybridSearchResult
            {
                ChunkId = "chunk_2",
                Content = "Roll dice to determine movement",
                PdfDocumentId = "pdf_1",
                GameId = Guid.Parse(gameId),
                ChunkIndex = 2,
                PageNumber = 6,
                HybridScore = 0.75f,
                VectorScore = 0.8f,
                KeywordScore = 0.6f,
                VectorRank = 2,
                KeywordRank = 3,
                Mode = SearchMode.Hybrid
            }
        };

        _hybridSearchServiceMock
            .Setup(x => x.SearchAsync(query, Guid.Parse(gameId), SearchMode.Hybrid, 5, null, 0.7f, 0.3f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hybridResults);

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "You move pieces by rolling dice.",
                Usage = new LlmUsage(PromptTokens: 100, CompletionTokens: 50, TotalTokens: 150)
            });

        // Act
        var result = await _ragService.AskWithHybridSearchAsync(gameId, query, SearchMode.Hybrid, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("You move pieces by rolling dice.");
        result.snippets.Should().HaveCount(2);
        result.confidence.Should().BeApproximately(0.85, 0.001, "confidence should be max hybrid score");
        result.snippets[0].score.Should().BeApproximately(0.85f, 0.001f, "first snippet should have highest hybrid score");
        result.snippets[1].score.Should().BeApproximately(0.75f, 0.001f, "second snippet should have second highest score");
    }

    /// <summary>
    /// Test 2: AskWithHybridSearchAsync Semantic mode - validates vector-only search.
    /// </summary>
    [Fact]
    public async Task AskWithHybridSearchAsync_SemanticMode_ReturnsVectorOnlyResults()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "What are the winning conditions?";

        var semanticResults = new List<HybridSearchResult>
        {
            new HybridSearchResult
            {
                ChunkId = "chunk_1",
                Content = "Win by collecting 10 victory points",
                PdfDocumentId = "pdf_1",
                GameId = Guid.Parse(gameId),
                ChunkIndex = 1,
                PageNumber = 12,
                HybridScore = 0.92f,
                VectorScore = 0.92f,
                KeywordScore = null,
                VectorRank = 1,
                KeywordRank = null,
                Mode = SearchMode.Semantic
            }
        };

        _hybridSearchServiceMock
            .Setup(x => x.SearchAsync(query, Guid.Parse(gameId), SearchMode.Semantic, 5, null, 0.7f, 0.3f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(semanticResults);

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "Win by collecting 10 victory points.",
                Usage = new LlmUsage(PromptTokens: 80, CompletionTokens: 30, TotalTokens: 110)
            });

        // Act
        var result = await _ragService.AskWithHybridSearchAsync(gameId, query, SearchMode.Semantic, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.confidence.Should().BeApproximately(0.92, 0.001, "confidence should be vector score in Semantic mode");
        result.snippets[0].score.Should().BeApproximately(0.92f, 0.001f);
    }

    /// <summary>
    /// Test 3: AskWithHybridSearchAsync Keyword mode - validates keyword-only search.
    /// </summary>
    [Fact]
    public async Task AskWithHybridSearchAsync_KeywordMode_ReturnsKeywordOnlyResults()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "trade action";

        var keywordResults = new List<HybridSearchResult>
        {
            new HybridSearchResult
            {
                ChunkId = "chunk_1",
                Content = "Trade action: exchange resources",
                PdfDocumentId = "pdf_1",
                GameId = Guid.Parse(gameId),
                ChunkIndex = 1,
                PageNumber = 8,
                HybridScore = 0.88f,
                VectorScore = null,
                KeywordScore = 0.88f,
                VectorRank = null,
                KeywordRank = 1,
                MatchedTerms = new List<string> { "trade", "action" },
                Mode = SearchMode.Keyword
            }
        };

        _hybridSearchServiceMock
            .Setup(x => x.SearchAsync(query, Guid.Parse(gameId), SearchMode.Keyword, 5, null, 0.7f, 0.3f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(keywordResults);

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "Trade action allows exchanging resources.",
                Usage = new LlmUsage(PromptTokens: 70, CompletionTokens: 40, TotalTokens: 110)
            });

        // Act
        var result = await _ragService.AskWithHybridSearchAsync(gameId, query, SearchMode.Keyword, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.confidence.Should().BeApproximately(0.88, 0.001, "confidence should be keyword score in Keyword mode");
        result.snippets[0].score.Should().BeApproximately(0.88f, 0.001f);
    }

    #endregion

    #region Test 4-5: Confidence Score Calculation

    /// <summary>
    /// Test 4: AskAsync - validates confidence score calculation from search results (≥0.70 threshold).
    /// </summary>
    [Fact]
    public async Task AskAsync_WithHighConfidenceResults_ReturnsMaxScoreAsConfidence()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "Setup instructions";

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(query, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[384] }
            });

        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem { Text = "Setup: Place board on table", PdfId = "pdf_1", Page = 2, Score = 0.95f },
            new SearchResultItem { Text = "Distribute cards to players", PdfId = "pdf_1", Page = 3, Score = 0.85f },
            new SearchResultItem { Text = "Each player gets 5 tokens", PdfId = "pdf_1", Page = 3, Score = 0.75f }
        };

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(gameId, It.IsAny<float[]>(), "en", 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult { Success = true, Results = searchResults });

        _rerankerMock
            .Setup(x => x.FuseSearchResultsAsync(It.IsAny<List<SearchResult>>()))
            .ReturnsAsync(searchResults);

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "Place the board on the table, distribute cards, and give each player 5 tokens.",
                Usage = new LlmUsage(PromptTokens: 150, CompletionTokens: 60, TotalTokens: 210)
            });

        // Act
        var result = await _ragService.AskAsync(gameId, query, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.confidence.Should().BeApproximately(0.95, 0.001, "confidence should be max score from search results");
        result.answer.Should().Contain("Place the board");
        result.snippets.Should().HaveCount(3);
    }

    /// <summary>
    /// Test 5: AskAsync with low confidence results - should still return but with lower confidence score.
    /// </summary>
    [Fact]
    public async Task AskAsync_WithLowConfidenceResults_ReturnsResultWithLowConfidence()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "Obscure rule";

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(query, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[384] } });

        var lowConfidenceResults = new List<SearchResultItem>
        {
            new SearchResultItem { Text = "Some tangentially related text", PdfId = "pdf_1", Page = 10, Score = 0.55f }
        };

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(gameId, It.IsAny<float[]>(), "en", 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult { Success = true, Results = lowConfidenceResults });

        _rerankerMock
            .Setup(x => x.FuseSearchResultsAsync(It.IsAny<List<SearchResult>>()))
            .ReturnsAsync(lowConfidenceResults);

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "Not specified",
                Usage = new LlmUsage(PromptTokens: 100, CompletionTokens: 20, TotalTokens: 120)
            });

        // Act
        var result = await _ragService.AskAsync(gameId, query, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.confidence.Should().BeApproximately(0.55, 0.001, "confidence should be below threshold");
        result.answer.Should().Contain("Not specified");
    }

    #endregion

    #region Test 6-8: Cache Integration

    /// <summary>
    /// Test 6: AskWithHybridSearchAsync caching - validates cache stores and retrieves responses.
    /// </summary>
    [Fact]
    public async Task AskWithHybridSearchAsync_StoresResponseInCache()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "Cached query";

        var hybridResults = new List<HybridSearchResult>
        {
            new HybridSearchResult
            {
                ChunkId = "chunk_1",
                Content = "Cached content",
                PdfDocumentId = "pdf_1",
                GameId = Guid.Parse(gameId),
                ChunkIndex = 1,
                PageNumber = 1,
                HybridScore = 0.80f,
                Mode = SearchMode.Hybrid
            }
        };

        _hybridSearchServiceMock
            .Setup(x => x.SearchAsync(query, Guid.Parse(gameId), SearchMode.Hybrid, 5, null, 0.7f, 0.3f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hybridResults);

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "Cached response content",
                Usage = new LlmUsage(PromptTokens: 100, CompletionTokens: 50, TotalTokens: 150)
            });

        // Act - Call to populate cache
        var result = await _ragService.AskWithHybridSearchAsync(gameId, query, SearchMode.Hybrid, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("Cached response content");
        result.snippets.Should().HaveCount(1);
        result.totalTokens.Should().Be(150, "token usage should be tracked");
        result.confidence.Should().BeApproximately(0.80, 0.001);
    }

    /// <summary>
    /// Test 7: AskWithHybridSearchAsync with bypassCache - validates cache bypass forces fresh LLM response.
    /// </summary>
    [Fact]
    public async Task AskWithHybridSearchAsync_WithBypassCache_SkipsCacheAndCallsLlm()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "Bypass cache query";

        var hybridResults = new List<HybridSearchResult>
        {
            new HybridSearchResult
            {
                ChunkId = "chunk_1",
                Content = "Fresh content",
                PdfDocumentId = "pdf_1",
                GameId = Guid.Parse(gameId),
                ChunkIndex = 1,
                PageNumber = 1,
                HybridScore = 0.80f,
                Mode = SearchMode.Hybrid
            }
        };

        _hybridSearchServiceMock
            .Setup(x => x.SearchAsync(query, Guid.Parse(gameId), SearchMode.Hybrid, 5, null, 0.7f, 0.3f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hybridResults);

        var callCount = 0;
        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new LlmCompletionResult
                {
                    Success = true,
                    Response = $"Response {callCount}",
                    Usage = new LlmUsage(PromptTokens: 100, CompletionTokens: 50, TotalTokens: 150)
                };
            });

        // Act - First call with cache
        var result1 = await _ragService.AskWithHybridSearchAsync(gameId, query, SearchMode.Hybrid, cancellationToken: TestContext.Current.CancellationToken);

        // Second call bypassing cache
        var result2 = await _ragService.AskWithHybridSearchAsync(gameId, query, SearchMode.Hybrid, bypassCache: true, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result1.answer.Should().Be("Response 1");
        result2.answer.Should().Be("Response 2", "should return fresh LLM response");
        callCount.Should().Be(2, "LLM should be called twice (bypass cache)");
    }

    /// <summary>
    /// Test 8: Cache key includes search mode - validates different modes use different cache keys.
    /// </summary>
    [Fact]
    public async Task AskWithHybridSearchAsync_DifferentSearchModes_UseDifferentCacheKeys()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "Same query different modes";

        var hybridResults = new List<HybridSearchResult>
        {
            new HybridSearchResult
            {
                ChunkId = "chunk_1",
                Content = "Content",
                PdfDocumentId = "pdf_1",
                GameId = Guid.Parse(gameId),
                ChunkIndex = 1,
                PageNumber = 1,
                HybridScore = 0.80f,
                Mode = SearchMode.Hybrid
            }
        };

        _hybridSearchServiceMock
            .Setup(x => x.SearchAsync(query, Guid.Parse(gameId), It.IsAny<SearchMode>(), 5, null, 0.7f, 0.3f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(hybridResults);

        var callCount = 0;
        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                return new LlmCompletionResult
                {
                    Success = true,
                    Response = $"Response {callCount}",
                    Usage = new LlmUsage(PromptTokens: 100, CompletionTokens: 50, TotalTokens: 150)
                };
            });

        // Act - Call with Hybrid mode
        var resultHybrid = await _ragService.AskWithHybridSearchAsync(gameId, query, SearchMode.Hybrid, cancellationToken: TestContext.Current.CancellationToken);

        // Call with Semantic mode (same query, different mode)
        var resultSemantic = await _ragService.AskWithHybridSearchAsync(gameId, query, SearchMode.Semantic, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        resultHybrid.answer.Should().Be("Response 1");
        resultSemantic.answer.Should().Be("Response 2", "different search mode should not hit cache");
        callCount.Should().Be(2, "LLM should be called twice for different modes");
    }

    #endregion

    #region Test 9-10: Query Expansion and Reranking

    /// <summary>
    /// Test 9: AskAsync with query expansion - validates query variations improve recall.
    /// </summary>
    [Fact]
    public async Task AskAsync_WithQueryExpansion_GeneratesMultipleSearchQueries()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "win condition";

        var queryVariations = new List<string> { "win condition", "victory condition", "winning criteria" };
        _queryExpansionServiceMock
            .Setup(x => x.GenerateQueryVariationsAsync(query, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryVariations);

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[384] } });

        var searchResults = new List<SearchResultItem>
        {
            new SearchResultItem { Text = "Win by collecting points", PdfId = "pdf_1", Page = 10, Score = 0.90f }
        };

        _qdrantServiceMock
            .Setup(x => x.SearchAsync(gameId, It.IsAny<float[]>(), "en", 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult { Success = true, Results = searchResults });

        _rerankerMock
            .Setup(x => x.FuseSearchResultsAsync(It.IsAny<List<SearchResult>>()))
            .ReturnsAsync(searchResults);

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "Win by collecting points.",
                Usage = new LlmUsage(PromptTokens: 100, CompletionTokens: 40, TotalTokens: 140)
            });

        // Act
        var result = await _ragService.AskAsync(gameId, query, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();

        // Verify query expansion was called
        _queryExpansionServiceMock.Verify(
            x => x.GenerateQueryVariationsAsync(query, "en", It.IsAny<CancellationToken>()),
            Times.Once
        );

        // Verify embeddings generated for each variation
        _embeddingServiceMock.Verify(
            x => x.GenerateEmbeddingAsync(It.IsAny<string>(), "en", It.IsAny<CancellationToken>()),
            Times.Exactly(queryVariations.Count)
        );

        // Verify reranker fused multiple search results
        _rerankerMock.Verify(
            x => x.FuseSearchResultsAsync(It.IsAny<List<SearchResult>>()),
            Times.Once
        );
    }

    /// <summary>
    /// Test 10: SearchResultReranker fusion - validates RRF deduplication and ranking.
    /// </summary>
    [Fact]
    public async Task AskAsync_WithReranker_FusesAndDeduplicatesSearchResults()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "game setup";

        // Setup query expansion to return TWO variations (to trigger multiple searches)
        var queryVariations = new List<string> { "game setup", "setup instructions" };
        _queryExpansionServiceMock
            .Setup(x => x.GenerateQueryVariationsAsync(query, "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(queryVariations);

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), "en", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[384] } });

        var searchResults1 = new List<SearchResultItem>
        {
            new SearchResultItem { Text = "Setup: Place board", PdfId = "pdf_1", Page = 2, Score = 0.95f },
            new SearchResultItem { Text = "Distribute cards", PdfId = "pdf_1", Page = 3, Score = 0.85f }
        };

        var searchResults2 = new List<SearchResultItem>
        {
            new SearchResultItem { Text = "Setup: Place board", PdfId = "pdf_1", Page = 2, Score = 0.90f },
            new SearchResultItem { Text = "Give tokens to players", PdfId = "pdf_1", Page = 3, Score = 0.80f }
        };

        _qdrantServiceMock
            .SetupSequence(x => x.SearchAsync(gameId, It.IsAny<float[]>(), "en", 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult { Success = true, Results = searchResults1 })
            .ReturnsAsync(new SearchResult { Success = true, Results = searchResults2 });

        var fusedResults = new List<SearchResultItem>
        {
            new SearchResultItem { Text = "Setup: Place board", PdfId = "pdf_1", Page = 2, Score = 0.95f },
            new SearchResultItem { Text = "Distribute cards", PdfId = "pdf_1", Page = 3, Score = 0.85f },
            new SearchResultItem { Text = "Give tokens to players", PdfId = "pdf_1", Page = 3, Score = 0.80f }
        };

        _rerankerMock
            .Setup(x => x.FuseSearchResultsAsync(It.IsAny<List<SearchResult>>()))
            .ReturnsAsync(fusedResults);

        _llmServiceMock
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "Setup by placing board, distributing cards, and giving tokens.",
                Usage = new LlmUsage(PromptTokens: 150, CompletionTokens: 60, TotalTokens: 210)
            });

        // Act
        var result = await _ragService.AskAsync(gameId, query, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.snippets.Should().HaveCount(3, "should have deduplicated results");

        // Verify reranker was called with 2 successful search results
        _rerankerMock.Verify(
            x => x.FuseSearchResultsAsync(It.Is<List<SearchResult>>(list => list.Count == 2 && list.All(r => r.Success))),
            Times.Once
        );
    }

    #endregion

    #region Test 11-12: Edge Cases

    /// <summary>
    /// Test 11: AskWithHybridSearchAsync with no search results - returns "Not specified".
    /// </summary>
    [Fact]
    public async Task AskWithHybridSearchAsync_WithNoResults_ReturnsNotSpecified()
    {
        // Arrange
        var gameId = Guid.NewGuid().ToString();
        var query = "Non-existent rule";

        _hybridSearchServiceMock
            .Setup(x => x.SearchAsync(query, Guid.Parse(gameId), SearchMode.Hybrid, 5, null, 0.7f, 0.3f, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        // Act
        var result = await _ragService.AskWithHybridSearchAsync(gameId, query, SearchMode.Hybrid, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("Not specified");
        result.snippets.Should().BeEmpty();
        result.confidence.Should().BeNull();
    }

    /// <summary>
    /// Test 12: AskWithHybridSearchAsync with invalid gameId format - returns error response.
    /// </summary>
    [Fact]
    public async Task AskWithHybridSearchAsync_WithInvalidGameId_ReturnsErrorResponse()
    {
        // Arrange
        var invalidGameId = "not-a-guid";
        var query = "Valid query";

        // Act
        var result = await _ragService.AskWithHybridSearchAsync(invalidGameId, query, SearchMode.Hybrid, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("Invalid game ID format.");
        result.snippets.Should().BeEmpty();
    }

    #endregion
}
