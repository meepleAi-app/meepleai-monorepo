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
}
