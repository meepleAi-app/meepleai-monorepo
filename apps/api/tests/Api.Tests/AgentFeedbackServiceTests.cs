using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class AgentFeedbackServiceTests
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
    public async Task RecordFeedbackAsync_CreatesOrUpdatesFeedback()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await service.RecordFeedbackAsync("msg-1", "qa", "user-1", "helpful", "game-1");

        var entry = await dbContext.AgentFeedbacks.SingleAsync();
        Assert.Equal("msg-1", entry.MessageId);
        Assert.Equal("qa", entry.Endpoint);
        Assert.Equal("user-1", entry.UserId);
        Assert.Equal("helpful", entry.Outcome);
        Assert.Equal("game-1", entry.GameId);

        await service.RecordFeedbackAsync("msg-1", "qa", "user-1", "not-helpful", "game-1");

        entry = await dbContext.AgentFeedbacks.SingleAsync();
        Assert.Equal("not-helpful", entry.Outcome);
    }

    [Fact]
    public async Task RecordFeedbackAsync_RemovesEntryWhenOutcomeNull()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AgentFeedbacks.Add(new AgentFeedbackEntity
        {
            MessageId = "msg-2",
            Endpoint = "qa",
            UserId = "user-1",
            GameId = "game-1",
            Outcome = "helpful"
        });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await service.RecordFeedbackAsync("msg-2", "qa", "user-1", null, "game-1");

        Assert.Empty(dbContext.AgentFeedbacks);
    }

    [Fact]
    public async Task GetStatsAsync_ComputesAggregates()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity
            {
                MessageId = "msg-1",
                Endpoint = "qa",
                UserId = "user-1",
                GameId = "game-1",
                Outcome = "helpful",
                CreatedAt = DateTime.UtcNow.AddHours(-2)
            },
            new AgentFeedbackEntity
            {
                MessageId = "msg-2",
                Endpoint = "qa",
                UserId = "user-2",
                GameId = "game-1",
                Outcome = "not-helpful",
                CreatedAt = DateTime.UtcNow.AddHours(-1)
            },
            new AgentFeedbackEntity
            {
                MessageId = "msg-3",
                Endpoint = "setup",
                UserId = "user-3",
                GameId = "game-2",
                Outcome = "helpful",
                CreatedAt = DateTime.UtcNow.AddMinutes(-30)
            });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(ct: CancellationToken.None);

        Assert.Equal(3, stats.TotalFeedback);
        Assert.Equal(2, stats.OutcomeCounts["helpful"]);
        Assert.Equal(1, stats.OutcomeCounts["not-helpful"]);
        Assert.Equal(2, stats.EndpointOutcomeCounts["qa"].Values.Sum());
        Assert.Equal(1, stats.EndpointOutcomeCounts["setup"].Values.Sum());
    }

    #region Phase 3: Additional Coverage Tests

    [Fact]
    public async Task RecordFeedbackAsync_WithNullMessageId_ThrowsArgumentException()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.RecordFeedbackAsync(null!, "qa", "user-1", "helpful", "game-1"));
    }

    [Fact]
    public async Task RecordFeedbackAsync_WithEmptyEndpoint_ThrowsArgumentException()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.RecordFeedbackAsync("msg-1", "", "user-1", "helpful", "game-1"));
    }

    [Fact]
    public async Task RecordFeedbackAsync_WithNullUserId_ThrowsArgumentException()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await service.RecordFeedbackAsync("msg-1", "qa", null!, "helpful", "game-1"));
    }

    [Fact]
    public async Task RecordFeedbackAsync_UpdatesExistingFeedback()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AgentFeedbacks.Add(new AgentFeedbackEntity
        {
            MessageId = "msg-1",
            Endpoint = "qa",
            UserId = "user-1",
            GameId = "game-1",
            Outcome = "helpful",
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            UpdatedAt = DateTime.UtcNow.AddMinutes(-5)
        });
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await service.RecordFeedbackAsync("msg-1", "explain", "user-1", "not-helpful", "game-2");

        var updated = await dbContext.AgentFeedbacks.SingleAsync();
        Assert.Equal("not-helpful", updated.Outcome);
        Assert.Equal("explain", updated.Endpoint);
        Assert.Equal("game-2", updated.GameId);
        Assert.True(updated.UpdatedAt > updated.CreatedAt);
    }

    [Fact]
    public async Task GetStatsAsync_FiltersByEndpoint()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity { MessageId = "1", Endpoint = "qa", UserId = "user-1", Outcome = "helpful", CreatedAt = DateTime.UtcNow },
            new AgentFeedbackEntity { MessageId = "2", Endpoint = "explain", UserId = "user-1", Outcome = "helpful", CreatedAt = DateTime.UtcNow },
            new AgentFeedbackEntity { MessageId = "3", Endpoint = "qa", UserId = "user-1", Outcome = "not-helpful", CreatedAt = DateTime.UtcNow }
        );
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(endpoint: "qa");

        Assert.Equal(2, stats.TotalFeedback);
        Assert.Equal(1, stats.OutcomeCounts["helpful"]);
        Assert.Equal(1, stats.OutcomeCounts["not-helpful"]);
    }

    [Fact]
    public async Task GetStatsAsync_FiltersByUserId()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity { MessageId = "1", Endpoint = "qa", UserId = "user-1", Outcome = "helpful", CreatedAt = DateTime.UtcNow },
            new AgentFeedbackEntity { MessageId = "2", Endpoint = "qa", UserId = "user-2", Outcome = "helpful", CreatedAt = DateTime.UtcNow }
        );
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(userId: "user-1");

        Assert.Equal(1, stats.TotalFeedback);
        Assert.Equal(1, stats.OutcomeCounts["helpful"]);
    }

    [Fact]
    public async Task GetStatsAsync_FiltersByGameId()
    {
        await using var dbContext = CreateInMemoryContext();
        dbContext.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity { MessageId = "1", Endpoint = "qa", UserId = "user-1", GameId = "game-1", Outcome = "helpful", CreatedAt = DateTime.UtcNow },
            new AgentFeedbackEntity { MessageId = "2", Endpoint = "qa", UserId = "user-1", GameId = "game-2", Outcome = "not-helpful", CreatedAt = DateTime.UtcNow }
        );
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(gameId: "game-1");

        Assert.Equal(1, stats.TotalFeedback);
        Assert.Equal(1, stats.OutcomeCounts["helpful"]);
    }

    [Fact]
    public async Task GetStatsAsync_FiltersByDateRange()
    {
        await using var dbContext = CreateInMemoryContext();
        var now = DateTime.UtcNow;
        dbContext.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity { MessageId = "1", Endpoint = "qa", UserId = "user-1", Outcome = "helpful", CreatedAt = now.AddDays(-5) },
            new AgentFeedbackEntity { MessageId = "2", Endpoint = "qa", UserId = "user-1", Outcome = "helpful", CreatedAt = now.AddDays(-2) },
            new AgentFeedbackEntity { MessageId = "3", Endpoint = "qa", UserId = "user-1", Outcome = "not-helpful", CreatedAt = now }
        );
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync(startDate: now.AddDays(-3), endDate: now.AddDays(-1));

        Assert.Equal(1, stats.TotalFeedback); // Only msg-2 in range
        Assert.Equal(1, stats.OutcomeCounts["helpful"]);
    }

    [Fact]
    public async Task GetStatsAsync_WithNoFeedback_ReturnsZeroCounts()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync();

        Assert.Equal(0, stats.TotalFeedback);
        Assert.Empty(stats.OutcomeCounts);
        Assert.Empty(stats.EndpointOutcomeCounts);
    }

    [Fact]
    public async Task RecordFeedbackAsync_WithNullGameId_StoresSuccessfully()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await service.RecordFeedbackAsync("msg-1", "qa", "user-1", "helpful", null);

        var entry = await dbContext.AgentFeedbacks.SingleAsync();
        Assert.Equal("msg-1", entry.MessageId);
        Assert.Null(entry.GameId);
    }

    [Fact]
    public async Task GetStatsAsync_WithMultipleFeedbacks_AggregatesBySameOutcome()
    {
        await using var dbContext = CreateInMemoryContext();
        // Note: Service is case-sensitive, so using consistent casing
        dbContext.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity { MessageId = "1", Endpoint = "qa", UserId = "user-1", Outcome = "helpful", CreatedAt = DateTime.UtcNow },
            new AgentFeedbackEntity { MessageId = "2", Endpoint = "qa", UserId = "user-2", Outcome = "helpful", CreatedAt = DateTime.UtcNow },
            new AgentFeedbackEntity { MessageId = "3", Endpoint = "explain", UserId = "user-1", Outcome = "not_helpful", CreatedAt = DateTime.UtcNow }
        );
        await dbContext.SaveChangesAsync();

        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync();

        // Should aggregate by outcome (case-sensitive)
        Assert.Equal(2, stats.OutcomeCounts["helpful"]);
        Assert.Equal(1, stats.OutcomeCounts["not_helpful"]);
        Assert.Equal(3, stats.TotalFeedback);
    }

    #endregion
}
