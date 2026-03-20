using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Api.Tests.Constants;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Unit tests for UserInsightsService (Issue #4308).
/// Tests parallel analyzer execution and result aggregation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Administration")]
public sealed class UserInsightsServiceTests
{
    private readonly Mock<IBacklogAnalyzer> _mockBacklogAnalyzer;
    private readonly Mock<IRulesAnalyzer> _mockRulesAnalyzer;
    private readonly Mock<IRAGRecommender> _mockRagRecommender;
    private readonly Mock<IStreakAnalyzer> _mockStreakAnalyzer;
    private readonly Mock<ILogger<UserInsightsService>> _mockLogger;
    private readonly UserInsightsService _sut;

    public UserInsightsServiceTests()
    {
        _mockBacklogAnalyzer = new Mock<IBacklogAnalyzer>();
        _mockRulesAnalyzer = new Mock<IRulesAnalyzer>();
        _mockRagRecommender = new Mock<IRAGRecommender>();
        _mockStreakAnalyzer = new Mock<IStreakAnalyzer>();
        _mockLogger = new Mock<ILogger<UserInsightsService>>();

        _sut = new UserInsightsService(
            _mockBacklogAnalyzer.Object,
            _mockRulesAnalyzer.Object,
            _mockRagRecommender.Object,
            _mockStreakAnalyzer.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task GenerateInsightsAsync_WhenAllAnalyzersReturnResults_AggregatesAndSortsByPriority()
    {
        // Arrange
        var userId = Guid.NewGuid();

        var backlogInsight = AIInsight.Create(InsightType.BacklogAlert, "Backlog", "desc", "action", "/url", 5);
        var rulesInsight = AIInsight.Create(InsightType.RulesReminder, "Rules", "desc", "action", "/url", 7);
        var streakInsight = AIInsight.Create(InsightType.StreakNudge, "Streak", "desc", "action", "/url", 9);

        _mockBacklogAnalyzer.Setup(x => x.AnalyzeBacklogAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight> { backlogInsight });
        _mockRulesAnalyzer.Setup(x => x.AnalyzeRulebooksAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight> { rulesInsight });
        _mockRagRecommender.Setup(x => x.RecommendSimilarGamesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());
        _mockStreakAnalyzer.Setup(x => x.AnalyzeStreakAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight> { streakInsight });

        // Act
        var result = await _sut.GenerateInsightsAsync(userId, CancellationToken.None);

        // Assert
        result.Count.Should().Be(3);
        result[0].Priority.Should().Be(9); // Streak (highest)
        result[1].Priority.Should().Be(7); // Rules
        result[2].Priority.Should().Be(5); // Backlog (lowest)
    }

    [Fact]
    public async Task GenerateInsightsAsync_WhenNoInsightsGenerated_ReturnsEmptyList()
    {
        // Arrange
        var userId = Guid.NewGuid();

        _mockBacklogAnalyzer.Setup(x => x.AnalyzeBacklogAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());
        _mockRulesAnalyzer.Setup(x => x.AnalyzeRulebooksAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());
        _mockRagRecommender.Setup(x => x.RecommendSimilarGamesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());
        _mockStreakAnalyzer.Setup(x => x.AnalyzeStreakAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());

        // Act
        var result = await _sut.GenerateInsightsAsync(userId, CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateInsightsAsync_LimitsResultsToMaxCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var manyInsights = Enumerable.Range(1, 15)
            .Select(i => AIInsight.Create(InsightType.Recommendation, $"Game {i}", "desc", "action", "/url", i))
            .ToList();

        _mockBacklogAnalyzer.Setup(x => x.AnalyzeBacklogAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyInsights);
        _mockRulesAnalyzer.Setup(x => x.AnalyzeRulebooksAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());
        _mockRagRecommender.Setup(x => x.RecommendSimilarGamesAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());
        _mockStreakAnalyzer.Setup(x => x.AnalyzeStreakAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AIInsight>());

        // Act
        var result = await _sut.GenerateInsightsAsync(userId, CancellationToken.None);

        // Assert
        result.Count.Should().Be(10); // MaxInsightsToReturn = 10
        result[0].Priority.Should().Be(15); // Highest priority first
    }
}
