using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.Services.Rag;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

namespace Api.Tests.Services;

/// <summary>
/// AI-09: Multilingual tests for RagService
/// Tests language parameter propagation through RAG pipeline and cache integration
/// </summary>
public class RagServiceMultilingualTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly Mock<IQdrantService> _qdrantMock;
    private readonly Mock<IEmbeddingService> _embeddingMock;
    private readonly Mock<IHybridSearchService> _hybridSearchMock;
    private readonly Mock<ILlmService> _llmMock;
    private readonly Mock<IAiResponseCacheService> _cacheMock;
    private readonly Mock<IPromptTemplateService> _promptTemplateMock;
    private readonly MeepleAiDbContext _dbContext;
    private readonly RagService _service;

    public RagServiceMultilingualTests(ITestOutputHelper output)
    {
        _output = output;
        _qdrantMock = new Mock<IQdrantService>();
        _embeddingMock = new Mock<IEmbeddingService>();
        _hybridSearchMock = CreateHybridSearchMock();
        _llmMock = new Mock<ILlmService>();
        _cacheMock = new Mock<IAiResponseCacheService>();
        _promptTemplateMock = CreatePromptTemplateMock();

        _dbContext = CreateInMemoryContext();
        SeedTestData();

        _service = new RagService(
            _dbContext,
            _embeddingMock.Object,
            _qdrantMock.Object,
            _hybridSearchMock.Object,
            _llmMock.Object,
            _cacheMock.Object,
            _promptTemplateMock.Object,
            NullLogger<RagService>.Instance,
            CreateQueryExpansionMock().Object,
            CreateRerankerMock().Object,
            CreateCitationExtractorMock().Object
        );
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

    private static Mock<IPromptTemplateService> CreatePromptTemplateMock()
    {
        var mock = new Mock<IPromptTemplateService>();

        var defaultTemplate = new PromptTemplate
        {
            SystemPrompt = "You are a board game rules assistant.",
            UserPromptTemplate = "CONTEXT: {context}\n\nQUESTION: {query}\n\nANSWER:",
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

    public void Dispose()
    {
        _dbContext?.Dispose();
    }

    private static MeepleAiDbContext CreateInMemoryContext()
    {
        var connection = new Microsoft.Data.Sqlite.SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new MeepleAiDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    private void SeedTestData()
    {
        _dbContext.Games.Add(new GameEntity
        {
            Id = "chess",
            Name = "Chess",
            CreatedAt = DateTime.UtcNow
        });
        _dbContext.SaveChanges();
    }

    #region AskAsync - Language Propagation

    /// <summary>
    /// BDD Scenario: AskAsync with Italian language propagates to services
    /// Given: User asks question in Italian
    /// When: AskAsync is called with language "it"
    /// Then: EmbeddingService and QdrantService receive "it" parameter
    /// </summary>
    [Fact]
    public async Task AskAsync_WithItalianLanguage_PropagatesLanguageToServices()
    {
        // Arrange
        string? capturedEmbeddingLanguage = null;
        string? capturedSearchLanguage = null;

        SetupMockCache(null); // Cache miss

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, lang, _) =>
            {
                capturedEmbeddingLanguage = lang;
            })
            .ReturnsAsync(CreateSuccessfulEmbeddingResult());

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, float[], string, int, CancellationToken>((_, _, lang, _, _) =>
            {
                capturedSearchLanguage = lang;
            })
            .ReturnsAsync(CreateSuccessfulSearchResult());

        SetupMockLlm("Risposta italiana");

        // Act
        var result = await _service.AskAsync("chess", "Come si muove il cavallo?", language: "it");

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("Risposta italiana");
        capturedEmbeddingLanguage.Should().Be("it");
        capturedSearchLanguage.Should().Be("it");
    }

    /// <summary>
    /// BDD Scenario: AskAsync with different languages
    /// Given: User asks questions in different languages
    /// When: AskAsync is called with each language
    /// Then: Correct language is propagated to embedding and search
    /// </summary>
    [Theory]
    [InlineData("en", "How does the knight move?")]
    [InlineData("it", "Come si muove il cavallo?")]
    [InlineData("de", "Wie bewegt sich der Springer?")]
    [InlineData("fr", "Comment le cavalier se déplace-t-il?")]
    [InlineData("es", "¿Cómo se mueve el caballo?")]
    public async Task AskAsync_DifferentLanguages_PropagatesCorrectLanguage(string language, string query)
    {
        // Arrange
        string? capturedLanguage = null;

        SetupMockCache(null);

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, lang, _) =>
            {
                capturedLanguage = lang;
            })
            .ReturnsAsync(CreateSuccessfulEmbeddingResult());

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessfulSearchResult());

        SetupMockLlm("Answer");

        // Act
        var result = await _service.AskAsync("chess", query, language: language);

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("Answer");
        capturedLanguage.Should().Be(language);
    }

    /// <summary>
    /// BDD Scenario: AskAsync with null language defaults to English
    /// Given: Language parameter is null
    /// When: AskAsync is called
    /// Then: Services receive "en" as language
    /// </summary>
    [Fact]
    public async Task AskAsync_NullLanguage_DefaultsToEnglish()
    {
        // Arrange
        string? capturedLanguage = null;

        SetupMockCache(null);

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, lang, _) =>
            {
                capturedLanguage = lang;
            })
            .ReturnsAsync(CreateSuccessfulEmbeddingResult());

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessfulSearchResult());

        SetupMockLlm("English answer");

        // Act
        var result = await _service.AskAsync("chess", "How does it work?", language: null);

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("English answer");
        capturedLanguage.Should().Be("en");
    }

    #endregion

    #region AskAsync - Cache Integration with Language

    /// <summary>
    /// BDD Scenario: Language-specific cache keys prevent cross-language hits
    /// Given: Same query asked in different languages
    /// When: AskAsync is called for Italian then English
    /// Then: Each creates different cache keys and doesn't hit other's cache
    /// </summary>
    [Fact]
    public async Task AskAsync_DifferentLanguages_UsesDifferentCacheKeys()
    {
        // Arrange
        var capturedCacheKeys = new List<string>();


        _cacheMock
            .Setup(c => c.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedCacheKeys.Add(key))
            .ReturnsAsync((QaResponse?)null); // Cache miss

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessfulEmbeddingResult());

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessfulSearchResult());

        SetupMockLlm("Response");

        // Act
        await _service.AskAsync("chess", "knight move", language: "it");
        await _service.AskAsync("chess", "knight move", language: "en");

        // Assert
        capturedCacheKeys.Count.Should().Be(2);
        capturedCacheKeys[0].Should().Contain(":lang:it");
        capturedCacheKeys[1].Should().Contain(":lang:en");
        capturedCacheKeys[1].Should().NotBe(capturedCacheKeys[0]);
    }

    /// <summary>
    /// BDD Scenario: Cache hit returns cached response without calling services
    /// Given: Italian response is cached
    /// When: Same query asked in Italian
    /// Then: Cached response returned, no embedding/search/LLM calls
    /// </summary>
    [Fact]
    public async Task AskAsync_CacheHitWithLanguage_SkipsServicesAndReturnsCache()
    {
        // Arrange
        var cachedResponse = new QaResponse(
            "Risposta italiana dalla cache",
            Array.Empty<Snippet>(),
            promptTokens: 50,
            completionTokens: 30,
            totalTokens: 80,
            confidence: 0.9
        );

        SetupMockCache(cachedResponse);

        // Act
        var result = await _service.AskAsync("chess", "Come si muove?", language: "it");

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("Risposta italiana dalla cache");

        // Verify services were NOT called
        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _qdrantMock.Verify(
            q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _llmMock.Verify(
            l => l.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region ExplainAsync - Language Propagation

    /// <summary>
    /// BDD Scenario: ExplainAsync with German language propagates to services
    /// Given: User requests explanation in German
    /// When: ExplainAsync is called with language "de"
    /// Then: EmbeddingService and QdrantService receive "de" parameter
    /// </summary>
    [Fact]
    public async Task ExplainAsync_WithGermanLanguage_PropagatesLanguageToServices()
    {
        // Arrange
        string? capturedEmbeddingLanguage = null;
        string? capturedSearchLanguage = null;

        SetupMockExplainCache(null);

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, lang, _) =>
            {
                capturedEmbeddingLanguage = lang;
            })
            .ReturnsAsync(CreateSuccessfulEmbeddingResult());

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, float[], string, int, CancellationToken>((_, _, lang, _, _) =>
            {
                capturedSearchLanguage = lang;
            })
            .ReturnsAsync(CreateSuccessfulSearchResult());

        SetupMockLlm("Deutsche Erklärung");

        // Act
        var result = await _service.ExplainAsync("chess", "Rochade", language: "de");

        // Assert
        result.Should().NotBeNull();
        result.script.Should().NotBeEmpty();
        result.script.Should().Contain("Rochade");
        capturedEmbeddingLanguage.Should().Be("de");
        capturedSearchLanguage.Should().Be("de");
    }

    /// <summary>
    /// BDD Scenario: ExplainAsync with null language defaults to English
    /// Given: Language parameter is null
    /// When: ExplainAsync is called
    /// Then: Services receive "en" as language
    /// </summary>
    [Fact]
    public async Task ExplainAsync_NullLanguage_DefaultsToEnglish()
    {
        // Arrange
        string? capturedLanguage = null;

        SetupMockExplainCache(null);

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((_, lang, _) =>
            {
                capturedLanguage = lang;
            })
            .ReturnsAsync(CreateSuccessfulEmbeddingResult());

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessfulSearchResult());

        SetupMockLlm("English explanation");

        // Act
        var result = await _service.ExplainAsync("chess", "castling", language: null);

        // Assert
        result.Should().NotBeNull();
        result.script.Should().NotBeEmpty();
        result.script.Should().Contain("castling");
        capturedLanguage.Should().Be("en");
    }

    #endregion

    #region ExplainAsync - Cache Integration with Language

    /// <summary>
    /// BDD Scenario: ExplainAsync language-specific cache keys
    /// Given: Same topic explained in different languages
    /// When: ExplainAsync is called for French then Spanish
    /// Then: Different cache keys are used
    /// </summary>
    [Fact]
    public async Task ExplainAsync_DifferentLanguages_UsesDifferentCacheKeys()
    {
        // Arrange
        var capturedCacheKeys = new List<string>();


        _cacheMock
            .Setup(c => c.GetAsync<ExplainResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((key, _) => capturedCacheKeys.Add(key))
            .ReturnsAsync((ExplainResponse?)null);

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessfulEmbeddingResult());

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessfulSearchResult());

        SetupMockLlm("Explanation");

        // Act
        await _service.ExplainAsync("chess", "castling", language: "fr");
        await _service.ExplainAsync("chess", "castling", language: "es");

        // Assert
        capturedCacheKeys.Count.Should().Be(2);
        capturedCacheKeys[0].Should().Contain(":lang:fr");
        capturedCacheKeys[1].Should().Contain(":lang:es");
        capturedCacheKeys[1].Should().NotBe(capturedCacheKeys[0]);
    }

    /// <summary>
    /// BDD Scenario: Cached Explain response with language
    /// Given: French explanation is cached
    /// When: Same topic requested in French
    /// Then: Cached response returned without service calls
    /// </summary>
    [Fact]
    public async Task ExplainAsync_CacheHitWithLanguage_SkipsServicesAndReturnsCache()
    {
        // Arrange
        var outline = new ExplainOutline("Roque", new List<string> { "Introduction", "Règles" });
        var cachedResponse = new ExplainResponse(
            outline,
            "Explication française du cache",
            Array.Empty<Snippet>(),
            estimatedReadingTimeMinutes: 5,
            promptTokens: 100,
            completionTokens: 150,
            totalTokens: 250,
            confidence: 0.85
        );

        SetupMockExplainCache(cachedResponse);

        // Act
        var result = await _service.ExplainAsync("chess", "roque", language: "fr");

        // Assert
        result.Should().NotBeNull();
        result.script.Should().Be("Explication française du cache");

        // Verify services were NOT called
        _embeddingMock.Verify(
            e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _qdrantMock.Verify(
            q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region Error Handling with Language Context

    /// <summary>
    /// BDD Scenario: Embedding failure includes language in error
    /// Given: EmbeddingService fails for Spanish query
    /// When: AskAsync is called with language "es"
    /// Then: Error response includes language context
    /// </summary>
    [Fact]
    public async Task AskAsync_EmbeddingFailureWithLanguage_ReturnsErrorWithContext()
    {
        // Arrange
        SetupMockCache(null);

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                "es",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmbeddingResult.CreateFailure("Embedding service unavailable"));

        // Act
        var result = await _service.AskAsync("chess", "¿Cómo se juega?", language: "es");

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("Unable to process query.");
    }

    /// <summary>
    /// BDD Scenario: Search failure with language parameter
    /// Given: QdrantService fails for Italian search
    /// When: AskAsync is called with language "it"
    /// Then: Error response returned
    /// </summary>
    [Fact]
    public async Task AskAsync_SearchFailureWithLanguage_ReturnsError()
    {
        // Arrange
        SetupMockCache(null);

        _embeddingMock
            .Setup(e => e.GenerateEmbeddingAsync(
                It.IsAny<string>(),
                "it",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSuccessfulEmbeddingResult());

        _qdrantMock
            .Setup(q => q.SearchAsync(
                It.IsAny<string>(),
                It.IsAny<float[]>(),
                "it",
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SearchResult.CreateFailure("Qdrant connection failed"));

        // Act
        var result = await _service.AskAsync("chess", "Come si gioca?", language: "it");

        // Assert
        result.Should().NotBeNull();
        result.answer.Should().Be("Not specified");
    }

    #endregion

    #region Helper Methods

    private void SetupMockCache(QaResponse? response)
    {
        _cacheMock
            .Setup(c => c.GetAsync<QaResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private void SetupMockExplainCache(ExplainResponse? response)
    {
        _cacheMock
            .Setup(c => c.GetAsync<ExplainResponse>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);
    }

    private EmbeddingResult CreateSuccessfulEmbeddingResult()
    {
        var embedding = Enumerable.Repeat(0.1f, 768).ToArray();
        return EmbeddingResult.CreateSuccess(new List<float[]> { embedding });
    }

    private SearchResult CreateSuccessfulSearchResult()
    {
        var results = new List<SearchResultItem>
        {
            new SearchResultItem
            {
                Text = "Test context",
                Score = 0.9f,
                PdfId = "pdf-001",
                Page = 1,
                ChunkIndex = 0
            }
        };
        return SearchResult.CreateSuccess(results);
    }

    private void SetupMockLlm(string response)
    {
        var llmResponse = LlmCompletionResult.CreateSuccess(
            response,
            new LlmUsage(50, 50, 100)
        );

        _llmMock
            .Setup(l => l.GenerateCompletionAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResponse);
    }

    #endregion
}
