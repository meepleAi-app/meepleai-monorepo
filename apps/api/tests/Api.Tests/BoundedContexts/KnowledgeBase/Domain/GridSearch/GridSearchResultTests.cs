using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.GridSearch;

/// <summary>
/// Unit tests for GridSearchResult and ConfigurationResult.
/// ADR-016 Phase 5: Validates result aggregation and best configuration selection.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class GridSearchResultTests
{
    [Fact]
    public void Create_WithValidResults_CreatesGridSearchResult()
    {
        // Arrange
        var startedAt = DateTime.UtcNow;
        var completedAt = startedAt.AddMinutes(5);
        var results = CreateSampleResults();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            startedAt,
            completedAt,
            results);

        // Assert
        Assert.NotNull(gridSearchResult);
        Assert.Equal("test-dataset", gridSearchResult.DatasetName);
        Assert.Equal(startedAt, gridSearchResult.StartedAt);
        Assert.Equal(completedAt, gridSearchResult.CompletedAt);
        Assert.Equal(3, gridSearchResult.ConfigurationCount);
    }

    [Fact]
    public void TotalDurationMs_CalculatedCorrectly()
    {
        // Arrange
        var startedAt = DateTime.UtcNow;
        var completedAt = startedAt.AddMilliseconds(5000);
        var results = CreateSampleResults();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            startedAt,
            completedAt,
            results);

        // Assert
        Assert.Equal(5000, gridSearchResult.TotalDurationMs, 1);
    }

    [Fact]
    public void SuccessfulCount_ReturnsCorrectCount()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];
        var config3 = configs[2];

        var metrics = EvaluationMetrics.Create(0.7, 0.8, 0.75, 0.8, 1200, 0.85, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics, 50, 1000),
            ConfigurationResult.Failure(config2, "Test error", 500),
            ConfigurationResult.Success(config3, metrics, 50, 2000)
        }.AsReadOnly();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Assert
        Assert.Equal(2, gridSearchResult.SuccessfulCount);
    }

    [Fact]
    public void BestByRecallAt10_ReturnsHighestRecallConfiguration()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];
        var config3 = configs[2];

        var metrics1 = EvaluationMetrics.Create(0.6, 0.70, 0.65, 0.7, 1200, 0.75, 50);
        var metrics2 = EvaluationMetrics.Create(0.7, 0.85, 0.80, 0.8, 1400, 0.85, 50); // Highest recall
        var metrics3 = EvaluationMetrics.Create(0.65, 0.75, 0.70, 0.75, 1100, 0.80, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500),
            ConfigurationResult.Success(config3, metrics3, 50, 2000)
        }.AsReadOnly();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Assert
        Assert.NotNull(gridSearchResult.BestByRecallAt10);
        Assert.Equal(config2.ConfigurationId, gridSearchResult.BestByRecallAt10.Configuration.ConfigurationId);
        Assert.Equal(0.85, gridSearchResult.BestByRecallAt10.Metrics.RecallAt10);
    }

    [Fact]
    public void BestByNdcg_ReturnsHighestNdcgConfiguration()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        var metrics1 = EvaluationMetrics.Create(0.7, 0.75, 0.90, 0.8, 1200, 0.85, 50); // Highest nDCG
        var metrics2 = EvaluationMetrics.Create(0.75, 0.85, 0.70, 0.8, 1100, 0.85, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500)
        }.AsReadOnly();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Assert
        Assert.NotNull(gridSearchResult.BestByNdcg);
        Assert.Equal(config1.ConfigurationId, gridSearchResult.BestByNdcg.Configuration.ConfigurationId);
        Assert.Equal(0.90, gridSearchResult.BestByNdcg.Metrics.NdcgAt10);
    }

    [Fact]
    public void BestByLatency_ReturnsLowestLatencyConfiguration()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        var metrics1 = EvaluationMetrics.Create(0.7, 0.75, 0.70, 0.7, 1500, 0.80, 50);
        var metrics2 = EvaluationMetrics.Create(0.65, 0.70, 0.65, 0.65, 800, 0.75, 50); // Lowest latency

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500)
        }.AsReadOnly();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Assert
        Assert.NotNull(gridSearchResult.BestByLatency);
        Assert.Equal(config2.ConfigurationId, gridSearchResult.BestByLatency.Configuration.ConfigurationId);
        Assert.Equal(800, gridSearchResult.BestByLatency.Metrics.P95LatencyMs);
    }

    [Fact]
    public void MeetsPhase5Target_WithQualifyingConfiguration_ReturnsTrue()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config = configs[0];

        // Metrics that meet Phase 5 targets: Recall@10 >= 70%, P95 < 1500ms
        var metrics = EvaluationMetrics.Create(0.7, 0.75, 0.70, 0.7, 1200, 0.80, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config, metrics, 50, 1000)
        }.AsReadOnly();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Assert
        Assert.True(gridSearchResult.MeetsPhase5Target);
    }

    [Fact]
    public void MeetsPhase5Target_WithNoQualifyingConfiguration_ReturnsFalse()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        // Config1: Good recall but high latency
        var metrics1 = EvaluationMetrics.Create(0.7, 0.75, 0.70, 0.7, 1600, 0.80, 50);
        // Config2: Low recall but good latency
        var metrics2 = EvaluationMetrics.Create(0.5, 0.55, 0.50, 0.5, 1000, 0.60, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500)
        }.AsReadOnly();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Assert
        Assert.False(gridSearchResult.MeetsPhase5Target);
    }

    [Fact]
    public void GridSearchId_IsValidGuid()
    {
        // Arrange
        var results = CreateSampleResults();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Assert
        Assert.True(Guid.TryParse(gridSearchResult.GridSearchId, out _));
    }

    [Fact]
    public void ConfigurationResult_Success_SetsIsSuccessTrue()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config = configs[0];
        var metrics = EvaluationMetrics.Create(0.7, 0.8, 0.75, 0.8, 1200, 0.85, 50);

        // Act
        var result = ConfigurationResult.Success(config, metrics, 50, 1500);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(50, result.SampleCount);
        Assert.Equal(1500, result.DurationMs);
    }

    [Fact]
    public void ConfigurationResult_Failure_SetsIsSuccessFalse()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config = configs[0];

        // Act
        var result = ConfigurationResult.Failure(config, "Connection timeout", 500);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Connection timeout", result.ErrorMessage);
        Assert.Equal(EvaluationMetrics.Empty, result.Metrics);
        Assert.Equal(0, result.SampleCount);
        Assert.Equal(500, result.DurationMs);
    }

    [Fact]
    public void BestConfigurations_WithAllFailures_ReturnsNulls()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Failure(config1, "Error 1", 500),
            ConfigurationResult.Failure(config2, "Error 2", 600)
        }.AsReadOnly();

        // Act
        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Assert
        Assert.Null(gridSearchResult.BestByRecallAt10);
        Assert.Null(gridSearchResult.BestByNdcg);
        Assert.Null(gridSearchResult.BestByLatency);
    }

    private static IReadOnlyList<ConfigurationResult> CreateSampleResults()
    {
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];
        var config3 = configs[2];

        var metrics = EvaluationMetrics.Create(0.7, 0.8, 0.75, 0.8, 1200, 0.85, 50);

        return new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics, 50, 1000),
            ConfigurationResult.Success(config2, metrics, 50, 1500),
            ConfigurationResult.Success(config3, metrics, 50, 2000)
        }.AsReadOnly();
    }
}
