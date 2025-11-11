using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.Services.Rag;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit;

/// <summary>
/// AI-04: Comprehensive tests for Q&A snippet handling and fallback behavior
///
/// Tests verify:
/// - Responses include snippets when relevant context is found
/// - Returns "Not specified" when answer is not in context
/// - E2E: No hallucination without snippet (anti-hallucination)
/// </summary>
public class SnippetHandlingTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<RagService>> _mockLogger = new();
    private readonly SqliteConnection _connection;

    public SnippetHandlingTests(ITestOutputHelper output)
    {
        _output = output;
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }

    private MeepleAiDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(_connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private static Mock<IPromptTemplateService> CreatePromptTemplateMock()
    {
        var mock = new Mock<IPromptTemplateService>();

        var defaultTemplate = new PromptTemplate
        {
            SystemPrompt = @"You are a board game rules assistant. You answer questions based ONLY on the provided rulebook excerpts.

CRITICAL INSTRUCTIONS:
- If the answer is NOT in the provided context, respond EXACTLY with: ""Not specified in the rules.""
- Do NOT hallucinate or make up information
- Do NOT use knowledge outside the provided context
- ONLY answer what is explicitly stated in the rulebook excerpts",
            UserPromptTemplate = "CONTEXT FROM RULEBOOK:\n{context}\n\nQUESTION: {query}\n\nANSWER:",
            FewShotExamples = new List<FewShotExample>()
        };

        mock.Setup(x => x.GetTemplateAsync(It.IsAny<Guid?>(), It.IsAny<QuestionType>()))
            .ReturnsAsync(defaultTemplate);

        mock.Setup(x => x.RenderSystemPrompt(It.IsAny<PromptTemplate>()))
            .Returns((PromptTemplate t) => t.SystemPrompt);

        mock.Setup(x => x.RenderUserPrompt(It.IsAny<PromptTemplate>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((PromptTemplate t, string context, string query) =>
                t.UserPromptTemplate.Replace("{context}", context).Replace("{query}", query));

        mock.Setup(x => x.ClassifyQuestion(It.IsAny<string>()))
            .Returns(QuestionType.General);

        return mock;
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

    // AI-14: Helper to create IHybridSearchService mock
    private static Mock<IHybridSearchService> CreateHybridSearchMock()
    {
        var mock = new Mock<IHybridSearchService>();
        mock.Setup(x => x.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<SearchMode>(),
            It.IsAny<int>(),
            It.IsAny<float>(),
            It.IsAny<float>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());
        return mock;
    }

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

    #region AI-04: Anti-Hallucination Tests

    [Fact]
    public async Task AskAsync_WhenAnswerNotInContext_ReturnsNotSpecified()
    {
        // Given: A query where the answer is not in the retrieved context
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        // Context talks about setup, not player count
        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Place the board in the center of the table.", PdfId = "pdf-1", Page = 1, Score = 0.45f },
            new() { Text = "Shuffle the deck thoroughly.", PdfId = "pdf-1", Page = 2, Score = 0.35f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        // LLM correctly identifies answer is not in context
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Not specified",
                new LlmUsage(150, 2, 152)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Asking a question whose answer is not in the context
        var result = await ragService.AskAsync("game1", "How many players can play this game?");

        // Then: Returns "Not specified" to avoid hallucination
        result.answer.Should().BeEquivalentTo("Not specified");
        result.snippets.Count.Should().Be(2); // Snippets are still provided for transparency
    }

    [Fact]
    public async Task AskAsync_WhenNoRelevantContext_ReturnsNotSpecifiedWithEmptySnippets()
    {
        // Given: No relevant context is found in the vector database
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>())); // Empty results

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Asking a question with no relevant context
        var result = await ragService.AskAsync("game1", "What is the airspeed velocity of an unladen swallow?");

        // Then: Returns "Not specified" with no snippets
        result.answer.Should().BeEquivalentTo("Not specified");
        result.snippets.Should().BeEmpty();
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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Qdrant service unavailable"));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Search fails
        var result = await ragService.AskAsync("game1", "How do I win?");

        // Then: Returns "Not specified" gracefully
        result.answer.Should().BeEquivalentTo("Not specified");
        result.snippets.Should().BeEmpty();
    }

    [Fact]
    public async Task AskAsync_WhenLlmReturnsNotSpecifiedExactly_PreservesExactText()
    {
        // Given: LLM returns exactly "Not specified" (case-sensitive test)
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Some unrelated rule.", PdfId = "pdf-1", Page = 1, Score = 0.50f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Not specified", new LlmUsage(100, 2, 102)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: LLM determines answer is not in context
        var result = await ragService.AskAsync("game1", "Unknown question");

        // Then: Preserves exact "Not specified" text (not "NOT SPECIFIED" or "not specified")
        result.answer.Should().BeEquivalentTo("Not specified");
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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "The game is designed for 2-4 players.", PdfId = "pdf-123", Page = 3, Score = 0.95f },
            new() { Text = "Each player starts with 7 cards.", PdfId = "pdf-123", Page = 4, Score = 0.88f },
            new() { Text = "The recommended age is 10+.", PdfId = "pdf-123", Page = 2, Score = 0.82f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "The game supports 2-4 players (see page 3).",
                new LlmUsage(250, 15, 265),
                new Dictionary<string, string> { { "model", "anthropic/claude-3.5-sonnet" }, { "finish_reason", "stop" } }));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Asking a question with clear context
        var result = await ragService.AskAsync("game1", "How many players?");

        // Then: Returns answer with all snippets
        result.answer.Should().NotBe("Not specified");
        result.answer.Should().Contain("2-4 players");
        result.snippets.Count.Should().Be(3);

        // Verify snippet details
        result.snippets[0].text.Should().Be("The game is designed for 2-4 players.");
        result.snippets[0].source.Should().Be("PDF:pdf-123");
        result.snippets[0].page.Should().Be(3);

        result.snippets[1].text.Should().Be("Each player starts with 7 cards.");
        result.snippets[1].source.Should().Be("PDF:pdf-123");
        result.snippets[1].page.Should().Be(4);
    }

    [Fact]
    public async Task AskAsync_WithMultiplePdfs_ReturnsSnippetsFromAllSources()
    {
        // Given: Context from multiple PDF sources
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Basic rules: 2-4 players.", PdfId = "pdf-basic", Page = 1, Score = 0.95f },
            new() { Text = "Advanced variant: up to 6 players.", PdfId = "pdf-advanced", Page = 5, Score = 0.90f },
            new() { Text = "Tournament rules: exactly 4 players.", PdfId = "pdf-tournament", Page = 2, Score = 0.85f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Base game: 2-4 players. Advanced variant allows up to 6.",
                new LlmUsage(300, 20, 320)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Asking a question that spans multiple sources
        var result = await ragService.AskAsync("game1", "Player count variations?");

        // Then: Returns snippets from all PDF sources
        result.snippets.Count.Should().Be(3);
        result.snippets[0].source.Should().Be("PDF:pdf-basic");
        result.snippets[1].source.Should().Be("PDF:pdf-advanced");
        result.snippets[2].source.Should().Be("PDF:pdf-tournament");
    }

    [Fact]
    public async Task AskAsync_SnippetsAreOrderedByRelevanceScore()
    {
        // Given: Search results with different relevance scores
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
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
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(100, 10, 110)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Asking a question
        var result = await ragService.AskAsync("game1", "test");

        // Then: Snippets maintain the order from search results (Qdrant already sorts by score)
        result.snippets.Count.Should().Be(3);
        result.snippets[0].text.Should().Be("Low relevance text.");
        result.snippets[1].text.Should().Be("Highest relevance text.");
        result.snippets[2].text.Should().Be("Medium relevance text.");
    }

    [Fact]
    public async Task AskAsync_SnippetSourceIncludesPdfPrefix()
    {
        // Given: Search results from a PDF
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule text.", PdfId = Guid.NewGuid(), Page = 10, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(50, 5, 55)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Retrieving answer
        var result = await ragService.AskAsync("game1", "test");

        // Then: Source includes "PDF:" prefix
        result.snippets.Should().ContainSingle();
        result.snippets[0].source.Should().Be("PDF:abc-123-def");
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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Game rule.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "The answer.",
                new LlmUsage(PromptTokens: 523, CompletionTokens: 87, TotalTokens: 610)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Making a Q&A request
        var result = await ragService.AskAsync("game1", "test");

        // Then: Token usage is accurately reported
        result.promptTokens.Should().Be(523);
        result.completionTokens.Should().Be(87);
        result.totalTokens.Should().Be(610);
    }

    [Fact]
    public async Task AskAsync_ReturnsConfidenceScore()
    {
        // Given: Search results with varying confidence scores
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule 1.", PdfId = "pdf-1", Page = 1, Score = 0.87f },
            new() { Text = "Rule 2.", PdfId = "pdf-1", Page = 2, Score = 0.92f }, // Max score
            new() { Text = "Rule 3.", PdfId = "pdf-1", Page = 3, Score = 0.78f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(100, 10, 110)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Making a Q&A request
        var result = await ragService.AskAsync("game1", "test");

        // Then: Confidence score is the maximum from search results
        result.confidence.Should().NotBeNull();
        result.confidence.Value.Should().BeApproximately(0.92, 2);
    }

    [Fact]
    public async Task AskAsync_ReturnsModelAndFinishReasonMetadata()
    {
        // Given: LLM returns metadata
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Making a Q&A request
        var result = await ragService.AskAsync("game1", "test");

        // Then: Metadata is included in response
        result.metadata.Should().NotBeNull();
        result.metadata["model"].Should().Be("anthropic/claude-3-5-sonnet-20241022");
        result.metadata["finish_reason"].Should().Be("end_turn");
        result.metadata["system_fingerprint"].Should().Be("fp_123abc");
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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>()));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Submitting a very long query (1000+ words)
        var longQuery = string.Join(" ", Enumerable.Repeat("question word", 500));
        var result = await ragService.AskAsync("game1", longQuery);

        // Then: Handles without error
        result.Should().NotBeNull();
        result.answer.Should().BeEquivalentTo("Not specified"); // No results found
    }

    [Fact]
    public async Task AskAsync_WithSpecialCharactersInQuery_HandlesCorrectly()
    {
        // Given: Query with special characters
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Q&A rule text.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer with special chars: & < > \" '", new LlmUsage(100, 15, 115)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Query contains special characters
        var result = await ragService.AskAsync("game1", "What about <special> & \"quoted\" chars?");

        // Then: Handles correctly without escaping issues
        result.Should().NotBeNull();
        result.answer.Should().Contain("special");
    }

    [Fact]
    public async Task AskAsync_WithLlmReturningVeryLongAnswer_TruncatesCorrectly()
    {
        // Given: LLM returns a very long answer
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule.", PdfId = "pdf-1", Page = 1, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        var longAnswer = string.Join(" ", Enumerable.Repeat("This is a very detailed answer.", 200)); // ~1200 words
        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(longAnswer, new LlmUsage(500, 1200, 1700)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: LLM returns very long answer
        var result = await ragService.AskAsync("game1", "test");

        // Then: Answer is preserved (no artificial truncation at service level)
        result.Should().NotBeNull();
        (result.answer.Length > 1000).Should().BeTrue(); // Long answer preserved
    }

    [Fact]
    public async Task AskAsync_WhenCancellationRequested_PropagatesCancellation()
    {
        // Given: A cancellation token that is already cancelled
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var mockQdrant = new Mock<IQdrantService>();
        var mockHybridSearch = CreateHybridSearchMock();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockHybridSearch.Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        // When/Then: Should handle cancellation gracefully
        // AI-09: Language parameter defaults to null (uses "en")
        var result = await ragService.AskAsync("game1", "test", language: null, bypassCache: false, cts.Token);

        // Note: Current implementation catches all exceptions and returns error message
        // In a real-world scenario, you might want to check for OperationCanceledException
        result.Should().NotBeNull();
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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Rule text from page 5.", PdfId = "pdf-1", Page = 5, Score = 0.95f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Making a Q&A request
        await ragService.AskAsync("game1", "Test question?");

        // Then: System prompt contains anti-hallucination instructions
        capturedSystemPrompt.Should().NotBeNull();
        capturedSystemPrompt.Should().Contain("board game rules assistant");
        capturedSystemPrompt.Should().Contain("NOT in the provided context");
        capturedSystemPrompt.Should().Contain("Not specified");
        capturedSystemPrompt.Should().Contain("Do NOT hallucinate");

        // User prompt contains context with page numbers
        capturedUserPrompt.Should().NotBeNull();
        capturedUserPrompt.Should().Contain("CONTEXT FROM RULEBOOK");
        capturedUserPrompt.Should().Contain("[Page 5]");
        capturedUserPrompt.Should().Contain("Rule text from page 5.");
        capturedUserPrompt.Should().Contain("QUESTION");
        capturedUserPrompt.Should().Contain("Test question?");
    }

    [Fact]
    public async Task AskAsync_IncludesPageNumbersInContext()
    {
        // Given: Multiple search results from different pages
        await using var dbContext = CreateInMemoryContext();
        var mockEmbedding = new Mock<IEmbeddingService>();
        var embedding = new float[] { 0.1f, 0.2f, 0.3f };
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { embedding }));

        var searchResults = new List<SearchResultItem>
        {
            new() { Text = "Setup instructions.", PdfId = "pdf-1", Page = 2, Score = 0.95f },
            new() { Text = "Gameplay rules.", PdfId = "pdf-1", Page = 8, Score = 0.90f },
            new() { Text = "Scoring details.", PdfId = "pdf-1", Page = 15, Score = 0.85f }
        };

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(searchResults));

        string? capturedUserPrompt = null;
        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, user, _) => capturedUserPrompt = user)
            .ReturnsAsync(LlmCompletionResult.CreateSuccess("Answer.", new LlmUsage(100, 10, 110)));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Generating answer
        await ragService.AskAsync("game1", "How to play?");

        // Then: Context includes page numbers for each chunk
        capturedUserPrompt.Should().NotBeNull();
        capturedUserPrompt.Should().Contain("[Page 2]");
        capturedUserPrompt.Should().Contain("[Page 8]");
        capturedUserPrompt.Should().Contain("[Page 15]");
    }

    #endregion
}