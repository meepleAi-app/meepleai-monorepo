using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

public class AgentFeedbackServiceTests : IDisposable
{
    private readonly ITestOutputHelper _output;

    private readonly SqliteConnection _connection;

    public AgentFeedbackServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // Keep connection open for the lifetime of the test class
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
    public async Task RecordFeedbackAsync_CreatesOrUpdatesFeedback()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await service.RecordFeedbackAsync("msg-1", "qa", "user-1", "helpful", "game-1");

        var entry = await dbContext.AgentFeedbacks.SingleAsync();
        // Convert all Assert statements to FluentAssertions
        
        // Simple property assertions
        entry.MessageId.Should().Be("msg-1");
        entry.Endpoint.Should().Be("qa");
        entry.UserId.Should().Be("user-1");
        entry.Outcome.Should().Be("helpful");
        entry.GameId.Should().Be("game-1");

        await service.RecordFeedbackAsync("msg-1", "qa", "user-1", "not-helpful", "game-1");

        entry = await dbContext.AgentFeedbacks.SingleAsync();
        entry.Outcome.Should().Be("not-helpful");
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

        // Empty and Null assertions
        dbContext.AgentFeedbacks.Should().BeEmpty();
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

        // Stats assertions
        stats.TotalFeedback.Should().Be(3);
        stats.OutcomeCounts["helpful"].Should().Be(2);
        stats.OutcomeCounts["not-helpful"].Should().Be(1);
        stats.EndpointOutcomeCounts["qa"].Values.Sum().Should().Be(2);
        stats.EndpointOutcomeCounts["setup"].Values.Sum().Should().Be(1);
    }

    #region Phase 3: Additional Coverage Tests

    [Fact]
    public async Task RecordFeedbackAsync_WithNullMessageId_ThrowsArgumentException()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await FluentActions.Invoking(async () => await service.RecordFeedbackAsync(null!, "qa", "user", "helpful", "game"))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecordFeedbackAsync_WithEmptyEndpoint_ThrowsArgumentException()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await FluentActions.Invoking(async () => await service.RecordFeedbackAsync("msg", null!, "user", "helpful", "game"))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RecordFeedbackAsync_WithNullUserId_ThrowsArgumentException()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await FluentActions.Invoking(async () => await service.RecordFeedbackAsync("msg", "qa", null!, "helpful", "game"))
            .Should().ThrowAsync<ArgumentException>();
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
        // Updated entity assertions
        updated.Outcome.Should().Be("not-helpful");
        updated.Endpoint.Should().Be("explain");
        updated.GameId.Should().Be("game-2");
        updated.UpdatedAt.Should().BeAfter(updated.CreatedAt);
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

        stats.TotalFeedback.Should().Be(2);
        stats.OutcomeCounts["helpful"].Should().Be(1);
        stats.OutcomeCounts["not-helpful"].Should().Be(1);
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

        stats.TotalFeedback.Should().Be(1);
        stats.OutcomeCounts["helpful"].Should().Be(1);
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

        stats.TotalFeedback.Should().Be(1);
        stats.OutcomeCounts["helpful"].Should().Be(1);
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

        stats.TotalFeedback.Should().Be(1); // Only msg-2 in range
        stats.OutcomeCounts["helpful"].Should().Be(1);
    }

    [Fact]
    public async Task GetStatsAsync_WithNoFeedback_ReturnsZeroCounts()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        var stats = await service.GetStatsAsync();

        stats.TotalFeedback.Should().Be(0);
        stats.OutcomeCounts.Should().BeEmpty();
        stats.EndpointOutcomeCounts.Should().BeEmpty();
    }

    [Fact]
    public async Task RecordFeedbackAsync_WithNullGameId_StoresSuccessfully()
    {
        await using var dbContext = CreateInMemoryContext();
        var loggerMock = new Mock<ILogger<AgentFeedbackService>>();
        var service = new AgentFeedbackService(dbContext, loggerMock.Object);

        await service.RecordFeedbackAsync("msg-1", "qa", "user-1", "helpful", null);

        var entry = await dbContext.AgentFeedbacks.SingleAsync();
        entry.GameId.Should().BeNull();
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
        stats.OutcomeCounts["helpful"].Should().Be(2);
        stats.OutcomeCounts["not_helpful"].Should().Be(1);
        stats.TotalFeedback.Should().Be(3);
    }

    #endregion
}