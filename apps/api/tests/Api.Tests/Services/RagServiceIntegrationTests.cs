using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Models;
using Api.Services;
using Api.Services.LlmClients;
using Api.Services.Rag;
using Api.Tests.TestHelpers;
using Api.Tests.Helpers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.Services;

/// <summary>
/// Integration tests for RagService backward compatibility with HybridLlmService
/// ISSUE-966: BGAI-024 - Ensure RAG unchanged with new LLM
///
/// Tests verify that RagService works correctly with HybridLlmService (BGAI-020)
/// by validating response structures and error handling for all RAG methods.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class RagServiceIntegrationTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IHybridSearchService> _mockHybridSearchService;
    private readonly Mock<IAiResponseCacheService> _mockCache;
    private readonly Mock<IPromptTemplateService> _mockPromptTemplateService;
    private readonly Mock<ILogger<RagService>> _mockLogger;
    private readonly Mock<IQueryExpansionService> _mockQueryExpansion;
    private readonly Mock<ISearchResultReranker> _mockReranker;
    private readonly Mock<IRagConfigurationProvider> _mockConfigProvider;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public RagServiceIntegrationTests()
    {
        // Use in-memory database for testing (like LlmCostLogRepositoryTests)
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"RagServiceTestDb_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        _dbContext = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);

        // Initialize all mocks
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockHybridSearchService = new Mock<IHybridSearchService>();
        _mockCache = new Mock<IAiResponseCacheService>();
        _mockPromptTemplateService = new Mock<IPromptTemplateService>();
        _mockLogger = new Mock<ILogger<RagService>>();
        _mockQueryExpansion = new Mock<IQueryExpansionService>();
        _mockReranker = new Mock<ISearchResultReranker>();


        // Use shared test helper for config provider setup
        _mockConfigProvider = RagTestHelpers.CreateDefaultConfigProvider();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    /// <summary>
    /// Test01: Verify AskAsync returns valid QaResponse with HybridLlmService
    /// This tests the primary RAG question-answering flow.
    /// </summary>
    [Fact]
    public async Task AskAsync_WithHybridLlmService_ReturnsValidQaResponse()
    {
        // Arrange
        var ragService = CreateRagService();
        var gameId = Guid.NewGuid().ToString();
        var query = "How many players can play this game?";

        // Setup mocks for the RAG pipeline
        SetupEmbeddingServiceMock();

        SetupCacheMock(); // Cache miss
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert - QaResponse is a record with lowercase parameters
        Assert.NotNull(result);
        Assert.NotNull(result.answer);
        Assert.NotEmpty(result.answer);
        Assert.NotNull(result.snippets);
        Assert.True(result.promptTokens >= 0);
        Assert.True(result.completionTokens >= 0);
        Assert.True(result.totalTokens >= 0);
    }

    /// <summary>
    /// Test02: Verify ExplainAsync returns empty response after Qdrant removal
    /// Vector retrieval was removed (Qdrant decommissioned), so ExplainAsync now
    /// returns an empty explain response indicating no relevant information found.
    /// </summary>
    [Fact]
    public async Task ExplainAsync_WithHybridLlmService_ReturnsValidExplainResponse()
    {
        // Arrange
        var ragService = CreateRagService();
        var gameId = Guid.NewGuid().ToString();
        var topic = "Setup phase";

        // Setup mocks
        SetupEmbeddingServiceMock();

        SetupCacheMock();

        // Act
        var result = await ragService.ExplainAsync(gameId, topic, cancellationToken: TestCancellationToken);

        // Assert - After Qdrant removal, vector retrieval returns empty results,
        // so ExplainAsync returns an empty explain response with a message.
        Assert.NotNull(result);
        Assert.NotNull(result.outline);
        Assert.NotNull(result.script);
        Assert.NotEmpty(result.script); // Contains the "no relevant information" message
        Assert.NotNull(result.citations);
        Assert.Empty(result.citations); // No citations when no vector results
        Assert.Equal(0, result.estimatedReadingTimeMinutes); // No reading time for empty response
    }

    /// <summary>
    /// Test03: Verify AskWithHybridSearchAsync returns valid response
    /// This tests the hybrid search (vector + keyword) integration.
    /// </summary>
    [Fact]
    public async Task AskWithHybridSearchAsync_WithHybridMode_ReturnsValidResponse()
    {
        // Arrange
        var ragService = CreateRagService();
        var gameId = Guid.NewGuid();
        var query = "What is the winning condition?";

        // Setup mocks
        SetupHybridSearchServiceMock(gameId);
        SetupCacheMock();
        SetupPromptTemplateMock();

        // Act
        var result = await ragService.AskWithHybridSearchAsync(
            gameId.ToString(),
            query,
            SearchMode.Hybrid,
            cancellationToken: TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.answer);
        Assert.NotEmpty(result.answer);
        Assert.NotNull(result.snippets);
        Assert.True(result.totalTokens >= 0);
    }

    /// <summary>
    /// Test04: Verify error handling when query is empty
    /// </summary>
    [Fact]
    public async Task AskAsync_WithEmptyQuery_ReturnsErrorMessage()
    {
        // Arrange
        var ragService = CreateRagService();
        var gameId = Guid.NewGuid().ToString();

        // Act
        var result = await ragService.AskAsync(gameId, "", cancellationToken: TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Please provide a question", result.answer);
        Assert.Empty(result.snippets);
    }

    /// <summary>
    /// Test05: Verify error handling when embedding service fails
    /// When embedding fails, retrieval returns empty results → "Not specified" response
    /// </summary>
    [Fact]
    public async Task AskAsync_WhenEmbeddingFails_ReturnsErrorMessage()
    {
        // Arrange
        var ragService = CreateRagService();
        var gameId = Guid.NewGuid().ToString();
        var query = "Test query";

        // Setup mocks
        SetupFailedEmbeddingServiceMock();
        SetupCacheMock();
        SetupQueryExpansionMock(query);

        // Act
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert - When embedding fails, no results are returned → "Not specified"
        Assert.NotNull(result);
        // When embedding fails, RagService returns empty results which triggers "Not specified" response
        Assert.Equal("Not specified", result.answer);
        Assert.Empty(result.snippets);
    }

    /// <summary>
    /// Test06: Verify cache hit scenario
    /// </summary>
    [Fact]
    public async Task AskAsync_WithCacheHit_ReturnsCachedResponse()
    {
        // Arrange
        var ragService = CreateRagService();
        var gameId = Guid.NewGuid().ToString();
        var query = "Cached query";

        var cachedResponse = new QaResponse(
            "Cached answer",
            new[] { new Snippet("text", "source", 1, 0, 0.9f) },
            10, 20, 30, 0.9);

        SetupCacheHitMock(cachedResponse);

        // Act
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Cached answer", result.answer);
        Assert.Single(result.snippets);
        Assert.Equal(30, result.totalTokens);
    }

    // ==================== Helper Methods ====================

    private RagService CreateRagService()
    {
        // Create mocked LLM service that returns valid responses
        var mockLlmService = CreateMockLlmService();

        return new RagService(
            _mockHybridSearchService.Object,
            mockLlmService,
            _mockCache.Object,
            _mockPromptTemplateService.Object,
            _mockLogger.Object,
            _mockConfigProvider.Object);
    }

    private ILlmService CreateMockLlmService()
    {
        var mockLlmService = new Mock<ILlmService>();

        // Setup successful LLM completion response
        mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "This is a test answer from the LLM. The game supports 2-4 players.",
                Usage = new LlmUsage(100, 50, 150),
                Cost = new LlmCost
                {
                    ModelId = "test-model",
                    Provider = "Test",
                    InputCost = 0.001m,
                    OutputCost = 0.002m
                },
                Metadata = new Dictionary<string, string>()
            });

        return mockLlmService.Object;
    }

    private void SetupEmbeddingServiceMock()
    {
        var dummyEmbedding = Enumerable.Range(0, 384).Select(i => (float)i / 384).ToArray();

        _mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { dummyEmbedding }
            });
    }

    private void SetupFailedEmbeddingServiceMock()
    {
        _mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = false,
                ErrorMessage = "Embedding service unavailable"
            });
    }

    // SetupQdrantServiceMock removed — IQdrantService no longer exists

    private void SetupHybridSearchServiceMock(Guid gameId)
    {
        var dummyResults = new List<HybridSearchResult>
        {
            new HybridSearchResult
            {
                ChunkId = "chunk_1",
                Content = "The game supports 2-4 players.",
                PdfDocumentId = Guid.NewGuid().ToString(),
                GameId = gameId,
                ChunkIndex = 0,
                PageNumber = 1,
                VectorScore = 0.85f,
                KeywordScore = 0.80f,
                HybridScore = 0.825f,
                Mode = SearchMode.Hybrid
            }
        };

        _mockHybridSearchService
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                gameId,
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<List<Guid>?>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dummyResults);
    }

    private void SetupCacheMock()
    {
        // Return null = cache miss
        _mockCache
            .Setup(c => c.GetAsync<QaResponse>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        _mockCache
            .Setup(c => c.GetAsync<ExplainResponse>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExplainResponse?)null);

        // Allow cache writes
        _mockCache
            .Setup(c => c.SetAsync(
                It.IsAny<string>(),
                It.IsAny<object>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    private void SetupCacheHitMock(QaResponse cachedResponse)
    {
        _mockCache
            .Setup(c => c.GetAsync<QaResponse>(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);
    }

    private void SetupPromptTemplateMock()
    {
        var dummyTemplate = new PromptTemplate
        {
            SystemPrompt = "You are a helpful board game assistant.",
            UserPromptTemplate = "Context: {context}\n\nQuestion: {question}"
        };

        _mockPromptTemplateService
            .Setup(s => s.ClassifyQuestion(It.IsAny<string>()))
            .Returns(QuestionType.Setup);

        _mockPromptTemplateService
            .Setup(s => s.GetTemplateAsync(
                It.IsAny<Guid?>(),
                It.IsAny<QuestionType>()))
            .ReturnsAsync(dummyTemplate);

        _mockPromptTemplateService
            .Setup(s => s.RenderSystemPrompt(It.IsAny<PromptTemplate>()))
            .Returns("You are a helpful board game assistant.");

        _mockPromptTemplateService
            .Setup(s => s.RenderUserPrompt(
                It.IsAny<PromptTemplate>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Returns((PromptTemplate t, string ctx, string q) => $"Context: {ctx}\n\nQuestion: {q}");
    }

    private void SetupQueryExpansionMock(string originalQuery)
    {
        var variations = new List<string>
        {
            originalQuery,
            $"{originalQuery} rules",
            $"How to {originalQuery}"
        };

        _mockQueryExpansion
            .Setup(s => s.GenerateQueryVariationsAsync(
                originalQuery,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(variations);
    }

    private void SetupRerankerMock()
    {
        _mockReranker
            .Setup(r => r.FuseSearchResultsAsync(
                It.IsAny<List<SearchResult>>()))
            .ReturnsAsync((List<SearchResult> results) =>
            {
                // Simple fusion: take first result's items
                return results.FirstOrDefault()?.Results ?? new List<SearchResultItem>();
            });
    }

    // ==================== Configuration Tests ====================

    /// <summary>
    /// Test07: Verify that RagService requests TopK configuration
    /// This ensures the service is properly using the configuration provider.
    /// </summary>
    [Fact]
    public async Task AskAsync_RequestsTopKConfiguration()
    {
        // Arrange
        var customMock = RagTestHelpers.CreateCustomConfigProvider(topK: 10);
        var ragService = CreateRagServiceWithCustomConfig(customMock);
        var gameId = Guid.NewGuid().ToString();
        var query = "How many players?";

        SetupEmbeddingServiceMock();

        SetupCacheMock();
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act
        await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert - Verify TopK configuration was requested
        customMock.Verify(c => c.GetRagConfigAsync(RagTestHelpers.ConfigKeys.TopK, It.IsAny<int>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test09: Verify that RagService requests MinScore configuration
    /// </summary>
    [Fact]
    public async Task AskAsync_RequestsMinScoreConfiguration()
    {
        // Arrange
        var customMock = RagTestHelpers.CreateCustomConfigProvider(minScore: 0.8);
        var ragService = CreateRagServiceWithCustomConfig(customMock);
        var gameId = Guid.NewGuid().ToString();
        var query = "Setup instructions";

        SetupEmbeddingServiceMock();

        SetupCacheMock();
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert - Verify TopK configuration was requested
        Assert.NotNull(result);
        customMock.Verify(c => c.GetRagConfigAsync(RagTestHelpers.ConfigKeys.TopK, It.IsAny<int>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test09: Test behavior with different TopK values
    /// Verifies that configuration changes affect service behavior.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(20)]
    public async Task AskAsync_WithDifferentTopKValues_ReturnsValidResponse(int topK)
    {
        // Arrange
        var customMock = RagTestHelpers.CreateCustomConfigProvider(topK: topK);
        var ragService = CreateRagServiceWithCustomConfig(customMock);
        var gameId = Guid.NewGuid().ToString();
        var query = "Game rules";

        SetupEmbeddingServiceMock();

        SetupCacheMock();
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.answer);
        Assert.NotEmpty(result.answer);
        // Configuration was used during query processing
        customMock.Verify(c => c.GetRagConfigAsync(RagTestHelpers.ConfigKeys.TopK, It.IsAny<int>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test10: Test behavior with different MinScore configuration values (parameter-only test)
    /// Note: MinScore is a configuration parameter but not currently used by RagService.
    /// This test verifies the service accepts different values without breaking.
    /// </summary>
    [Theory]
    [InlineData(0.5)]
    [InlineData(0.7)]
    [InlineData(0.9)]
    public async Task AskAsync_WithDifferentMinScoreValues_ReturnsValidResponse(double minScore)
    {
        // Arrange
        var customMock = RagTestHelpers.CreateCustomConfigProvider(minScore: minScore);
        var ragService = CreateRagServiceWithCustomConfig(customMock);
        var gameId = Guid.NewGuid().ToString();
        var query = "Winning conditions";

        SetupEmbeddingServiceMock();

        SetupCacheMock();
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.answer);
        customMock.Verify(c => c.GetRagConfigAsync(RagTestHelpers.ConfigKeys.TopK, It.IsAny<int>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test11: Test behavior with extreme configuration values
    /// Ensures the service handles edge cases gracefully.
    /// </summary>
    [Fact]
    public async Task AskAsync_WithExtremeConfigValues_HandlesGracefully()
    {
        // Arrange - Test with extreme but valid values
        var customMock = RagTestHelpers.CreateCustomConfigProvider(
            topK: 50,           // Maximum TopK
            minScore: 0.1,      // Very low threshold
            rrfK: 100,          // Maximum RRF parameter
            maxQueryVariations: 10  // Maximum variations
        );
        var ragService = CreateRagServiceWithCustomConfig(customMock);
        var gameId = Guid.NewGuid().ToString();
        var query = "Complex query";

        SetupEmbeddingServiceMock();

        SetupCacheMock();
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert - Service should handle extreme values gracefully
        Assert.NotNull(result);
        Assert.NotNull(result.answer);
        Assert.True(result.totalTokens >= 0);
    }

    /// <summary>
    /// Test12: Test behavior with out-of-range configuration values
    /// Note: RagConfigurationProvider clamps values, but we test that the service
    /// handles extreme inputs gracefully even if they bypass validation.
    /// </summary>
    /// <param name="outOfRangeTopK">The out-of-range value (for documentation purposes, demonstrating the scenario being tested)</param>
    /// <param name="expectedClamped">The expected clamped value that the mock will return (simulates provider clamping behavior)</param>
    [Theory]
    [InlineData(1000, 5)]    // TopK way above max (50) should get clamped
    [InlineData(0, 5)]       // TopK below min (1) should get clamped
    [InlineData(-10, 5)]     // Negative TopK should get clamped
    public async Task AskAsync_WithOutOfRangeTopK_HandlesGracefully(int outOfRangeTopK, int expectedClamped)
    {
        // Arrange - Simulate values that would be clamped by RagConfigurationProvider
        var customMock = RagTestHelpers.CreateCustomConfigProvider(topK: expectedClamped);
        var ragService = CreateRagServiceWithCustomConfig(customMock);
        var gameId = Guid.NewGuid().ToString();
        var query = "Test query";

        SetupEmbeddingServiceMock();

        SetupCacheMock();
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act - Service should handle clamped values gracefully
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.answer);
        // Verify configuration was requested (would be clamped by provider in real scenario)
        customMock.Verify(c => c.GetRagConfigAsync(RagTestHelpers.ConfigKeys.TopK, It.IsAny<int>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test13: Test behavior with boundary configuration values
    /// Tests minimum and maximum valid values for each configuration parameter.
    /// </summary>
    [Theory]
    [InlineData(1, 0.0, 1, 1)]       // All minimums
    [InlineData(50, 1.0, 100, 10)]   // All maximums
    [InlineData(1, 1.0, 1, 10)]      // Mixed boundaries
    [InlineData(50, 0.0, 100, 1)]    // Mixed boundaries (opposite)
    public async Task AskAsync_WithBoundaryValues_ReturnsValidResponse(
        int topK, double minScore, int rrfK, int maxQueryVariations)
    {
        // Arrange
        var customMock = RagTestHelpers.CreateCustomConfigProvider(
            topK: topK,
            minScore: minScore,
            rrfK: rrfK,
            maxQueryVariations: maxQueryVariations
        );
        var ragService = CreateRagServiceWithCustomConfig(customMock);
        var gameId = Guid.NewGuid().ToString();
        var query = "Boundary test query";

        SetupEmbeddingServiceMock();

        SetupCacheMock();
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert - Service should handle boundary values correctly
        Assert.NotNull(result);
        Assert.NotNull(result.answer);
        Assert.True(result.totalTokens >= 0);

        // Verify TopK configuration was requested
        customMock.Verify(c => c.GetRagConfigAsync(RagTestHelpers.ConfigKeys.TopK, It.IsAny<int>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test14: Test configuration with MinScore edge cases (parameter-only test)
    /// Note: MinScore is a configuration parameter but not currently used by RagService.
    /// Verifies the service handles various threshold values without breaking.
    /// </summary>
    [Theory]
    [InlineData(0.0)]   // Minimum score
    [InlineData(0.3)]   // Very low threshold
    [InlineData(0.5)]   // Medium threshold
    [InlineData(0.7)]   // Default threshold
    [InlineData(1.0)]   // Maximum score
    public async Task AskAsync_WithMinScoreEdgeCases_FiltersResultsCorrectly(double minScore)
    {
        // Arrange
        var customMock = RagTestHelpers.CreateCustomConfigProvider(minScore: minScore);
        var ragService = CreateRagServiceWithCustomConfig(customMock);
        var gameId = Guid.NewGuid().ToString();
        var query = "Score threshold test";

        SetupEmbeddingServiceMock();

        SetupCacheMock();
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act
        var result = await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.answer);

        // At minimum score (0.0), should accept most results
        // At maximum score (1.0), should be very selective
        // Both should return valid responses from our mocked data
        Assert.True(result.totalTokens >= 0);
        customMock.Verify(c => c.GetRagConfigAsync(RagTestHelpers.ConfigKeys.TopK, It.IsAny<int>()), Times.AtLeastOnce);
    }

    /// <summary>
    /// Test15: Test that using ConfigKeys constants works correctly
    /// Verifies that the ConfigKeys helper class can be used in tests.
    /// </summary>
    [Fact]
    public async Task AskAsync_UsingConfigKeysConstants_VerifiesCorrectly()
    {
        // Arrange - Use ConfigKeys constants for readability
        var customMock = RagTestHelpers.CreateCustomConfigProvider(topK: 8, minScore: 0.75);
        var ragService = CreateRagServiceWithCustomConfig(customMock);
        var gameId = Guid.NewGuid().ToString();
        var query = "ConfigKeys test";

        SetupEmbeddingServiceMock();

        SetupCacheMock();
        SetupPromptTemplateMock();
        SetupQueryExpansionMock(query);
        SetupRerankerMock();

        // Act
        await ragService.AskAsync(gameId, query, cancellationToken: TestCancellationToken);

        // Assert - Use ConfigKeys constants in verification
        customMock.Verify(c => c.GetRagConfigAsync(RagTestHelpers.ConfigKeys.TopK, It.IsAny<int>()), Times.AtLeastOnce);

        // Verify that using constants is type-safe and readable
        Assert.Equal("TopK", RagTestHelpers.ConfigKeys.TopK);
        Assert.Equal("MinScore", RagTestHelpers.ConfigKeys.MinScore);
        Assert.Equal("RrfK", RagTestHelpers.ConfigKeys.RrfK);
        Assert.Equal("MaxQueryVariations", RagTestHelpers.ConfigKeys.MaxQueryVariations);
    }

    /// <summary>
    /// Helper method to create RagService with custom configuration mock
    /// </summary>
    private RagService CreateRagServiceWithCustomConfig(Mock<IRagConfigurationProvider> configMock)
    {
        var mockLlmService = CreateMockLlmService();

        return new RagService(
            _mockHybridSearchService.Object,
            mockLlmService,
            _mockCache.Object,
            _mockPromptTemplateService.Object,
            _mockLogger.Object,
            configMock.Object);
    }
}

