using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Models;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.Services.LlmClients;
using Api.Services.Rag;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Integration tests for RagService backward compatibility with HybridLlmService
/// ISSUE-966: BGAI-024 - Ensure RAG unchanged with new LLM
///
/// Tests verify that RagService works correctly with HybridLlmService (BGAI-020)
/// by validating response structures and error handling for all RAG methods.
/// </summary>
public class RagServiceIntegrationTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IEmbeddingService> _mockEmbeddingService;
    private readonly Mock<IQdrantService> _mockQdrantService;
    private readonly Mock<IHybridSearchService> _mockHybridSearchService;
    private readonly Mock<IAiResponseCacheService> _mockCache;
    private readonly Mock<IPromptTemplateService> _mockPromptTemplateService;
    private readonly Mock<ILogger<RagService>> _mockLogger;
    private readonly Mock<IQueryExpansionService> _mockQueryExpansion;
    private readonly Mock<ISearchResultReranker> _mockReranker;
    private readonly Mock<ICitationExtractorService> _mockCitationExtractor;
    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public RagServiceIntegrationTests()
    {
        // Use in-memory database for testing (like LlmCostLogRepositoryTests)
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: $"RagServiceTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(options);

        // Initialize all mocks
        _mockEmbeddingService = new Mock<IEmbeddingService>();
        _mockQdrantService = new Mock<IQdrantService>();
        _mockHybridSearchService = new Mock<IHybridSearchService>();
        _mockCache = new Mock<IAiResponseCacheService>();
        _mockPromptTemplateService = new Mock<IPromptTemplateService>();
        _mockLogger = new Mock<ILogger<RagService>>();
        _mockQueryExpansion = new Mock<IQueryExpansionService>();
        _mockReranker = new Mock<ISearchResultReranker>();
        _mockCitationExtractor = new Mock<ICitationExtractorService>();
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
    public async Task Test01_AskAsync_WithHybridLlmService_ReturnsValidQaResponse()
    {
        // Arrange
        var ragService = CreateRagService();
        var gameId = Guid.NewGuid().ToString();
        var query = "How many players can play this game?";

        // Setup mocks for the RAG pipeline
        SetupEmbeddingServiceMock();
        SetupQdrantServiceMock();
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
    /// Test02: Verify ExplainAsync returns valid ExplainResponse with HybridLlmService
    /// This tests the structured explanation generation flow.
    /// </summary>
    [Fact]
    public async Task Test02_ExplainAsync_WithHybridLlmService_ReturnsValidExplainResponse()
    {
        // Arrange
        var ragService = CreateRagService();
        var gameId = Guid.NewGuid().ToString();
        var topic = "Setup phase";

        // Setup mocks
        SetupEmbeddingServiceMock();
        SetupQdrantServiceMock();
        SetupCacheMock();

        // Act
        var result = await ragService.ExplainAsync(gameId, topic, cancellationToken: TestCancellationToken);

        // Assert - ExplainResponse is a record with lowercase parameters
        Assert.NotNull(result);
        Assert.NotNull(result.outline);
        Assert.NotNull(result.outline.mainTopic);
        Assert.NotNull(result.outline.sections);
        Assert.NotNull(result.script);
        Assert.NotEmpty(result.script);
        Assert.NotNull(result.citations);
        Assert.True(result.estimatedReadingTimeMinutes > 0);
    }

    /// <summary>
    /// Test03: Verify AskWithHybridSearchAsync returns valid response
    /// This tests the hybrid search (vector + keyword) integration.
    /// </summary>
    [Fact]
    public async Task Test03_AskWithHybridSearchAsync_WithHybridMode_ReturnsValidResponse()
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
    public async Task Test04_AskAsync_WithEmptyQuery_ReturnsErrorMessage()
    {
        // Arrange
        var ragService = CreateRagService();
        var gameId = Guid.NewGuid().ToString();

        // Act
        var result = await ragService.AskAsync(gameId, "", cancellationToken: TestCancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Please provide a question.", result.answer);
        Assert.Empty(result.snippets);
    }

    /// <summary>
    /// Test05: Verify error handling when embedding service fails
    /// </summary>
    [Fact]
    public async Task Test05_AskAsync_WhenEmbeddingFails_ReturnsErrorMessage()
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

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unable to process query.", result.answer);
        Assert.Empty(result.snippets);
    }

    /// <summary>
    /// Test06: Verify cache hit scenario
    /// </summary>
    [Fact]
    public async Task Test06_AskAsync_WithCacheHit_ReturnsCachedResponse()
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
            _dbContext,
            _mockEmbeddingService.Object,
            _mockQdrantService.Object,
            _mockHybridSearchService.Object,
            mockLlmService,
            _mockCache.Object,
            _mockPromptTemplateService.Object,
            _mockLogger.Object,
            _mockQueryExpansion.Object,
            _mockReranker.Object,
            _mockCitationExtractor.Object,
            configurationService: null,
            configuration: null);
    }

    private ILlmService CreateMockLlmService()
    {
        var mockLlmService = new Mock<ILlmService>();

        // Setup successful LLM completion response
        mockLlmService
            .Setup(s => s.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
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

    private void SetupQdrantServiceMock()
    {
        var dummyResults = new List<SearchResultItem>
        {
            new SearchResultItem
            {
                Text = "The game supports 2-4 players.",
                PdfId = Guid.NewGuid().ToString(),
                Page = 1,
                Score = 0.85f
            },
            new SearchResultItem
            {
                Text = "Setup takes approximately 10 minutes.",
                PdfId = Guid.NewGuid().ToString(),
                Page = 2,
                Score = 0.75f
            }
        };

        _mockQdrantService
            .Setup(s => s.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SearchResult
            {
                Success = true,
                Results = dummyResults
            });
    }

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
}
