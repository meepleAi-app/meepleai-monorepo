using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests;

public class AiRequestLogServiceTests
{
    [Fact]
    public async Task LogRequestAsync_PersistsLogEntry()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new MeepleAiDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(context, loggerMock.Object);

        var beforeCall = DateTime.UtcNow;
        await service.LogRequestAsync(
            userId: "user-1",
            gameId: "game-1",
            endpoint: "chat.completions",
            query: "What is a meeple?",
            responseSnippet: "Meeple is a board game term...",
            latencyMs: 123,
            tokenCount: 456,
            confidence: 0.87,
            status: "Success",
            errorMessage: null,
            ipAddress: "127.0.0.1",
            userAgent: "test-agent/1.0");
        var afterCall = DateTime.UtcNow;

        var stored = await context.AiRequestLogs.SingleAsync();

        Assert.Equal("user-1", stored.UserId);
        Assert.Equal("game-1", stored.GameId);
        Assert.Equal("chat.completions", stored.Endpoint);
        Assert.Equal("What is a meeple?", stored.Query);
        Assert.Equal("Meeple is a board game term...", stored.ResponseSnippet);
        Assert.Equal(123, stored.LatencyMs);
        Assert.Equal(456, stored.TokenCount);
        Assert.Equal(0.87, stored.Confidence);
        Assert.Equal("Success", stored.Status);
        Assert.Null(stored.ErrorMessage);
        Assert.Equal("127.0.0.1", stored.IpAddress);
        Assert.Equal("test-agent/1.0", stored.UserAgent);
        Assert.InRange(stored.CreatedAt, beforeCall, afterCall);
    }

    [Fact]
    public async Task LogRequestAsync_WhenSaveFails_LogsErrorWithoutThrowing()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>().Options;
        var dbContextMock = new Mock<MeepleAiDbContext>(options);
        var dbSetMock = new Mock<DbSet<AiRequestLogEntity>>();
        dbSetMock
            .Setup(set => set.Add(It.IsAny<AiRequestLogEntity>()))
            .Returns((EntityEntry<AiRequestLogEntity>)null!);

        dbContextMock
            .Setup(context => context.Set<AiRequestLogEntity>())
            .Returns(dbSetMock.Object);

        dbContextMock
            .Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Database unavailable"));

        var loggerMock = new Mock<ILogger<AiRequestLogService>>();
        var service = new AiRequestLogService(dbContextMock.Object, loggerMock.Object);

        var exception = await Record.ExceptionAsync(() => service.LogRequestAsync(
            userId: "user-1",
            gameId: "game-1",
            endpoint: "chat.completions",
            query: null,
            responseSnippet: null,
            latencyMs: 42));

        Assert.Null(exception);
        dbSetMock.Verify(set => set.Add(It.Is<AiRequestLogEntity>(log => log.Endpoint == "chat.completions")), Times.Once);
        loggerMock.Verify(
            logger => logger.LogError(
                It.IsAny<Exception>(),
                It.Is<string>(message => message.Contains("Failed to log AI request")),
                "chat.completions"),
            Times.Once);
    }

    [Fact]
    public async Task GetRequestsAsync_AppliesFiltersAndPagination()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new MeepleAiDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var baseTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var seededLogs = new List<AiRequestLogEntity>
        {
            new()
            {
                UserId = "user-1",
                GameId = "game-1",
                Endpoint = "chat.completions",
                LatencyMs = 100,
                TokenCount = 200,
                Status = "Success",
                CreatedAt = baseTime.AddMinutes(-10)
            },
            new()
            {
                UserId = "user-1",
                GameId = "game-1",
                Endpoint = "chat.completions",
                LatencyMs = 120,
                TokenCount = 210,
                Status = "Success",
                CreatedAt = baseTime.AddMinutes(-5)
            },
            new()
            {
                UserId = "user-2",
                GameId = "game-2",
                Endpoint = "rules.lookup",
                LatencyMs = 150,
                TokenCount = 150,
                Status = "Error",
                CreatedAt = baseTime.AddMinutes(-20)
            },
            new()
            {
                UserId = "user-3",
                GameId = "game-1",
                Endpoint = "summary.generate",
                LatencyMs = 80,
                TokenCount = 180,
                Status = "Success",
                CreatedAt = baseTime.AddMinutes(-1)
            }
        };

        context.AiRequestLogs.AddRange(seededLogs);
        await context.SaveChangesAsync();

        var service = new AiRequestLogService(context, Mock.Of<ILogger<AiRequestLogService>>());

        var all = await service.GetRequestsAsync();
        Assert.Equal(
            seededLogs.OrderByDescending(log => log.CreatedAt).Select(log => log.Id),
            all.Select(log => log.Id));

        var paged = await service.GetRequestsAsync(limit: 2, offset: 1);
        Assert.Equal(2, paged.Count);
        Assert.Equal(
            seededLogs
                .OrderByDescending(log => log.CreatedAt)
                .Skip(1)
                .Take(2)
                .Select(log => log.Id),
            paged.Select(log => log.Id));

        var endpointFiltered = await service.GetRequestsAsync(endpoint: "chat.completions");
        Assert.All(endpointFiltered, log => Assert.Equal("chat.completions", log.Endpoint));

        var userFiltered = await service.GetRequestsAsync(userId: "user-1");
        Assert.All(userFiltered, log => Assert.Equal("user-1", log.UserId));

        var gameFiltered = await service.GetRequestsAsync(gameId: "game-1");
        Assert.All(gameFiltered, log => Assert.Equal("game-1", log.GameId));

        var dateFiltered = await service.GetRequestsAsync(
            startDate: baseTime.AddMinutes(-12),
            endDate: baseTime.AddMinutes(-2));
        Assert.Equal(2, dateFiltered.Count);
        Assert.All(dateFiltered, log => Assert.InRange(log.CreatedAt, baseTime.AddMinutes(-12), baseTime.AddMinutes(-2)));
    }

    [Fact]
    public async Task GetStatsAsync_ComputesAggregations()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new MeepleAiDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var baseTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var logs = new List<AiRequestLogEntity>
        {
            new()
            {
                UserId = "user-1",
                GameId = "game-1",
                Endpoint = "chat.completions",
                LatencyMs = 100,
                TokenCount = 50,
                Status = "Success",
                CreatedAt = baseTime.AddMinutes(-30)
            },
            new()
            {
                UserId = "user-2",
                GameId = "game-2",
                Endpoint = "chat.completions",
                LatencyMs = 200,
                TokenCount = 70,
                Status = "Error",
                CreatedAt = baseTime.AddMinutes(-20)
            },
            new()
            {
                UserId = "user-1",
                GameId = "game-1",
                Endpoint = "rules.lookup",
                LatencyMs = 150,
                TokenCount = null,
                Status = "Success",
                CreatedAt = baseTime.AddMinutes(-10)
            },
            new()
            {
                UserId = "user-3",
                GameId = "game-1",
                Endpoint = "rules.lookup",
                LatencyMs = 250,
                TokenCount = 40,
                Status = "Success",
                CreatedAt = baseTime.AddMinutes(-5)
            }
        };

        context.AiRequestLogs.AddRange(logs);
        await context.SaveChangesAsync();

        var service = new AiRequestLogService(context, Mock.Of<ILogger<AiRequestLogService>>());

        var stats = await service.GetStatsAsync(
            startDate: baseTime.AddMinutes(-40),
            endDate: baseTime,
            userId: null,
            gameId: null);

        Assert.Equal(4, stats.TotalRequests);
        Assert.Equal(175, stats.AvgLatencyMs);
        Assert.Equal(160, stats.TotalTokens);
        Assert.Equal(0.75, stats.SuccessRate, 3);
        Assert.Equal(2, stats.EndpointCounts["chat.completions"]);
        Assert.Equal(2, stats.EndpointCounts["rules.lookup"]);
    }
}
