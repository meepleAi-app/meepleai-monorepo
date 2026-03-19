using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Administration;
using Api.SharedKernel.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Unit tests for GetInsightAccuracyQueryHandler.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// </summary>
public sealed class GetInsightAccuracyQueryHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly FakeTimeProvider _timeProvider;
    private readonly GetInsightAccuracyQueryHandler _handler;

    public GetInsightAccuracyQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"InsightAccuracy_{Guid.NewGuid()}")
            .Options;

        _dbContext = new MeepleAiDbContext(options, new Mock<IMediator>().Object, new Mock<IDomainEventCollector>().Object);
        _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 2, 16, 12, 0, 0, TimeSpan.Zero));
        _handler = new GetInsightAccuracyQueryHandler(_dbContext, _timeProvider);
    }

    [Fact]
    public async Task Handle_NoFeedback_ReturnsZeroAccuracy()
    {
        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(0, result.TotalFeedback);
        Assert.Equal(0, result.RelevantCount);
        Assert.Equal(0, result.AccuracyPercentage);
        Assert.Empty(result.ByType);
        Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, result.CalculatedAt);
    }

    [Fact]
    public async Task Handle_AllRelevant_Returns100Percent()
    {
        // Arrange
        await SeedFeedbackAsync("Backlog", isRelevant: true, count: 5);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(5, result.TotalFeedback);
        Assert.Equal(5, result.RelevantCount);
        Assert.Equal(100.0, result.AccuracyPercentage);
    }

    [Fact]
    public async Task Handle_NoneRelevant_Returns0Percent()
    {
        // Arrange
        await SeedFeedbackAsync("Backlog", isRelevant: false, count: 4);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(4, result.TotalFeedback);
        Assert.Equal(0, result.RelevantCount);
        Assert.Equal(0, result.AccuracyPercentage);
    }

    [Fact]
    public async Task Handle_MixedFeedback_CalculatesCorrectPercentage()
    {
        // Arrange: 3 relevant out of 4 = 75%
        await SeedFeedbackAsync("Backlog", isRelevant: true, count: 3);
        await SeedFeedbackAsync("Backlog", isRelevant: false, count: 1);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(4, result.TotalFeedback);
        Assert.Equal(3, result.RelevantCount);
        Assert.Equal(75.0, result.AccuracyPercentage);
    }

    [Fact]
    public async Task Handle_MultipleTypes_ReturnsBreakdownByType()
    {
        // Arrange
        await SeedFeedbackAsync("Backlog", isRelevant: true, count: 3);
        await SeedFeedbackAsync("Backlog", isRelevant: false, count: 1);
        await SeedFeedbackAsync("Streak", isRelevant: true, count: 2);
        await SeedFeedbackAsync("Recommendation", isRelevant: false, count: 1);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(7, result.TotalFeedback);
        Assert.Equal(5, result.RelevantCount);
        Assert.Equal(3, result.ByType.Count);

        // Ordered by total descending
        var backlog = result.ByType.First(t => t.Type == "Backlog");
        Assert.Equal(4, backlog.Total);
        Assert.Equal(3, backlog.Relevant);
        Assert.Equal(75.0, backlog.AccuracyPercentage);

        var streak = result.ByType.First(t => t.Type == "Streak");
        Assert.Equal(2, streak.Total);
        Assert.Equal(2, streak.Relevant);
        Assert.Equal(100.0, streak.AccuracyPercentage);

        var recommendation = result.ByType.First(t => t.Type == "Recommendation");
        Assert.Equal(1, recommendation.Total);
        Assert.Equal(0, recommendation.Relevant);
        Assert.Equal(0, recommendation.AccuracyPercentage);
    }

    [Fact]
    public async Task Handle_ByType_OrderedByTotalDescending()
    {
        // Arrange: Different counts per type
        await SeedFeedbackAsync("Achievement", isRelevant: true, count: 1);
        await SeedFeedbackAsync("Backlog", isRelevant: true, count: 5);
        await SeedFeedbackAsync("Streak", isRelevant: true, count: 3);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert: Ordered by total count descending
        Assert.Equal(3, result.ByType.Count);
        Assert.Equal("Backlog", result.ByType[0].Type);
        Assert.Equal("Streak", result.ByType[1].Type);
        Assert.Equal("Achievement", result.ByType[2].Type);
    }

    [Fact]
    public async Task Handle_AccuracyRoundedToOneDecimal()
    {
        // Arrange: 1 relevant out of 3 = 33.333...%
        await SeedFeedbackAsync("Backlog", isRelevant: true, count: 1);
        await SeedFeedbackAsync("Backlog", isRelevant: false, count: 2);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert: Rounded to 1 decimal
        Assert.Equal(33.3, result.AccuracyPercentage);
    }

    [Fact]
    public async Task Handle_UsesTimeProviderForCalculatedAt()
    {
        // Arrange
        var specificTime = new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.Zero);
        _timeProvider.SetUtcNow(specificTime);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert
        Assert.Equal(specificTime.UtcDateTime, result.CalculatedAt);
    }

    [Fact]
    public async Task Handle_ConsistentCasing_GroupsCorrectly()
    {
        // Arrange: Same type, consistent casing (as enforced by validator)
        await SeedFeedbackAsync("Backlog", isRelevant: true, count: 2);
        await SeedFeedbackAsync("Backlog", isRelevant: false, count: 1);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert: Single group for consistent casing
        Assert.Single(result.ByType);
        Assert.Equal("Backlog", result.ByType[0].Type);
        Assert.Equal(3, result.ByType[0].Total);
        Assert.Equal(3, result.TotalFeedback);
    }

    private async Task SeedFeedbackAsync(string insightType, bool isRelevant, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _dbContext.Set<InsightFeedbackEntity>().Add(new InsightFeedbackEntity
            {
                Id = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                InsightId = $"{insightType.ToLowerInvariant()}-{Guid.NewGuid():N}",
                InsightType = insightType,
                IsRelevant = isRelevant,
                SubmittedAt = _timeProvider.GetUtcNow().UtcDateTime
            });
        }
        await _dbContext.SaveChangesAsync();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }
}
