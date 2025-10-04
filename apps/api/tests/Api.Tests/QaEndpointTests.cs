using Api.Infrastructure;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class QaEndpointTests
{
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
        var ruleService = new RuleSpecService(dbContext);

        // Mock dependencies for RagService (AI-01 mocked to avoid external API calls)
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        configMock.Setup(c => c["OPENROUTER_API_KEY"]).Returns("test-key");

        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(new HttpClient());

        var embeddingServiceMock = new Mock<EmbeddingService>(
            httpClientFactoryMock.Object,
            configMock.Object,
            Mock.Of<ILogger<EmbeddingService>>());

        // Configure mock to return successful embedding result
        var mockEmbedding = Enumerable.Repeat(0.1f, 1536).ToArray();
        var embeddingResult = EmbeddingResult.CreateSuccess(new List<float[]> { mockEmbedding });
        embeddingServiceMock
            .Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(embeddingResult);

        var qdrantServiceMock = new Mock<QdrantService>(
            Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>(),
            Mock.Of<ILogger<QdrantService>>());

        // Configure mock to return successful search result with expected answer
        var searchResults = new List<SearchResultItem>
        {
            new() { Score = 0.95f, Text = "Two players.", PdfId = "pdf-demo-chess", Page = 1, ChunkIndex = 0 }
        };
        var searchResult = SearchResult.CreateSuccess(searchResults);
        qdrantServiceMock
            .Setup(q => q.SearchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<float[]>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchResult);

        var ragLoggerMock = Mock.Of<ILogger<RagService>>();
        var llmServiceMock = new Mock<ILlmService>();

        // Configure LLM mock to return successful completion
        var llmResult = LlmCompletionResult.CreateSuccess("Two players.");
        llmServiceMock
            .Setup(l => l.GenerateCompletionAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(llmResult);

        var cacheServiceMock = new Mock<IAiResponseCacheService>();
        var ragService = new RagService(dbContext, embeddingServiceMock.Object, qdrantServiceMock.Object, llmServiceMock.Object, cacheServiceMock.Object, ragLoggerMock);

        var tenantId = "tenant-test";
        var gameId = "demo-chess";

        var spec = await ruleService.GetOrCreateDemoAsync(tenantId, gameId);

        Assert.Equal(gameId, spec.gameId);
        Assert.Equal(2, spec.rules.Count);

        var response = await ragService.AskAsync(tenantId, gameId, "How many players?");

        Assert.Equal("Two players.", response.answer);
        Assert.Single(response.snippets);
        Assert.Equal("Two players.", response.snippets[0].text);
        Assert.Equal("PDF:pdf-demo-chess", response.snippets[0].source);
    }
}
