using System.Threading;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.TestHelpers;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Tests for GetFeedbackStatsQueryHandler.
/// Tests agent feedback statistics aggregation for analytics dashboard.
/// ISSUE-1695: CQRS migration for AgentFeedbackService
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GetFeedbackStatsQueryHandlerTests
{
    private readonly Mock<ILogger<GetFeedbackStatsQueryHandler>> _mockLogger;

    public GetFeedbackStatsQueryHandlerTests()
    {
        _mockLogger = new Mock<ILogger<GetFeedbackStatsQueryHandler>>();
    }

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    /// <summary>
    /// Creates a fresh DbContext for each test to ensure complete isolation
    /// </summary>
    private static MeepleAiDbContext CreateFreshDbContext()
    {
        return TestDbContextFactory.CreateInMemoryDbContext();
    }
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange - fresh context per test
        using var context = CreateFreshDbContext();

        // Act
        var handler = new GetFeedbackStatsQueryHandler(context, _mockLogger.Object);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullDbContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () =>
            new GetFeedbackStatsQueryHandler(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        using var context = CreateFreshDbContext();

        // Act & Assert
        var act = () =>
            new GetFeedbackStatsQueryHandler(context, null!);
        act.Should().Throw<ArgumentNullException>();
    }
    [Fact]
    public void Query_WithDefaultParameters_ConstructsCorrectly()
    {
        // Act
        var query = new GetFeedbackStatsQuery();

        // Assert
        query.StartDate.Should().BeNull();
        query.EndDate.Should().BeNull();
        query.Endpoint.Should().BeNull();
    }

    [Fact]
    public void Query_WithDateRange_ConstructsCorrectly()
    {
        // Arrange
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        // Act
        var query = new GetFeedbackStatsQuery(startDate, endDate);

        // Assert
        query.StartDate.Should().Be(startDate);
        query.EndDate.Should().Be(endDate);
    }

    [Fact]
    public void Query_WithEndpointFilter_ConstructsCorrectly()
    {
        // Act
        var query = new GetFeedbackStatsQuery(Endpoint: "/api/v1/chat");

        // Assert
        query.Endpoint.Should().Be("/api/v1/chat");
    }
    [Fact]
    public async Task Handle_WithNoFeedback_ReturnsEmptyStats()
    {
        // Arrange - fresh context with no data
        using var context = CreateFreshDbContext();
        var handler = new GetFeedbackStatsQueryHandler(context, _mockLogger.Object);
        var query = new GetFeedbackStatsQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalFeedbacks.Should().Be(0);
        result.HelpfulCount.Should().Be(0);
        result.NotHelpfulCount.Should().Be(0);
        result.HelpfulRate.Should().Be(0.0);
        result.FeedbackByEndpoint.Should().BeEmpty();
        result.FeedbackByOutcome.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithMixedFeedback_ReturnsCorrectAggregations()
    {
        // Arrange - fresh context with test data
        using var context = CreateFreshDbContext();
        var userId = Guid.NewGuid();
        var messageId1 = Guid.NewGuid();
        var messageId2 = Guid.NewGuid();
        var messageId3 = Guid.NewGuid();

        // Add 2 helpful, 1 not-helpful
        context.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = messageId1,
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = messageId2,
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = messageId3,
                Endpoint = "/api/v1/search",
                UserId = userId,
                Outcome = "not-helpful",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync(TestCancellationToken);

        var handler = new GetFeedbackStatsQueryHandler(context, _mockLogger.Object);
        var query = new GetFeedbackStatsQuery();

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.TotalFeedbacks.Should().Be(3);
        result.HelpfulCount.Should().Be(2);
        result.NotHelpfulCount.Should().Be(1);
        result.HelpfulRate.Should().BeApproximately(2.0 / 3.0, 0.01);
        result.FeedbackByEndpoint.Count.Should().Be(2);
        result.FeedbackByEndpoint["/api/v1/chat"].Should().Be(2);
        result.FeedbackByEndpoint["/api/v1/search"].Should().Be(1);
        result.FeedbackByOutcome.Count.Should().Be(2);
        result.FeedbackByOutcome["helpful"].Should().Be(2);
        result.FeedbackByOutcome["not-helpful"].Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithDateRangeFilter_ReturnsFilteredStats()
    {
        // Arrange - fresh context with test data
        using var context = CreateFreshDbContext();
        var userId = Guid.NewGuid();
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        // Add feedback inside range (2) and outside range (1)
        context.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc), // Inside range
                UpdatedAt = DateTime.UtcNow
            },
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = new DateTime(2025, 1, 20, 12, 0, 0, DateTimeKind.Utc), // Inside range
                UpdatedAt = DateTime.UtcNow
            },
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "not-helpful",
                CreatedAt = new DateTime(2024, 12, 15, 12, 0, 0, DateTimeKind.Utc), // Outside range
                UpdatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync(TestCancellationToken);

        var handler = new GetFeedbackStatsQueryHandler(context, _mockLogger.Object);
        var query = new GetFeedbackStatsQuery(startDate, endDate);

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert - Should only include 2 feedbacks within date range
        result.Should().NotBeNull();
        result.TotalFeedbacks.Should().Be(2);
        result.HelpfulCount.Should().Be(2);
        result.NotHelpfulCount.Should().Be(0);
        result.HelpfulRate.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_WithEndpointFilter_ReturnsFilteredStats()
    {
        // Arrange - fresh context with test data
        using var context = CreateFreshDbContext();
        var userId = Guid.NewGuid();

        // Add feedback for 2 different endpoints
        context.AgentFeedbacks.AddRange(
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "not-helpful",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/search",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync(TestCancellationToken);

        var handler = new GetFeedbackStatsQueryHandler(context, _mockLogger.Object);
        var query = new GetFeedbackStatsQuery(Endpoint: "/api/v1/chat");

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert - Should only include /api/v1/chat feedback
        result.Should().NotBeNull();
        result.TotalFeedbacks.Should().Be(2);
        result.HelpfulCount.Should().Be(1);
        result.NotHelpfulCount.Should().Be(1);
        result.HelpfulRate.Should().Be(0.5);
        result.FeedbackByEndpoint.Should().ContainSingle();
        result.FeedbackByEndpoint["/api/v1/chat"].Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithAllFilters_ReturnsCorrectlyFilteredStats()
    {
        // Arrange - fresh context with test data
        using var context = CreateFreshDbContext();
        var userId = Guid.NewGuid();
        var startDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = new DateTime(2025, 1, 31, 23, 59, 59, DateTimeKind.Utc);

        // Add various feedback entries
        context.AgentFeedbacks.AddRange(
            // Matches all filters
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAt = DateTime.UtcNow
            },
            // Wrong endpoint
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/search",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = new DateTime(2025, 1, 15, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAt = DateTime.UtcNow
            },
            // Wrong date
            new AgentFeedbackEntity
            {
                Id = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Endpoint = "/api/v1/chat",
                UserId = userId,
                Outcome = "helpful",
                CreatedAt = new DateTime(2024, 12, 15, 12, 0, 0, DateTimeKind.Utc),
                UpdatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync(TestCancellationToken);

        var handler = new GetFeedbackStatsQueryHandler(context, _mockLogger.Object);
        var query = new GetFeedbackStatsQuery(startDate, endDate, "/api/v1/chat");

        // Act
        var result = await handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert - Should only include 1 feedback matching all filters
        result.Should().NotBeNull();
        result.TotalFeedbacks.Should().Be(1);
        result.HelpfulCount.Should().Be(1);
        result.NotHelpfulCount.Should().Be(0);
        result.HelpfulRate.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_WithCancellationToken_UsesCancellationToken()
    {
        // Arrange - fresh context
        using var context = CreateFreshDbContext();
        var handler = new GetFeedbackStatsQueryHandler(context, _mockLogger.Object);
        var query = new GetFeedbackStatsQuery();
        using var cts = new CancellationTokenSource();

        // Act & Assert - Should not throw with valid cancellation token
        var result = await handler.Handle(query, cts.Token);
        result.Should().NotBeNull();
    }
}
