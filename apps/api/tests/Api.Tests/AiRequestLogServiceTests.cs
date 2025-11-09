using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Xunit;

public class AiRequestLogServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;

    public AiRequestLogServiceTests(ITestOutputHelper output)
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
        logs.Should().ContainSingle();

        var log = logs[0];
        log.UserId.Should().Be("user-1");
        log.GameId.Should().Be("game-1");
        log.Endpoint.Should().Be("qa");
        log.Query.Should().Be("What is the setup?");
        log.ResponseSnippet.Should().Be("Setup details...");
        log.LatencyMs.Should().Be(120);
        log.TokenCount.Should().Be(500);
        log.PromptTokens.Should().Be(300);
        log.CompletionTokens.Should().Be(200);
        log.Confidence.Should().Be(0.9);
        log.Status.Should().Be("Success");
        log.IpAddress.Should().Be("127.0.0.1");
        log.UserAgent.Should().Be("agent");
        log.Model.Should().Be("anthropic/claude-3.5-sonnet");
        log.FinishReason.Should().Be("stop");
        log.CreatedAt.Should().BeAfter(DateTime.UtcNow.AddMinutes(-5));
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

        errorInvocation.Should().NotBeNull();
        var state = errorInvocation!.Arguments[2];
        state.ToString().Should().Contain("Failed to log AI request");
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

        result.Requests.Should().ContainSingle();
        result.TotalCount.Should().Be(1);
        result.Requests[0].GameId.Should().Be("game-2");
        result.Requests[0].Endpoint.Should().Be("qa");
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

        stats.TotalRequests.Should().Be(3);
        stats.AvgLatencyMs.Should().Be(200);
        stats.TotalTokens.Should().Be(350);
        stats.SuccessRate.Should().Be(2d / 3d);
        stats.EndpointCounts["qa"].Should().Be(2);
        stats.EndpointCounts["setup"].Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_WhenNoData_ReturnsEmptyStats()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(ct: CancellationToken.None);

        stats.TotalRequests.Should().Be(0);
        stats.AvgLatencyMs.Should().Be(0);
        stats.TotalTokens.Should().Be(0);
        stats.SuccessRate.Should().Be(0);
        stats.EndpointCounts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatsAsync_FiltersBy_UserId()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow,
                LatencyMs = 100,
                TokenCount = 50,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Endpoint = "setup",
                UserId = "user-2",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow,
                LatencyMs = 200,
                TokenCount = 100,
                Status = "Success"
            });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(userId: "user-1", ct: CancellationToken.None);

        stats.TotalRequests.Should().Be(1);
        stats.AvgLatencyMs.Should().Be(100);
        stats.TotalTokens.Should().Be(50);
        stats.SuccessRate.Should().Be(1.0);
        stats.EndpointCounts.Should().ContainSingle();
        stats.EndpointCounts["qa"].Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_FiltersBy_GameId()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow,
                LatencyMs = 150,
                TokenCount = 75,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Endpoint = "explain",
                UserId = "user-1",
                GameId = "game-2",
                CreatedAt = DateTime.UtcNow,
                LatencyMs = 250,
                TokenCount = 125,
                Status = "Error"
            });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(gameId: "game-1", ct: CancellationToken.None);

        stats.TotalRequests.Should().Be(1);
        stats.AvgLatencyMs.Should().Be(150);
        stats.TotalTokens.Should().Be(75);
        stats.SuccessRate.Should().Be(1.0);
        stats.EndpointCounts.Should().ContainSingle();
        stats.EndpointCounts["qa"].Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_ComputesSuccessRate_WhenAllErrors()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow,
                LatencyMs = 100,
                TokenCount = 0,
                Status = "Error"
            },
            new AiRequestLogEntity
            {
                Endpoint = "setup",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow,
                LatencyMs = 150,
                TokenCount = 0,
                Status = "Error"
            });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(ct: CancellationToken.None);

        stats.TotalRequests.Should().Be(2);
        stats.SuccessRate.Should().Be(0.0);
    }

    [Fact]
    public async Task GetRequestsAsync_FiltersBy_DateRange()
    {
        await using var dbContext = CreateInMemoryContext();
        var now = DateTime.UtcNow;
        dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = now.AddHours(-10),
                LatencyMs = 100,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Endpoint = "setup",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = now.AddHours(-5),
                LatencyMs = 200,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Endpoint = "explain",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = now.AddHours(-1),
                LatencyMs = 150,
                Status = "Success"
            });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        var result = await service.GetRequestsAsync(
            startDate: now.AddHours(-6),
            endDate: now,
            ct: CancellationToken.None);

        result.Requests.Count.Should().Be(2);
        result.TotalCount.Should().Be(2);
        result.Requests.Should().NotContain(r => r.CreatedAt < now.AddHours(-6));
    }

    [Fact]
    public async Task GetRequestsAsync_OrdersByCreatedAtDescending()
    {
        await using var dbContext = CreateInMemoryContext();
        var now = DateTime.UtcNow;
        dbContext.AiRequestLogs.AddRange(
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = now.AddHours(-3),
                LatencyMs = 100,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Endpoint = "setup",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = now.AddHours(-1),
                LatencyMs = 200,
                Status = "Success"
            },
            new AiRequestLogEntity
            {
                Endpoint = "explain",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = now.AddHours(-2),
                LatencyMs = 150,
                Status = "Success"
            });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        var result = await service.GetRequestsAsync(ct: CancellationToken.None);

        result.Requests.Count.Should().Be(3);
        result.Requests[0].Endpoint.Should().Be("setup"); // Most recent
        result.Requests[1].Endpoint.Should().Be("explain");
        result.Requests[2].Endpoint.Should().Be("qa"); // Oldest
    }

    [Fact]
    public async Task GetRequestsAsync_WhenOffsetBeyondTotal_ReturnsEmpty()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AiRequestLogs.Add(
            new AiRequestLogEntity
            {
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-1",
                CreatedAt = DateTime.UtcNow,
                LatencyMs = 100,
                Status = "Success"
            });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        var result = await service.GetRequestsAsync(offset: 10, ct: CancellationToken.None);

        result.Requests.Should().BeEmpty();
        result.TotalCount.Should().Be(1); // Total count still correct
    }

    [Fact]
    public async Task LogRequestAsync_WithNullOptionalValues_PersistsSuccessfully()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContext, loggerMock.Object);

        await service.LogRequestAsync(
            userId: null,
            gameId: null,
            endpoint: "qa",
            query: null,
            responseSnippet: null,
            latencyMs: 100,
            tokenCount: null,
            confidence: null,
            status: "Success",
            errorMessage: null,
            ipAddress: null,
            userAgent: null,
            promptTokens: null,
            completionTokens: null,
            model: null,
            finishReason: null,
            apiKeyId: null,
            ct: CancellationToken.None);

        var logs = await dbContext.AiRequestLogs.ToListAsync();
        logs.Should().ContainSingle();

        var log = logs[0];
        log.UserId.Should().BeNull();
        log.GameId.Should().BeNull();
        log.Query.Should().BeNull();
        log.Confidence.Should().BeNull();
        log.TokenCount.Should().Be(0); // Default value
        log.PromptTokens.Should().Be(0);
        log.CompletionTokens.Should().Be(0);
    }
}