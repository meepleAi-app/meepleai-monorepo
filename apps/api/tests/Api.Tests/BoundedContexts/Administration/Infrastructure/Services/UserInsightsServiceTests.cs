using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Infrastructure.Services;

/// <summary>
/// Unit tests for UserInsightsService (Issue #4308).
/// Tests parallel analyzer execution and result aggregation.
/// </summary>
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
        Assert.Equal(3, result.Count);
        Assert.Equal(9, result[0].Priority); // Streak (highest)
        Assert.Equal(7, result[1].Priority); // Rules
        Assert.Equal(5, result[2].Priority); // Backlog (lowest)
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
        Assert.Empty(result);
    }

    [Fact]
    public async Task GenerateInsightsAsync_LimitsResultsToMaxCount()
    {
        // Arrange
        var userId = Guid.NewGuid();
        // Create 15 insights with priorities cycling in 1-10 range (valid range)
        var manyInsights = Enumerable.Range(1, 15)
            .Select(i => AIInsight.Create(InsightType.Recommendation, $"Game {i}", "desc", "action", "/url", ((i - 1) % 10) + 1))
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
        Assert.Equal(10, result.Count); // MaxInsightsToReturn = 10
        Assert.Equal(10, result[0].Priority); // Highest priority first (max valid priority is 10)
    }
}
