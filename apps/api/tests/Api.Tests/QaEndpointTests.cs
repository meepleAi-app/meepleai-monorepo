using Api.Infrastructure;
using Api.Models;
using Api.Services;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class QaEndpointTests
{
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

        var ragService = new RagService(dbContext, embeddingServiceMock.Object, qdrantServiceMock.Object, hybridSearchMock.Object, llmServiceMock.Object, cacheServiceMock.Object, CreatePromptTemplateMock().Object, ragLoggerMock);

        var gameId = "demo-chess";

        var spec = await ruleService.GetOrCreateDemoAsync(gameId);

        Assert.Equal(gameId, spec.gameId);
        Assert.Equal(2, spec.rules.Count);

        var response = await ragService.AskAsync(gameId, "How many players?");

        Assert.Equal("Two players.", response.answer);
        Assert.Single(response.snippets);
        Assert.Equal("Two players.", response.snippets[0].text);
        Assert.Equal("PDF:pdf-demo-chess", response.snippets[0].source);
        Assert.Equal(6, response.promptTokens);
        Assert.Equal(4, response.completionTokens);
        Assert.Equal(10, response.totalTokens);
    }
}
