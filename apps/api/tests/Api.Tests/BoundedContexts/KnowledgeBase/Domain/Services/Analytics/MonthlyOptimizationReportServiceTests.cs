using Api.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services.Analytics;

/// <summary>
/// Tests for the MonthlyOptimizationReportService.
/// Issue #3025: Backend 90% Coverage Target - Phase 16
/// </summary>
[Trait("Category", "Unit")]
public sealed class MonthlyOptimizationReportServiceTests
{
    private readonly Mock<IQueryEfficiencyAnalyzer> _efficiencyAnalyzerMock;
    private readonly Mock<IModelRecommendationService> _recommendationServiceMock;
    private readonly Mock<ICacheCorrelationAnalyzer> _cacheAnalyzerMock;
    private readonly Mock<ILogger<MonthlyOptimizationReportService>> _loggerMock;
    private readonly MonthlyOptimizationReportService _service;

    public MonthlyOptimizationReportServiceTests()
    {
        _efficiencyAnalyzerMock = new Mock<IQueryEfficiencyAnalyzer>();
        _recommendationServiceMock = new Mock<IModelRecommendationService>();
        _cacheAnalyzerMock = new Mock<ICacheCorrelationAnalyzer>();
        _loggerMock = new Mock<ILogger<MonthlyOptimizationReportService>>();
        _service = new MonthlyOptimizationReportService(
            _efficiencyAnalyzerMock.Object,
            _recommendationServiceMock.Object,
            _cacheAnalyzerMock.Object,
            _loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullEfficiencyAnalyzer_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new MonthlyOptimizationReportService(
            null!,
            _recommendationServiceMock.Object,
            _cacheAnalyzerMock.Object,
            _loggerMock.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("efficiencyAnalyzer");
    }

    [Fact]
    public void Constructor_WithNullRecommendationService_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new MonthlyOptimizationReportService(
            _efficiencyAnalyzerMock.Object,
            null!,
            _cacheAnalyzerMock.Object,
            _loggerMock.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("recommendationService");
    }

    [Fact]
    public void Constructor_WithNullCacheAnalyzer_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new MonthlyOptimizationReportService(
            _efficiencyAnalyzerMock.Object,
            _recommendationServiceMock.Object,
            null!,
            _loggerMock.Object);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("cacheAnalyzer");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new MonthlyOptimizationReportService(
            _efficiencyAnalyzerMock.Object,
            _recommendationServiceMock.Object,
            _cacheAnalyzerMock.Object,
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    #endregion

    #region GenerateReportAsync Tests - Validation

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(13)]
    [InlineData(100)]
    public async Task GenerateReportAsync_WithInvalidMonth_ThrowsArgumentException(int month)
    {
        // Act
        var action = () => _service.GenerateReportAsync(2024, month);

        // Assert
        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Month must be between 1 and 12*");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public async Task GenerateReportAsync_WithValidMonth_DoesNotThrow(int month)
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var result = await _service.GenerateReportAsync(2024, month);

        // Assert
        result.Should().NotBeNull();
        result.Month.Should().Be(month);
    }

    #endregion

    #region GenerateReportAsync Tests - Report Generation

    [Fact]
    public async Task GenerateReportAsync_ReturnsReportWithCorrectYearAndMonth()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.Year.Should().Be(2024);
        result.Month.Should().Be(6);
    }

    [Fact]
    public async Task GenerateReportAsync_CallsEfficiencyAnalyzer()
    {
        // Arrange
        var expectedStartDate = new DateOnly(2024, 6, 1);
        var expectedEndDate = new DateOnly(2024, 6, 30);
        SetupDefaultMocks();

        // Act
        await _service.GenerateReportAsync(2024, 6);

        // Assert
        _efficiencyAnalyzerMock.Verify(
            a => a.AnalyzeEfficiencyAsync(
                expectedStartDate,
                expectedEndDate,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateReportAsync_CallsCacheAnalyzer()
    {
        // Arrange
        var expectedStartDate = new DateOnly(2024, 6, 1);
        var expectedEndDate = new DateOnly(2024, 6, 30);
        SetupDefaultMocks();

        // Act
        await _service.GenerateReportAsync(2024, 6);

        // Assert
        _cacheAnalyzerMock.Verify(
            a => a.AnalyzeCacheEffectivenessAsync(
                expectedStartDate,
                expectedEndDate,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateReportAsync_CallsModelCompareModels()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        await _service.GenerateReportAsync(2024, 6);

        // Assert
        _recommendationServiceMock.Verify(
            s => s.CompareModelsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateReportAsync_CallsGetRecommendation()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        await _service.GenerateReportAsync(2024, 6);

        // Assert
        _recommendationServiceMock.Verify(
            s => s.GetRecommendationAsync("qa", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateReportAsync_IncludesEfficiencyAnalysis()
    {
        // Arrange
        var efficiencyReport = CreateEfficiencyReport(totalCost: 500m);
        SetupDefaultMocks(efficiencyReport: efficiencyReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.EfficiencyAnalysis.Should().Be(efficiencyReport);
    }

    [Fact]
    public async Task GenerateReportAsync_IncludesCacheAnalysis()
    {
        // Arrange
        var cacheReport = CreateCacheReport(estimatedSavings: 75m);
        SetupDefaultMocks(cacheReport: cacheReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.CacheAnalysis.Should().Be(cacheReport);
    }

    [Fact]
    public async Task GenerateReportAsync_IncludesModelComparisons()
    {
        // Arrange
        var comparisons = new List<ModelComparison>
        {
            CreateModelComparison("model-1"),
            CreateModelComparison("model-2")
        };
        SetupDefaultMocks(modelComparisons: comparisons);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.ModelComparisons.Should().BeEquivalentTo(comparisons);
    }

    [Fact]
    public async Task GenerateReportAsync_IncludesRecommendedModel()
    {
        // Arrange
        var recommendation = CreateRecommendation("openai/gpt-4o-mini");
        SetupDefaultMocks(recommendation: recommendation);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.RecommendedModel.Should().Be(recommendation);
    }

    #endregion

    #region GenerateReportAsync Tests - Savings Calculation

    [Fact]
    public async Task GenerateReportAsync_CalculatesTotalSavings()
    {
        // Arrange - Cache savings + 20% of total cost
        var cacheReport = CreateCacheReport(estimatedSavings: 100m);
        var efficiencyReport = CreateEfficiencyReport(totalCost: 500m);
        // Expected: 100 (cache) + 500 * 0.20 (model optimization) = 200
        var expectedTotalSavings = 100m + (500m * 0.20m);

        SetupDefaultMocks(efficiencyReport: efficiencyReport, cacheReport: cacheReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.TotalSavingsOpportunity.Should().Be(expectedTotalSavings);
    }

    [Fact]
    public async Task GenerateReportAsync_ModelSwitchSavingsIs20Percent()
    {
        // Arrange
        var efficiencyReport = CreateEfficiencyReport(totalCost: 1000m);
        var cacheReport = CreateCacheReport(estimatedSavings: 0m); // Zero cache savings
        SetupDefaultMocks(efficiencyReport: efficiencyReport, cacheReport: cacheReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert - Only model switch savings
        result.TotalSavingsOpportunity.Should().Be(200m); // 1000 * 0.20
    }

    #endregion

    #region GenerateReportAsync Tests - Executive Summary

    [Fact]
    public async Task GenerateReportAsync_ExecutiveSummaryIncludesMonthlyCosts()
    {
        // Arrange
        var efficiencyReport = CreateEfficiencyReport(totalCost: 500m, totalQueries: 1000, totalTokens: 500000);
        SetupDefaultMocks(efficiencyReport: efficiencyReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert - Use culture-invariant check (contains "500" and "1000 queries")
        result.ExecutiveSummary.Should().Contain(s => s.Contains("500") && s.Contains("1000 queries"));
    }

    [Fact]
    public async Task GenerateReportAsync_ExecutiveSummaryIncludesCachePerformance()
    {
        // Arrange
        var cacheReport = CreateCacheReport(hitRate: 0.75, estimatedSavings: 150m);
        SetupDefaultMocks(cacheReport: cacheReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert - Use culture-invariant check (contains "75%" and "150")
        result.ExecutiveSummary.Should().Contain(s => s.Contains("75%") && s.Contains("150"));
    }

    [Fact]
    public async Task GenerateReportAsync_ExecutiveSummaryIncludesRecommendedModel()
    {
        // Arrange
        var recommendation = CreateRecommendation("openai/gpt-4o-mini", qualityTier: "Budget");
        SetupDefaultMocks(recommendation: recommendation);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.ExecutiveSummary.Should().Contain(s =>
            s.Contains("openai/gpt-4o-mini") && s.Contains("Budget"));
    }

    [Fact]
    public async Task GenerateReportAsync_ExecutiveSummaryIncludesTotalSavingsOpportunity()
    {
        // Arrange
        var cacheReport = CreateCacheReport(estimatedSavings: 100m);
        var efficiencyReport = CreateEfficiencyReport(totalCost: 500m);
        SetupDefaultMocks(efficiencyReport: efficiencyReport, cacheReport: cacheReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert - Use culture-invariant check (contains "200" and "potential")
        result.ExecutiveSummary.Should().Contain(s => s.Contains("200") && s.Contains("potential"));
    }

    [Fact]
    public async Task GenerateReportAsync_ExecutiveSummaryIncludesTopEfficiencyRecommendation()
    {
        // Arrange
        var efficiencyReport = CreateEfficiencyReport(
            optimizationRecommendations: ["Batch similar queries", "Use caching"]);
        SetupDefaultMocks(efficiencyReport: efficiencyReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.ExecutiveSummary.Should().Contain(s => s.Contains("Batch similar queries"));
    }

    [Fact]
    public async Task GenerateReportAsync_ExecutiveSummaryIncludesTopCacheRecommendation()
    {
        // Arrange
        var cacheReport = CreateCacheReport(recommendations: ["Increase cache TTL", "Add more cache keys"]);
        SetupDefaultMocks(cacheReport: cacheReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.ExecutiveSummary.Should().Contain(s => s.Contains("Increase cache TTL"));
    }

    [Fact]
    public async Task GenerateReportAsync_WithNoRecommendations_DoesNotThrow()
    {
        // Arrange
        var efficiencyReport = CreateEfficiencyReport(optimizationRecommendations: []);
        var cacheReport = CreateCacheReport(recommendations: []);
        SetupDefaultMocks(efficiencyReport: efficiencyReport, cacheReport: cacheReport);

        // Act
        var result = await _service.GenerateReportAsync(2024, 6);

        // Assert
        result.ExecutiveSummary.Should().NotBeEmpty();
    }

    #endregion

    #region GenerateReportAsync Tests - Date Range

    [Fact]
    public async Task GenerateReportAsync_January_CalculatesCorrectDateRange()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        await _service.GenerateReportAsync(2024, 1);

        // Assert
        _efficiencyAnalyzerMock.Verify(
            a => a.AnalyzeEfficiencyAsync(
                new DateOnly(2024, 1, 1),
                new DateOnly(2024, 1, 31),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateReportAsync_February_LeapYear_CalculatesCorrectDateRange()
    {
        // Arrange - 2024 is a leap year
        SetupDefaultMocks();

        // Act
        await _service.GenerateReportAsync(2024, 2);

        // Assert
        _efficiencyAnalyzerMock.Verify(
            a => a.AnalyzeEfficiencyAsync(
                new DateOnly(2024, 2, 1),
                new DateOnly(2024, 2, 29),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GenerateReportAsync_December_CalculatesCorrectDateRange()
    {
        // Arrange
        SetupDefaultMocks();

        // Act
        await _service.GenerateReportAsync(2024, 12);

        // Assert
        _efficiencyAnalyzerMock.Verify(
            a => a.AnalyzeEfficiencyAsync(
                new DateOnly(2024, 12, 1),
                new DateOnly(2024, 12, 31),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task GenerateReportAsync_PassesCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;
        SetupDefaultMocks();

        // Act
        await _service.GenerateReportAsync(2024, 6, token);

        // Assert
        _efficiencyAnalyzerMock.Verify(
            a => a.AnalyzeEfficiencyAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), token),
            Times.Once);
        _cacheAnalyzerMock.Verify(
            a => a.AnalyzeCacheEffectivenessAsync(It.IsAny<DateOnly>(), It.IsAny<DateOnly>(), token),
            Times.Once);
        _recommendationServiceMock.Verify(
            s => s.CompareModelsAsync(token),
            Times.Once);
        _recommendationServiceMock.Verify(
            s => s.GetRecommendationAsync(It.IsAny<string>(), It.IsAny<bool>(), token),
            Times.Once);
    }

    #endregion

    #region Helper Methods

    private void SetupDefaultMocks(
        QueryEfficiencyReport? efficiencyReport = null,
        CacheCorrelationReport? cacheReport = null,
        List<ModelComparison>? modelComparisons = null,
        ModelRecommendation? recommendation = null)
    {
        _efficiencyAnalyzerMock
            .Setup(a => a.AnalyzeEfficiencyAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(efficiencyReport ?? CreateEfficiencyReport());

        _cacheAnalyzerMock
            .Setup(a => a.AnalyzeCacheEffectivenessAsync(
                It.IsAny<DateOnly>(),
                It.IsAny<DateOnly>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(cacheReport ?? CreateCacheReport());

        _recommendationServiceMock
            .Setup(s => s.CompareModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(modelComparisons ?? [CreateModelComparison("default-model")]);

        _recommendationServiceMock
            .Setup(s => s.GetRecommendationAsync(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(recommendation ?? CreateRecommendation("openai/gpt-4o-mini"));
    }

    private static QueryEfficiencyReport CreateEfficiencyReport(
        decimal totalCost = 100m,
        int totalQueries = 100,
        int totalTokens = 100000,
        List<string>? optimizationRecommendations = null)
    {
        return new QueryEfficiencyReport
        {
            StartDate = new DateOnly(2024, 6, 1),
            EndDate = new DateOnly(2024, 6, 30),
            TotalQueries = totalQueries,
            TotalCost = totalCost,
            TotalTokens = totalTokens,
            AverageTokensPerQuery = totalTokens / (double)totalQueries,
            AverageCostPerQuery = totalCost / totalQueries,
            TopCostlyQueries = [],
            AverageTokensByOperation = new Dictionary<string, double>(),
            OptimizationRecommendations = optimizationRecommendations ?? ["Default optimization recommendation"]
        };
    }

    private static CacheCorrelationReport CreateCacheReport(
        double hitRate = 0.5,
        decimal estimatedSavings = 50m,
        List<string>? recommendations = null)
    {
        return new CacheCorrelationReport
        {
            StartDate = new DateOnly(2024, 6, 1),
            EndDate = new DateOnly(2024, 6, 30),
            TotalRequests = 1000,
            CacheHits = (int)(1000 * hitRate),
            CacheMisses = (int)(1000 * (1 - hitRate)),
            HitRate = hitRate,
            EstimatedSavingsUsd = estimatedSavings,
            CostWithoutCache = 200m,
            ActualCost = 150m,
            CacheEfficiencyScore = 0.8,
            Recommendations = recommendations ?? ["Default cache recommendation"]
        };
    }

    private static ModelComparison CreateModelComparison(string modelId)
    {
        return new ModelComparison
        {
            ModelId = modelId,
            Provider = "OpenRouter",
            InputCostPer1M = 0.15m,
            OutputCostPer1M = 0.60m,
            QualityTier = "Budget",
            CostQualityRatio = 0.214
        };
    }

    private static ModelRecommendation CreateRecommendation(string model, string qualityTier = "Standard")
    {
        return new ModelRecommendation
        {
            RecommendedModel = model,
            Provider = "OpenRouter",
            EstimatedCostPer1MTokens = 0.15m,
            QualityTier = qualityTier,
            Rationale = "Test rationale",
            AlternativeModels = ["alt-model-1", "alt-model-2"]
        };
    }

    #endregion
}
