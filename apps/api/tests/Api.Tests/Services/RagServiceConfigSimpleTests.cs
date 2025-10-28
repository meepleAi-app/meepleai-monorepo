using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.Services.Rag;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// CONFIG-04: Simplified unit tests for RagService dynamic configuration
/// Tests appsettings.json fallback and hardcoded defaults (no database tests due to FK complexity)
/// Database configuration integration tested in ConfigurationServiceTests
/// </summary>
public class RagServiceConfigSimpleTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<IEmbeddingService> _mockEmbedding;
    private readonly Mock<IQdrantService> _mockQdrant;
    private readonly Mock<ILlmService> _mockLlm;
    private readonly Mock<IAiResponseCacheService> _mockCache;
    private readonly Mock<IPromptTemplateService> _mockPromptTemplate;
    private readonly Mock<ILogger<RagService>> _mockLogger;

    public RagServiceConfigSimpleTests()
    {
        // Setup in-memory SQLite database (minimal, no complex schema)
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new MeepleAiDbContext(options);
        _dbContext.Database.EnsureCreated();

        // Setup mocks
        _mockEmbedding = new Mock<IEmbeddingService>();
        _mockQdrant = new Mock<IQdrantService>();
        _mockLlm = new Mock<ILlmService>();
        _mockCache = new Mock<IAiResponseCacheService>();
        _mockPromptTemplate = new Mock<IPromptTemplateService>();
        _mockLogger = new Mock<ILogger<RagService>>();
    }

    public void Dispose()
    {
        _dbContext?.Dispose();
        _connection?.Dispose();
    }

    #region Appsettings Configuration Tests

    [Theory]
    [InlineData("8", 8)]
    [InlineData("12", 12)]
    [InlineData("1", 1)]   // Min boundary
    [InlineData("50", 50)] // Max boundary
    public async Task GetRagConfig_AppsettingsTopK_ShouldUseValue(string configValue, int expectedTopK)
    {
        // Arrange: appsettings provides TopK
        var appsettings = new Dictionary<string, string?> { { "RAG:TopK", configValue } };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appsettings)
            .Build();

        var ragService = CreateRagService(configuration: configuration);
        SetupBasicMocks();

        // Act
        var result = await ragService.AskAsync("test-game", "test query");

        // Assert
        _mockQdrant.Verify(q => q.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<float[]>(),
            It.IsAny<string>(),
            expectedTopK,
            It.IsAny<CancellationToken>()
        ), Times.AtLeastOnce);
    }

    [Theory]
    [InlineData("-5", 1)]   // Negative → clamped to 1
    [InlineData("0", 1)]    // Zero → clamped to 1
    [InlineData("100", 50)] // Over max → clamped to 50
    [InlineData("999", 50)] // Way over → clamped to 50
    public async Task GetRagConfig_AppsettingsTopK_OutOfRange_ShouldClamp(string configValue, int expectedClamped)
    {
        // Arrange
        var appsettings = new Dictionary<string, string?> { { "RAG:TopK", configValue } };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appsettings)
            .Build();

        var ragService = CreateRagService(configuration: configuration);
        SetupBasicMocks();

        // Act
        var result = await ragService.AskAsync("test-game", "test query");

        // Assert: Should clamp to valid range
        _mockQdrant.Verify(q => q.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<float[]>(),
            It.IsAny<string>(),
            expectedClamped,
            It.IsAny<CancellationToken>()
        ), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetRagConfig_AppsettingsMaxQueryVariations_ShouldLimitVariations()
    {
        // Arrange
        var appsettings = new Dictionary<string, string?> { { "RAG:MaxQueryVariations", "2" } };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appsettings)
            .Build();

        var ragService = CreateRagService(configuration: configuration);
        SetupBasicMocks();

        // Act
        var result = await ragService.AskAsync("test-game", "how to win");

        // Assert: Should generate max 2 query variations
        _mockEmbedding.Verify(e => e.GenerateEmbeddingAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()
        ), Times.AtMost(2));
    }

    #endregion

    #region Hardcoded Default Tests

    [Fact]
    public async Task GetRagConfig_NoConfigProvided_ShouldUseDefaults()
    {
        // Arrange: No config services
        var ragService = CreateRagService(); // No config
        SetupBasicMocks();

        // Act
        var result = await ragService.AskAsync("test-game", "test query");

        // Assert: Should use hardcoded default TopK=5
        _mockQdrant.Verify(q => q.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<float[]>(),
            It.IsAny<string>(),
            5, // DefaultTopK
            It.IsAny<CancellationToken>()
        ), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetRagConfig_NoConfigProvided_DefaultMaxQueryVariations()
    {
        // Arrange
        var ragService = CreateRagService();
        SetupBasicMocks();

        // Act
        var result = await ragService.AskAsync("test-game", "how to win the game");

        // Assert: Should use default MaxQueryVariations=4
        _mockEmbedding.Verify(e => e.GenerateEmbeddingAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()
        ), Times.AtMost(4));
    }

    #endregion

    #region ExplainAsync Configuration Tests

    [Fact]
    public async Task ExplainAsync_AppsettingsTopK_ShouldUseValue()
    {
        // Arrange
        var appsettings = new Dictionary<string, string?> { { "RAG:TopK", "7" } };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(appsettings)
            .Build();

        var ragService = CreateRagService(configuration: configuration);

        // Setup mocks for ExplainAsync
        _mockEmbedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult { Success = true, Embeddings = new List<float[]> { new float[] { 0.1f } } });

        _mockQdrant.Setup(q => q.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { Text = "Context", PdfId = "pdf", Page = 1, ChunkIndex = 0, Score = 0.9f }
            }));

        _mockLlm.Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "{\"sections\":[{\"title\":\"Test\",\"content\":\"Content\"}]}",
                Usage = new LlmUsage(10, 20, 30)
            });

        _mockCache.Setup(c => c.GetAsync<ExplainResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExplainResponse?)null);

        _mockPromptTemplate.Setup(p => p.GetTemplateAsync(It.IsAny<Guid?>(), It.IsAny<QuestionType>()))
            .ReturnsAsync(new PromptTemplate { SystemPrompt = "System", UserPromptTemplate = "User: {query}" });

        // Act
        var result = await ragService.ExplainAsync("test-game", "test topic");

        // Assert: SearchAsync should be called with limit=7
        _mockQdrant.Verify(q => q.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<float[]>(),
            It.IsAny<string>(),
            7, // TopK from appsettings
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    #endregion

    #region Backward Compatibility Tests

    [Fact]
    public async Task RagService_OldStyleConstructor_ShouldWorkWithDefaults()
    {
        // Arrange: Old-style constructor with AI-14 hybrid search
        var mockHybridSearch = new Mock<IHybridSearchService>();
        mockHybridSearch.Setup(x => x.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<SearchMode>(),
            It.IsAny<int>(),
            It.IsAny<float>(),
            It.IsAny<float>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        var ragService = new RagService(
            _dbContext,
            _mockEmbedding.Object,
            _mockQdrant.Object,
            mockHybridSearch.Object,
            _mockLlm.Object,
            _mockCache.Object,
            _mockPromptTemplate.Object,
            _mockLogger.Object,
            CreateQueryExpansionMock().Object,
            CreateRerankerMock().Object,
            CreateCitationExtractorMock().Object
            // No configService, no configuration
        );

        SetupBasicMocks();

        // Act
        var result = await ragService.AskAsync("test-game", "test query");

        // Assert: Should work with hardcoded defaults
        Assert.NotNull(result);
        _mockQdrant.Verify(q => q.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<float[]>(),
            It.IsAny<string>(),
            5, // Default TopK
            It.IsAny<CancellationToken>()
        ), Times.AtLeastOnce);
    }

    #endregion

    #region Helper Methods

    // AI-14: Helper to create IQueryExpansionService mock (pass-through)
    private static Mock<IQueryExpansionService> CreateQueryExpansionMock()
    {
        var mock = new Mock<IQueryExpansionService>();
        mock.Setup(x => x.GenerateQueryVariationsAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync((string query, string _, CancellationToken __) => new List<string> { query });
        return mock;
    }

    // AI-14: Helper to create ISearchResultReranker mock (pass-through)
    private static Mock<ISearchResultReranker> CreateRerankerMock()
    {
        var mock = new Mock<ISearchResultReranker>();
        mock.Setup(x => x.FuseSearchResultsAsync(It.IsAny<List<SearchResult>>()))
            .ReturnsAsync((List<SearchResult> results) =>
                results.SelectMany(r => r.Results).ToList());
        return mock;
    }

    // AI-14: Helper to create ICitationExtractorService mock (pass-through)
    private static Mock<ICitationExtractorService> CreateCitationExtractorMock()
    {
        var mock = new Mock<ICitationExtractorService>();
        mock.Setup(x => x.ValidateCitations(It.IsAny<List<Snippet>>(), It.IsAny<string>()))
            .Returns(true);
        return mock;
    }

    private RagService CreateRagService(IConfiguration? configuration = null)
    {
        var mockHybridSearch = new Mock<IHybridSearchService>();
        mockHybridSearch.Setup(x => x.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<SearchMode>(),
            It.IsAny<int>(),
            It.IsAny<float>(),
            It.IsAny<float>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        return new RagService(
            _dbContext,
            _mockEmbedding.Object,
            _mockQdrant.Object,
            mockHybridSearch.Object,
            _mockLlm.Object,
            _mockCache.Object,
            _mockPromptTemplate.Object,
            _mockLogger.Object,
            CreateQueryExpansionMock().Object,
            CreateRerankerMock().Object,
            CreateCitationExtractorMock().Object,
            configurationService: null, // No DB config in simple tests
            configuration: configuration
        );
    }

    private void SetupBasicMocks()
    {
        // Setup embedding service
        _mockEmbedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingResult
            {
                Success = true,
                Embeddings = new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }
            });

        // Setup Qdrant service (accept any limit)
        _mockQdrant.Setup(q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new SearchResultItem { Text = "Context", PdfId = "pdf", Page = 1, ChunkIndex = 0, Score = 0.95f }
            }));

        // Setup LLM service
        _mockLlm.Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCompletionResult
            {
                Success = true,
                Response = "Test answer",
                Usage = new LlmUsage(10, 20, 30)
            });

        // Setup cache (miss)
        _mockCache.Setup(c => c.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);

        _mockCache.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<QaResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Setup prompt template
        _mockPromptTemplate.Setup(p => p.GetTemplateAsync(It.IsAny<Guid?>(), It.IsAny<QuestionType>()))
            .ReturnsAsync(new PromptTemplate { SystemPrompt = "System", UserPromptTemplate = "User: {query}" });

        _mockPromptTemplate.Setup(p => p.RenderSystemPrompt(It.IsAny<PromptTemplate>()))
            .Returns("System prompt");

        _mockPromptTemplate.Setup(p => p.RenderUserPrompt(It.IsAny<PromptTemplate>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns("User prompt");

        _mockPromptTemplate.Setup(p => p.ClassifyQuestion(It.IsAny<string>()))
            .Returns(QuestionType.Gameplay);
    }

    #endregion
}
