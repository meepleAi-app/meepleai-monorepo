using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Services.Rag;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

/// <summary>
/// AI-04: BDD-style integration tests for snippet pipeline and fallback behavior
///
/// These tests verify the complete Q&A snippet pipeline including:
/// - RAG pipeline integration (embedding → search → LLM generation)
/// - Anti-hallucination behavior ("Not specified" fallback)
/// - Snippet extraction and formatting
/// - Token tracking
/// - Confidence scoring
/// </summary>
public class SnippetPipelineIntegrationTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<ILogger<RagService>> _mockLogger = new();
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

    private readonly SqliteConnection _connection;

    public SnippetPipelineIntegrationTests(ITestOutputHelper output)
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

    #region Scenario: End-to-End Q&A Flow with Valid Context

    [Fact]
    public async Task Scenario_GivenValidQuestion_WhenContextExists_ThenReturnsAnswerWithSnippets()
    {
        // Given: A board game Q&A system with relevant rulebook context
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Text = "Monopoly is a multiplayer economics-themed board game for 2-8 players.", PdfId = "pdf-mono-rules", Page = 1, Score = 0.94f },
                new() { Text = "Each player begins with $1,500.", PdfId = "pdf-mono-rules", Page = 3, Score = 0.89f },
                new() { Text = "The game typically lasts 60-180 minutes.", PdfId = "pdf-mono-rules", Page = 2, Score = 0.85f }
            }));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateSuccess(
                "Monopoly supports 2-8 players (see page 1).",
                new LlmUsage(PromptTokens: 312, CompletionTokens: 18, TotalTokens: 330),
                new Dictionary<string, string>
                {
                    { "model", "anthropic/claude-3-5-sonnet-20241022" },
                    { "finish_reason", "end_turn" }
                }));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: A user asks "How many players can play Monopoly?"
        var result = await ragService.AskAsync("monopoly", "How many players can play Monopoly?");

        // Then: System returns a grounded answer with citations
        result.Should().NotBeNull();
        result.answer.Should().NotBe("Not specified");
        result.answer.Should().Contain("2-8 players");

        // And: Snippets are provided with proper structure
        result.snippets.Count.Should().Be(3);
        result.snippets[0].text.Should().Be("Monopoly is a multiplayer economics-themed board game for 2-8 players.");
        result.snippets[0].source.Should().Be("PDF:pdf-mono-rules");
        result.snippets[0].page.Should().Be(1);

        // And: Token usage is accurately tracked
        result.promptTokens.Should().Be(312);
        result.completionTokens.Should().Be(18);
        result.totalTokens.Should().Be(330);

        // And: Confidence score reflects search quality
        result.confidence.Should().NotBeNull();
        result.confidence.Value.Should().BeApproximately(0.94, 0.01);

        // And: Metadata is preserved
        result.metadata.Should().NotBeNull();
        result.metadata["model"].Should().Be("anthropic/claude-3-5-sonnet-20241022");
        result.metadata["finish_reason"].Should().Be("end_turn");
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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>())); // Empty results

        var mockHybridSearch = CreateHybridSearchMock();
        mockHybridSearch
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<Guid>(),
                It.IsAny<SearchMode>(),
                It.IsAny<int>(),
                It.IsAny<float>(),
                It.IsAny<float>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>()); // Empty results

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockHybridSearch.Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: A user asks about game mechanics
        var result = await ragService.AskAsync("chess", "Can pawns move backward?");

        // Then: System returns "Not specified" instead of hallucinating
        result.answer.Should().BeEquivalentTo("Not specified");
        result.snippets.Should().BeEmpty();

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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        // TEST-656: Mock Qdrant to return empty results (no relevant context found)
        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>())); // Empty due to low relevance

        var mockHybridSearch = CreateHybridSearchMock();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, mockHybridSearch.Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: User asks a question not covered by the retrieved context
        var result = await ragService.AskAsync("catan", "What is the maximum score to win?");

        // Then: Service returns "Not specified" when no relevant context is found
        result.answer.Should().Be("Not specified");

        // And: No snippets are provided when context is insufficient
        result.snippets.Should().BeEmpty();
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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        // Qdrant mock for vector search
        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: User asks about combat mechanics
        var result = await ragService.AskAsync("dnd", "How do I make an attack roll?");

        // Then: Service returns a valid answer
        result.Should().NotBeNull();
        result.answer.Should().NotBeEmpty();

        // And: Snippets are provided from search results
        result.snippets.Should().NotBeEmpty();
        result.snippets.Count.Should().BeLessThanOrEqualTo(3);
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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("OpenRouter API timeout"));

        var mockQdrant = new Mock<IQdrantService>();
        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: User makes a Q&A request
        var result = await ragService.AskAsync("risk", "How many armies do I start with?");

        // Then: System returns a user-friendly error message
        result.answer.Should().BeEquivalentTo("Unable to process query.");
        result.snippets.Should().BeEmpty();
    }

    [Fact]
    public async Task Scenario_GivenQuestion_WhenVectorSearchFails_ThenReturnsNotSpecified()
    {
        // Given: Vector database is unavailable
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Qdrant connection timeout"));

        var mockLlm = new Mock<ILlmService>();
        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: User makes a Q&A request
        var result = await ragService.AskAsync("clue", "Who can I accuse?");

        // Then: System returns "Not specified" to avoid unreliable answers
        result.answer.Should().BeEquivalentTo("Not specified");
        result.snippets.Should().BeEmpty();
    }

    [Fact]
    public async Task Scenario_GivenQuestion_WhenLlmFails_ThenReturnsErrorWithSnippets()
    {
        // Given: Context is found but LLM service fails
        await using var dbContext = CreateInMemoryContext();

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateSuccess(new List<SearchResultItem>
            {
                new() { Text = "Relevant rule text.", PdfId = "pdf-1", Page = 5, Score = 0.95f }
            }));

        var mockLlm = new Mock<ILlmService>();
        mockLlm
            .Setup(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmCompletionResult.CreateFailure("Rate limit exceeded"));

        var mockCache = CreateCacheMock();
        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: User makes a Q&A request
        var result = await ragService.AskAsync("scrabble", "How do I score?");

        // Then: Error message is returned
        result.answer.Should().BeEquivalentTo("Unable to generate answer.");

        // And: Snippets are still provided (user can read context directly)
        result.snippets.Should().ContainSingle();
        result.snippets[0].text.Should().Be("Relevant rule text.");
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
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateSuccess(new List<float[]> { new float[] { 0.1f, 0.2f, 0.3f } }));

        var mockQdrant = new Mock<IQdrantService>();
        mockQdrant
            .Setup(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
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
            new List<Snippet> { new("Cached text", "PDF:cached", 1, 0, 0.85f) },
            100, 10, 110, 0.95);

        var mockCache = new Mock<IAiResponseCacheService>();
        mockCache.Setup(x => x.GenerateQaCacheKey("game1", "test query"))
            .Returns("qa::game1::test");
        mockCache.Setup(x => x.GetAsync<QaResponse>(It.Is<string>(s => s.Contains("qa::game1::test")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(cachedResponse);
        mockCache.Setup(x => x.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockCache.Setup(x => x.InvalidateEndpointAsync(It.IsAny<string>(), It.IsAny<AiCacheEndpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var ragService = new RagService(dbContext, mockEmbedding.Object, mockQdrant.Object, CreateHybridSearchMock().Object, mockLlm.Object, mockCache.Object, CreatePromptTemplateMock().Object, _mockLogger.Object, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        // When: Same question is asked
        var result = await ragService.AskAsync("game1", "test query");

        // Then: Cached response is returned
        result.answer.Should().BeEquivalentTo("Cached answer");
        result.snippets[0].text.Should().Be("Cached text");

        // And: No expensive operations were performed
        mockEmbedding.Verify(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        mockQdrant.Verify(x => x.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        mockLlm.Verify(x => x.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    #endregion
}