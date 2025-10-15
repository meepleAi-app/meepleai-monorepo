using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class AiRequestLogServiceTests
{
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

    [Fact]
    public async Task LogRequestAsync_PersistsLogEntry()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        await service.LogRequestAsync(
            userId: "user-1",
            gameId: "game-1",
            endpoint: "qa",
            query: "What is the setup?",
            responseSnippet: "Setup details...",
            latencyMs: 120,
            tokenCount: 500,
            confidence: 0.9,
            status: "Success",
            errorMessage: null,
            ipAddress: "127.0.0.1",
            userAgent: "agent",
            promptTokens: 300,
            completionTokens: 200,
            model: "anthropic/claude-3.5-sonnet",
            finishReason: "stop",
            ct: CancellationToken.None);

        var logs = await dbContext.AiRequestLogs.ToListAsync();
        Assert.Single(logs);

        var log = logs[0];
        Assert.Equal("user-1", log.UserId);
        Assert.Equal("game-1", log.GameId);
        Assert.Equal("qa", log.Endpoint);
        Assert.Equal("What is the setup?", log.Query);
        Assert.Equal("Setup details...", log.ResponseSnippet);
        Assert.Equal(120, log.LatencyMs);
        Assert.Equal(500, log.TokenCount);
        Assert.Equal(300, log.PromptTokens);
        Assert.Equal(200, log.CompletionTokens);
        Assert.Equal(0.9, log.Confidence);
        Assert.Equal("Success", log.Status);
        Assert.Equal("127.0.0.1", log.IpAddress);
        Assert.Equal("agent", log.UserAgent);
        Assert.Equal("anthropic/claude-3.5-sonnet", log.Model);
        Assert.Equal("stop", log.FinishReason);
        Assert.True(log.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task LogRequestAsync_WhenDbThrows_LogsError()
    {
        var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        await dbContext.DisposeAsync();

        await service.LogRequestAsync(null, null, "qa", null, null, 10, ct: CancellationToken.None);

        var errorInvocation = loggerMock.Invocations
            .FirstOrDefault(invocation => invocation.Arguments.FirstOrDefault() is LogLevel level && level == LogLevel.Error);

        Assert.NotNull(errorInvocation);
        var state = errorInvocation!.Arguments[2];
        Assert.Contains("Failed to log AI request", state.ToString());
    }

    [Fact]
    public async Task GetRequestsAsync_AppliesFiltersAndPagination()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                LatencyMs = 100,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Endpoint = "setup",
                UserId = "user-2",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                LatencyMs = 200,
                Status = "Error"
            },
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-2",
                CreatedAt = DateTime.UtcNow.AddMinutes(-1),
                LatencyMs = 150,
                Status = "Success"
            });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        var result = await service.GetRequestsAsync(
            limit: 1,
            offset: 0,
            endpoint: "qa",
            userId: "user-1",
            gameId: "game-2",
            ct: CancellationToken.None);

        Assert.Single(result.Requests);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("game-2", result.Requests[0].GameId);
        Assert.Equal("qa", result.Requests[0].Endpoint);
    }

    [Fact]
    public async Task GetStatsAsync_ComputesAggregates()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow.AddHours(-2),
                LatencyMs = 100,
                TokenCount = 200,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-2",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow.AddHours(-1),
                LatencyMs = 300,
                TokenCount = 100,
                Status = "Error"
            },
            new AiRequestLogEntity
            {
                Endpoint = "setup",
                UserId = "user-1",
                GameId = "game-2",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30),
                LatencyMs = 200,
                TokenCount = 50,
                Status = "Success"
            });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(
            startDate: DateTime.UtcNow.AddHours(-3),
            endDate: DateTime.UtcNow,
            ct: CancellationToken.None);

        Assert.Equal(3, stats.TotalRequests);
        Assert.Equal(200, stats.AvgLatencyMs);
        Assert.Equal(350, stats.TotalTokens);
        Assert.Equal(2d / 3d, stats.SuccessRate, 3);
        Assert.Equal(2, stats.EndpointCounts["qa"]);
        Assert.Equal(1, stats.EndpointCounts["setup"]);
    }
}
