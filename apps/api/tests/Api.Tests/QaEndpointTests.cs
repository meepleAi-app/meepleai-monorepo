using Api.Infrastructure;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

        // Mock dependencies for RagService (AI-01 not used in this legacy test)
        var embeddingServiceMock = new Mock<EmbeddingService>(
            Mock.Of<IHttpClientFactory>(),
            Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>(),
            Mock.Of<ILogger<EmbeddingService>>());
        var qdrantServiceMock = new Mock<QdrantService>(
            Mock.Of<Microsoft.Extensions.Configuration.IConfiguration>(),
            Mock.Of<ILogger<QdrantService>>());
        var ragLoggerMock = Mock.Of<ILogger<RagService>>();

        var ragService = new RagService(dbContext, embeddingServiceMock.Object, qdrantServiceMock.Object, ragLoggerMock);

        var tenantId = "tenant-test";
        var gameId = "demo-chess";

        var spec = await ruleService.GetOrCreateDemoAsync(tenantId, gameId);

        Assert.Equal(gameId, spec.gameId);
        Assert.Equal(2, spec.rules.Count);

        var response = await ragService.AskAsync(tenantId, gameId, "How many players?");

        Assert.Equal("Two players.", response.answer);
        Assert.Single(response.snippets);
        Assert.Equal("Two players.", response.snippets[0].text);
        Assert.Equal("RuleSpec:v0-demo:Basics", response.snippets[0].source);
    }
}
