using Api.Infrastructure;
using Api.Models;
using Api.Services;
using Api.Services.Rag;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;

public class QaEndpointTests
{
    private readonly ITestOutputHelper _output;

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

    [Fact]
    public async Task RoundTrip_CreatesAndQueriesDemoSpec()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setupContext = new MeepleAiDbContext(options))
        {
            await setupContext.Database.EnsureCreatedAsync();
        }

        await using var dbContext = new MeepleAiDbContext(options);

        var cacheServiceMock = new Mock<IAiResponseCacheService>();
        cacheServiceMock
            .Setup(x => x.InvalidateGameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cacheServiceMock
            .Setup(x => x.InvalidateEndpointAsync(It.IsAny<string>(), It.IsAny<AiCacheEndpoint>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var ruleService = new RuleSpecService(dbContext, cacheServiceMock.Object);

        // Mock dependencies for RagService (AI-01 mocked to avoid external API calls)
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["OPENROUTER_API_KEY"]).Returns("test-key");

        var embeddingServiceMock = new Mock<IEmbeddingService>();

        // Configure mock to return successful embedding result
        var mockEmbedding = Enumerable.Repeat(0.1f, 1536).ToArray();
        var embeddingResult = EmbeddingResult.CreateSuccess(new List<float[]> { mockEmbedding });
        embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingResult);

        var qdrantServiceMock = new Mock<IQdrantService>();

        // Configure mock to return successful search result with expected answer
        var searchResults = new List<SearchResultItem>
        {
            new() { Score = 0.95f, Text = "Two players.", PdfId = "pdf-demo-chess", Page = 1, ChunkIndex = 0 }
        };
        var searchResult = SearchResult.CreateSuccess(searchResults);
        qdrantServiceMock
            .Setup(q => q.SearchAsync(It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResult);

        var ragLoggerMock = Mock.Of<ILogger<RagService>>();
        var llmServiceMock = new Mock<ILlmService>();

        // Configure LLM mock to return successful completion
        var llmResult = LlmCompletionResult.CreateSuccess(
            "Two players.",
            new LlmUsage(6, 4, 10),
            new Dictionary<string, string> { { "model", "anthropic/claude-3.5-sonnet" } });
        llmServiceMock
            .Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        var hybridSearchMock = new Mock<IHybridSearchService>();
        hybridSearchMock.Setup(x => x.SearchAsync(
            It.IsAny<string>(),
            It.IsAny<Guid>(),
            It.IsAny<SearchMode>(),
            It.IsAny<int>(),
            It.IsAny<float>(),
            It.IsAny<float>(),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<HybridSearchResult>());

        var ragService = new RagService(dbContext, embeddingServiceMock.Object, qdrantServiceMock.Object, hybridSearchMock.Object, llmServiceMock.Object, cacheServiceMock.Object, CreatePromptTemplateMock().Object, ragLoggerMock, CreateQueryExpansionMock().Object, CreateRerankerMock().Object, CreateCitationExtractorMock().Object);

        var gameId = "demo-chess";

        var spec = await ruleService.GetOrCreateDemoAsync(gameId);

        spec.gameId.Should().Be(gameId);
        spec.rules.Count.Should().Be(2);

        var response = await ragService.AskAsync(gameId, "How many players?");

        response.answer.Should().Be("Two players.");
        response.snippets.Should().ContainSingle();
        response.snippets[0].text.Should().Be("Two players.");
        response.snippets[0].source.Should().Be("PDF:pdf-demo-chess");
        response.promptTokens.Should().Be(6);
        response.completionTokens.Should().Be(4);
        response.totalTokens.Should().Be(10);
    }
}
