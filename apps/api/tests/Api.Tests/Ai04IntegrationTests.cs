using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

/// <summary>
/// AI-04: BDD-style integration tests for Q&A with snippet and fallback behavior
///
/// These tests verify the complete Q&A flow including:
/// - RAG pipeline integration (embedding → search → LLM generation)
/// - Anti-hallucination behavior
/// - Snippet extraction and formatting
/// - Token tracking
/// - Confidence scoring
/// </summary>
public class Ai04IntegrationTests
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

    #region Scenario: End-to-End Q&A Flow with Valid Context

    [Fact]
    public async Task Scenario_GivenValidQuestion_WhenContextExists_ThenReturnsAnswerWithSnippets()
    {
        // Given: A board game Q&A system with relevant rulebook context
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync("monopoly", It.IsAny<float[]>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Text = "Monopoly is a multiplayer economics-themed board game for 2-8 players.", PdfId = "pdf-mono-rules", Page = 1, Score = 0.94f },
                new() { Text = "Each player begins with $1,500.", PdfId = "pdf-mono-rules", Page = 3, Score = 0.89f },
                new() { Text = "The game typically lasts 60-180 minutes.", PdfId = "pdf-mono-rules", Page = 2, Score = 0.85f }
            }));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(
                It.Is<string>(s => s.Contains("board game rules assistant")),
                It.Is<string>(s => s.Contains("Monopoly is a multiplayer") && s.Contains("How many players")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Monopoly supports 2-8 players (see page 1).",
                new LlmUsage(PromptTokens: 312, CompletionTokens: 18, TotalTokens: 330),
                new Dictionary<string, string>
                {
                    { "model", "anthropic/claude-3-5-sonnet-20241022" },
                    { "finish_reason", "end_turn" }
                }));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: A user asks "How many players can play Monopoly?"
        var result = await ragService.AskAsync("monopoly", "How many players can play Monopoly?", CancellationToken.None);

        // Then: System returns a grounded answer with citations
        Assert.NotNull(result);
        Assert.NotEqual("Not specified", result.answer);
        Assert.Contains("2-8 players", result.answer);

        // And: Snippets are provided with proper structure
        Assert.Equal(3, result.snippets.Count);
        Assert.Equal("Monopoly is a multiplayer economics-themed board game for 2-8 players.", result.snippets[0].text);
        Assert.Equal("PDF:pdf-mono-rules", result.snippets[0].source);
        Assert.Equal(1, result.snippets[0].page);

        // And: Token usage is accurately tracked
        Assert.Equal(312, result.promptTokens);
        Assert.Equal(18, result.completionTokens);
        Assert.Equal(330, result.totalTokens);

        // And: Confidence score reflects search quality
        Assert.NotNull(result.confidence);
        Assert.Equal(0.94, result.confidence.Value, precision: 2);

        // And: Metadata is preserved
        Assert.NotNull(result.metadata);
        Assert.Equal("anthropic/claude-3-5-sonnet-20241022", result.metadata["model"]);
        Assert.Equal("end_turn", result.metadata["finish_reason"]);
    }

    #endregion

    #region Scenario: Anti-Hallucination - No Context Available

    [Fact]
    public async Task Scenario_GivenQuestion_WhenNoContextExists_ThenReturnsNotSpecified()
    {
        // Given: A Q&A system with no relevant context in the database
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync("chess", It.IsAny<float[]>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>())); // Empty results

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: A user asks about game mechanics
        var result = await ragService.AskAsync("chess", "Can pawns move backward?", CancellationToken.None);

        // Then: System returns "Not specified" instead of hallucinating
        Assert.Equal("Not specified", result.answer);
        Assert.Empty(result.snippets);

        // And: LLM is never called (no context to process)
        mockLlm.Verify(
            x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Scenario: Anti-Hallucination - Irrelevant Context

    [Fact]
    public async Task Scenario_GivenQuestion_WhenContextIsIrrelevant_ThenLlmReturnsNotSpecified()
    {
        // Given: Context exists but doesn't answer the specific question
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        // Vector search returns low-relevance matches
        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync("catan", It.IsAny<float[]>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Text = "Place the game board on a flat surface.", PdfId = "pdf-catan", Page = 2, Score = 0.42f },
                new() { Text = "Shuffle the resource cards thoroughly.", PdfId = "pdf-catan", Page = 1, Score = 0.38f }
            }));

        // LLM correctly identifies answer is not in context
        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Not specified",
                new LlmUsage(200, 2, 202)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: User asks a question not covered by the retrieved context
        var result = await ragService.AskAsync("catan", "What is the maximum score to win?", CancellationToken.None);

        // Then: LLM returns "Not specified" to avoid hallucination
        Assert.Equal("Not specified", result.answer);

        // And: Context snippets are still provided for transparency
        Assert.Equal(2, result.snippets.Count);
        Assert.Equal("Place the game board on a flat surface.", result.snippets[0].text);
    }

    #endregion

    #region Scenario: Multiple PDF Sources

    [Fact]
    public async Task Scenario_GivenQuestion_WhenMultiplePdfsHaveContext_ThenSnippetsFromAllSources()
    {
        // Given: A game with multiple rulebook PDFs
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync("dnd", It.IsAny<float[]>(), 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Text = "Basic rules: Roll d20 for attack.", PdfId = "pdf-players-handbook", Page = 45, Score = 0.93f },
                new() { Text = "Advanced combat: Add proficiency bonus.", PdfId = "pdf-dm-guide", Page = 112, Score = 0.88f },
                new() { Text = "Optional rule: Critical hits deal double damage.", PdfId = "pdf-xanathar", Page = 23, Score = 0.85f }
            }));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Roll a d20 and add your proficiency bonus for attack rolls.",
                new LlmUsage(400, 25, 425)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: User asks about combat mechanics
        var result = await ragService.AskAsync("dnd", "How do I make an attack roll?", CancellationToken.None);

        // Then: Snippets are provided from all source PDFs
        Assert.Equal(3, result.snippets.Count);
        Assert.Equal("PDF:pdf-players-handbook", result.snippets[0].source);
        Assert.Equal("PDF:pdf-dm-guide", result.snippets[1].source);
        Assert.Equal("PDF:pdf-xanathar", result.snippets[2].source);

        // And: Each snippet has correct page numbers
        Assert.Equal(45, result.snippets[0].page);
        Assert.Equal(112, result.snippets[1].page);
        Assert.Equal(23, result.snippets[2].page);
    }

    #endregion

    #region Scenario: Error Handling

    [Fact]
    public async Task Scenario_GivenQuestion_WhenEmbeddingServiceFails_ThenReturnsGracefulError()
    {
        // Given: Embedding service is unavailable
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("OpenRouter API timeout"));

        var mockQdrant = new Mock<IQdrantService>();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: User makes a Q&A request
        var result = await ragService.AskAsync("risk", "How many armies do I start with?", CancellationToken.None);

        // Then: System returns a user-friendly error message
        Assert.Equal("Unable to process query.", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task Scenario_GivenQuestion_WhenVectorSearchFails_ThenReturnsNotSpecified()
    {
        // Given: Vector database is unavailable
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Qdrant connection timeout"));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: User makes a Q&A request
        var result = await ragService.AskAsync("clue", "Who can I accuse?", CancellationToken.None);

        // Then: System returns "Not specified" to avoid unreliable answers
        Assert.Equal("Not specified", result.answer);
        Assert.Empty(result.snippets);
    }

    [Fact]
    public async Task Scenario_GivenQuestion_WhenLlmFails_ThenReturnsErrorWithSnippets()
    {
        // Given: Context is found but LLM service fails
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Text = "Relevant rule text.", PdfId = "pdf-1", Page = 5, Score = 0.95f }
            }));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Rate limit exceeded"));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: User makes a Q&A request
        var result = await ragService.AskAsync("scrabble", "How do I score?", CancellationToken.None);

        // Then: Error message is returned
        Assert.Equal("Unable to generate answer.", result.answer);

        // And: Snippets are still provided (user can read context directly)
        Assert.Single(result.snippets);
        Assert.Equal("Relevant rule text.", result.snippets[0].text);
    }

    #endregion

    #region Scenario: Caching Behavior

    [Fact]
    public async Task Scenario_GivenSameQuestion_WhenAskedTwice_ThenSecondCallReturnsCachedResponse()
    {
        // Given: A Q&A system with caching enabled
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Text = "Rule text.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
            }));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(100, 10, 110)));

        var cachedResponse = new QaResponse(
            "Cached answer",
            new List<Snippet> { new("Cached text", "PDF:cached", 1, 0) },
            100, 10, 110, 0.95);

        var mockCache = new Mock<IAiResponseCacheService>();
        mockCache.Setup(x => x.GenerateQaCacheKey("game1", "test query"))
            .Returns("qa::game1::test");
        mockCache.Setup(x => x.GetAsync<QaResponse>("qa::game1::test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);
        mockCache.Setup(x => x.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockCache.Setup(x => x.InvalidateEndpointAsync(It.IsAny<string>(), It.IsAny<AiCacheEndpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockLlm.Object, mockCache.Object, _mockLogger.Object);

        // When: Same question is asked
        var result = await ragService.AskAsync("game1", "test query", CancellationToken.None);

        // Then: Cached response is returned
        Assert.Equal("Cached answer", result.answer);
        Assert.Equal("Cached text", result.snippets[0].text);

        // And: No expensive operations were performed
        mockEmbedding.Verify(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mockQdrant.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        mockLlm.Verify(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}
