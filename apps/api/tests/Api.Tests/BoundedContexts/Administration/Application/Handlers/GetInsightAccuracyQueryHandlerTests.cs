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
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Unit tests for GetInsightAccuracyQueryHandler.
/// Issue #4124: AI Insights Runtime Validation (Performance + Accuracy).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
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
        result.TotalFeedback.Should().Be(0);
        result.RelevantCount.Should().Be(0);
        result.AccuracyPercentage.Should().Be(0);
        result.ByType.Should().BeEmpty();
        result.CalculatedAt.Should().Be(_timeProvider.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task Handle_AllRelevant_Returns100Percent()
    {
        // Arrange
        await SeedFeedbackAsync("Backlog", isRelevant: true, count: 5);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert
        result.TotalFeedback.Should().Be(5);
        result.RelevantCount.Should().Be(5);
        result.AccuracyPercentage.Should().Be(100.0);
    }

    [Fact]
    public async Task Handle_NoneRelevant_Returns0Percent()
    {
        // Arrange
        await SeedFeedbackAsync("Backlog", isRelevant: false, count: 4);

        // Act
        var result = await _handler.Handle(new GetInsightAccuracyQuery(), CancellationToken.None);

        // Assert
        result.TotalFeedback.Should().Be(4);
        result.RelevantCount.Should().Be(0);
        result.AccuracyPercentage.Should().Be(0);
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
        result.TotalFeedback.Should().Be(4);
        result.RelevantCount.Should().Be(3);
        result.AccuracyPercentage.Should().Be(75.0);
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
        result.TotalFeedback.Should().Be(7);
        result.RelevantCount.Should().Be(5);
        result.ByType.Count.Should().Be(3);

        // Ordered by total descending
        var backlog = result.ByType.First(t => t.Type == "Backlog");
        backlog.Total.Should().Be(4);
        backlog.Relevant.Should().Be(3);
        backlog.AccuracyPercentage.Should().Be(75.0);

        var streak = result.ByType.First(t => t.Type == "Streak");
        streak.Total.Should().Be(2);
        streak.Relevant.Should().Be(2);
        streak.AccuracyPercentage.Should().Be(100.0);

        var recommendation = result.ByType.First(t => t.Type == "Recommendation");
        recommendation.Total.Should().Be(1);
        recommendation.Relevant.Should().Be(0);
        recommendation.AccuracyPercentage.Should().Be(0);
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
        result.ByType.Count.Should().Be(3);
        result.ByType[0].Type.Should().Be("Backlog");
        result.ByType[1].Type.Should().Be("Streak");
        result.ByType[2].Type.Should().Be("Achievement");
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
        result.AccuracyPercentage.Should().Be(33.3);
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
        result.CalculatedAt.Should().Be(specificTime.UtcDateTime);
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
        result.ByType.Should().ContainSingle();
        result.ByType[0].Type.Should().Be("Backlog");
        result.ByType[0].Total.Should().Be(3);
        result.TotalFeedback.Should().Be(3);
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
