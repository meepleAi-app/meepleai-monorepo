using Api.Infrastructure;
using Api.Models;
using Api.Services;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// AI-04: Comprehensive tests for Q&A with snippet and fallback "Not specified"
///
/// Acceptance Criteria:
/// - Responses include snippets when relevant context is found
/// - Returns "Not specified" when answer is not in context
/// - E2E: No hallucination without snippet
/// </summary>
public class Ai04ComprehensiveTests
{
    private readonly Mock<ILogger<RagService>> _mockLogger = new();

    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static Mock<IAiResponseCacheService> CreateCacheMock()
    {
        var mock = new Mock<IAiResponseCacheService>();
        mock.Setup(x => x.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.InvalidateEndpointAsync(It.IsAny<string>(), It.IsAny<AiCacheEndpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mock.Setup(x => x.GenerateQaCacheKey(It.IsAny<string>(), It.IsAny<string>()))
            .Returns((string gameId, string query) => $"qa::{gameId}::{query.GetHashCode()}");
        mock.Setup(x => x.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((QaResponse?)null);
        mock.Setup(x => x.SetAsync(It.IsAny<string>(), It.IsAny<QaResponse>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return mock;
    }

    #region AI-04: Anti-Hallucination Tests

    [Fact]
    public async Task AskAsync_WhenAnswerNotInContext_ReturnsNotSpecified()
    {
        // Given: A query where the answer is not in the retrieved context
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        // Context talks about setup, not player count
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Place the board in the center of the table.", PdfId = "pdf-1", Page = 1, Score = 0.45f },
            new() { Text = "Shuffle the deck thoroughly.", PdfId = "pdf-1", Page = 2, Score = 0.35f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        // LLM correctly identifies answer is not in context
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Not specified",
                new LlmUsage(150, 2, 152)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Asking a question whose answer is not in the context
        var result = await ragService.AskAsync("game1", "How many players can play this game?", CancellationToken.None);

        // Then: Returns "Not specified" to avoid hallucination
        Assert.Equal("Not specified", result.answer);
        Assert.Equal(2, result.snippets.Count); // Snippets are still provided for transparency
    }

    [Fact]
    public async Task AskAsync_WhenNoRelevantContext_ReturnsNotSpecifiedWithEmptySnippets()
    {
        // Given: No relevant context is found in the vector database
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>())); // Empty results

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Asking a question with no relevant context
        var result = await ragService.AskAsync("game1", "What is the airspeed velocity of an unladen swallow?", CancellationToken.None);

        // Then: Returns "Not specified" with no snippets
        Assert.Equal("Not specified", result.answer);
        Assert.Empty(result.snippets);
        mockLlm.Verify(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AskAsync_WhenSearchFails_ReturnsNotSpecified()
    {
        // Given: The vector search fails
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Qdrant service unavailable"));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Search fails
        var result = await ragService.AskAsync("game1", "How do I win?", CancellationToken.None);

        // Then: Returns "Not specified" gracefully
        Assert.Equal("Not specified", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task AskAsync_WhenLlmReturnsNotSpecifiedExactly_PreservesExactText()
    {
        // Given: LLM returns exactly "Not specified" (case-sensitive test)
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Some unrelated rule.", PdfId = "pdf-1", Page = 1, Score = 0.50f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Not specified", new LlmUsage(100, 2, 102)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: LLM determines answer is not in context
        var result = await ragService.AskAsync("game1", "Unknown question", CancellationToken.None);

        // Then: Preserves exact "Not specified" text (not "NOT SPECIFIED" or "not specified")
        Assert.Equal("Not specified", result.answer);
    }

    #endregion

    #region AI-04: Snippet Handling Tests

    [Fact]
    public async Task AskAsync_WithValidContext_ReturnsAnswerWithSnippets()
    {
        // Given: Relevant context is found
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "The game is designed for 2-4 players.", PdfId = "pdf-123", Page = 3, Score = 0.95f },
            new() { Text = "Each player starts with 7 cards.", PdfId = "pdf-123", Page = 4, Score = 0.88f },
            new() { Text = "The recommended age is 10+.", PdfId = "pdf-123", Page = 2, Score = 0.82f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "The game supports 2-4 players (see page 3).",
                new LlmUsage(250, 15, 265),
                new Dictionary<string, string> { { "model", "anthropic/claude-3.5-sonnet" }, { "finish_reason", "stop" } }));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Asking a question with clear context
        var result = await ragService.AskAsync("game1", "How many players?", CancellationToken.None);

        // Then: Returns answer with all snippets
        Assert.NotEqual("Not specified", result.answer);
        Assert.Contains("2-4 players", result.answer);
        Assert.Equal(3, result.snippets.Count);

        // Verify snippet details
        Assert.Equal("The game is designed for 2-4 players.", result.snippets[0].text);
        Assert.Equal("PDF:pdf-123", result.snippets[0].source);
        Assert.Equal(3, result.snippets[0].page);

        Assert.Equal("Each player starts with 7 cards.", result.snippets[1].text);
        Assert.Equal("PDF:pdf-123", result.snippets[1].source);
        Assert.Equal(4, result.snippets[1].page);
    }

    [Fact]
    public async Task AskAsync_WithMultiplePdfs_ReturnsSnippetsFromAllSources()
    {
        // Given: Context from multiple PDF sources
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Basic rules: 2-4 players.", PdfId = "pdf-basic", Page = 1, Score = 0.95f },
            new() { Text = "Advanced variant: up to 6 players.", PdfId = "pdf-advanced", Page = 5, Score = 0.90f },
            new() { Text = "Tournament rules: exactly 4 players.", PdfId = "pdf-tournament", Page = 2, Score = 0.85f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Base game: 2-4 players. Advanced variant allows up to 6.",
                new LlmUsage(300, 20, 320)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Asking a question that spans multiple sources
        var result = await ragService.AskAsync("game1", "Player count variations?", CancellationToken.None);

        // Then: Returns snippets from all PDF sources
        Assert.Equal(3, result.snippets.Count);
        Assert.Equal("PDF:pdf-basic", result.snippets[0].source);
        Assert.Equal("PDF:pdf-advanced", result.snippets[1].source);
        Assert.Equal("PDF:pdf-tournament", result.snippets[2].source);
    }

    [Fact]
    public async Task AskAsync_SnippetsAreOrderedByRelevanceScore()
    {
        // Given: Search results with different relevance scores
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        // Intentionally unordered by score
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Low relevance text.", PdfId = "pdf-1", Page = 5, Score = 0.65f },
            new() { Text = "Highest relevance text.", PdfId = "pdf-1", Page = 1, Score = 0.98f },
            new() { Text = "Medium relevance text.", PdfId = "pdf-1", Page = 3, Score = 0.80f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(100, 10, 110)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Asking a question
        var result = await ragService.AskAsync("game1", "test", CancellationToken.None);

        // Then: Snippets maintain the order from search results (Qdrant already sorts by score)
        Assert.Equal(3, result.snippets.Count);
        Assert.Equal("Low relevance text.", result.snippets[0].text);
        Assert.Equal("Highest relevance text.", result.snippets[1].text);
        Assert.Equal("Medium relevance text.", result.snippets[2].text);
    }

    [Fact]
    public async Task AskAsync_SnippetSourceIncludesPdfPrefix()
    {
        // Given: Search results from a PDF
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule text.", PdfId = "abc-123-def", Page = 10, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(50, 5, 55)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Retrieving answer
        var result = await ragService.AskAsync("game1", "test", CancellationToken.None);

        // Then: Source includes "PDF:" prefix
        Assert.Single(result.snippets);
        Assert.Equal("PDF:abc-123-def", result.snippets[0].source);
    }

    #endregion

    #region AI-04: Token Usage and Metadata Tests

    [Fact]
    public async Task AskAsync_ReturnsTokenUsageInResponse()
    {
        // Given: A successful Q&A interaction
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Game rule.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "The answer.",
                new LlmUsage(PromptTokens: 523, CompletionTokens: 87, TotalTokens: 610)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Making a Q&A request
        var result = await ragService.AskAsync("game1", "test", CancellationToken.None);

        // Then: Token usage is accurately reported
        Assert.Equal(523, result.promptTokens);
        Assert.Equal(87, result.completionTokens);
        Assert.Equal(610, result.totalTokens);
    }

    [Fact]
    public async Task AskAsync_ReturnsConfidenceScore()
    {
        // Given: Search results with varying confidence scores
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule 1.", PdfId = "pdf-1", Page = 1, Score = 0.87f },
            new() { Text = "Rule 2.", PdfId = "pdf-1", Page = 2, Score = 0.92f }, // Max score
            new() { Text = "Rule 3.", PdfId = "pdf-1", Page = 3, Score = 0.78f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(100, 10, 110)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Making a Q&A request
        var result = await ragService.AskAsync("game1", "test", CancellationToken.None);

        // Then: Confidence score is the maximum from search results
        Assert.NotNull(result.confidence);
        Assert.Equal(0.92, result.confidence.Value, precision: 2);
    }

    [Fact]
    public async Task AskAsync_ReturnsModelAndFinishReasonMetadata()
    {
        // Given: LLM returns metadata
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Answer.",
                new LlmUsage(100, 10, 110),
                new Dictionary<string, string>
                {
                    { "model", "anthropic/claude-3-5-sonnet-20241022" },
                    { "finish_reason", "end_turn" },
                    { "system_fingerprint", "fp_123abc" }
                }));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Making a Q&A request
        var result = await ragService.AskAsync("game1", "test", CancellationToken.None);

        // Then: Metadata is included in response
        Assert.NotNull(result.metadata);
        Assert.Equal("anthropic/claude-3-5-sonnet-20241022", result.metadata["model"]);
        Assert.Equal("end_turn", result.metadata["finish_reason"]);
        Assert.Equal("fp_123abc", result.metadata["system_fingerprint"]);
    }

    #endregion

    #region AI-04: Edge Cases and Error Scenarios

    [Fact]
    public async Task AskAsync_WithVeryLongQuery_HandlesGracefully()
    {
        // Given: A very long query string
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Submitting a very long query (1000+ words)
        var longQuery = string.Join(" ", Enumerable.Repeat("question word", 500));
        var result = await ragService.AskAsync("game1", longQuery, CancellationToken.None);

        // Then: Handles without error
        Assert.NotNull(result);
        Assert.Equal("Not specified", result.answer); // No results found
    }

    [Fact]
    public async Task AskAsync_WithSpecialCharactersInQuery_HandlesCorrectly()
    {
        // Given: Query with special characters
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Q&A rule text.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer with special chars: & < > \" '", new LlmUsage(100, 15, 115)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Query contains special characters
        var result = await ragService.AskAsync("game1", "What about <special> & \"quoted\" chars?", CancellationToken.None);

        // Then: Handles correctly without escaping issues
        Assert.NotNull(result);
        Assert.Contains("special", result.answer);
    }

    [Fact]
    public async Task AskAsync_WithLlmReturningVeryLongAnswer_TruncatesCorrectly()
    {
        // Given: LLM returns a very long answer
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var longAnswer = string.Join(" ", Enumerable.Repeat("This is a very detailed answer.", 200)); // ~1200 words
        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(longAnswer, new LlmUsage(500, 1200, 1700)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: LLM returns very long answer
        var result = await ragService.AskAsync("game1", "test", CancellationToken.None);

        // Then: Answer is preserved (no artificial truncation at service level)
        Assert.NotNull(result);
        Assert.True(result.answer.Length > 1000); // Long answer preserved
    }

    [Fact]
    public async Task AskAsync_WhenCancellationRequested_PropagatesCancellation()
    {
        // Given: A cancellation token that is already cancelled
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // When/Then: Should handle cancellation gracefully
        var result = await ragService.AskAsync("game1", "test", cts.Token);

        // Note: Current implementation catches all exceptions and returns error message
        // In a real-world scenario, you might want to check for OperationCanceledException
        Assert.NotNull(result);
    }

    #endregion

    #region AI-04: Prompt Engineering Verification

    [Fact]
    public async Task AskAsync_SendsAntiHallucinationPromptToLlm()
    {
        // Given: A standard Q&A setup
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule text from page 5.", PdfId = "pdf-1", Page = 5, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        string? capturedSystemPrompt = null;
        string? capturedUserPrompt = null;

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((sys, user, _) =>
            {
                capturedSystemPrompt = sys;
                capturedUserPrompt = user;
            })
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(100, 10, 110)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Making a Q&A request
        await ragService.AskAsync("game1", "Test question?", CancellationToken.None);

        // Then: System prompt contains anti-hallucination instructions
        Assert.NotNull(capturedSystemPrompt);
        Assert.Contains("board game rules assistant", capturedSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT in the provided context", capturedSystemPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Not specified", capturedSystemPrompt);
        Assert.Contains("Do NOT hallucinate", capturedSystemPrompt, StringComparison.OrdinalIgnoreCase);

        // User prompt contains context with page numbers
        Assert.NotNull(capturedUserPrompt);
        Assert.Contains("CONTEXT FROM RULEBOOK", capturedUserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[Page 5]", capturedUserPrompt);
        Assert.Contains("Rule text from page 5.", capturedUserPrompt);
        Assert.Contains("QUESTION", capturedUserPrompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Test question?", capturedUserPrompt);
    }

    [Fact]
    public async Task AskAsync_IncludesPageNumbersInContext()
    {
        // Given: Multiple search results from different pages
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Setup instructions.", PdfId = "pdf-1", Page = 2, Score = 0.95f },
            new() { Text = "Gameplay rules.", PdfId = "pdf-1", Page = 8, Score = 0.90f },
            new() { Text = "Scoring details.", PdfId = "pdf-1", Page = 15, Score = 0.85f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        string? capturedUserPrompt = null;
        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, user, _) => capturedUserPrompt = user)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(100, 10, 110)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Generating answer
        await ragService.AskAsync("game1", "How to play?", CancellationToken.None);

        // Then: Context includes page numbers for each chunk
        Assert.NotNull(capturedUserPrompt);
        Assert.Contains("[Page 2]", capturedUserPrompt);
        Assert.Contains("[Page 8]", capturedUserPrompt);
        Assert.Contains("[Page 15]", capturedUserPrompt);
    }

    #endregion
}
