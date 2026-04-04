using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using Xunit;
using FluentAssertions;
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
        gridSearchResult.Should().NotBeNull();
        gridSearchResult.DatasetName.Should().Be("test-dataset");
        gridSearchResult.StartedAt.Should().Be(startedAt);
        gridSearchResult.CompletedAt.Should().Be(completedAt);
        gridSearchResult.ConfigurationCount.Should().Be(3);
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
        gridSearchResult.TotalDurationMs.Should().BeApproximately(5000, 1);
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
        gridSearchResult.SuccessfulCount.Should().Be(2);
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
        gridSearchResult.BestByRecallAt10.Should().NotBeNull();
        gridSearchResult.BestByRecallAt10.Configuration.ConfigurationId.Should().Be(config2.ConfigurationId);
        gridSearchResult.BestByRecallAt10.Metrics.RecallAt10.Should().Be(0.85);
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
        gridSearchResult.BestByNdcg.Should().NotBeNull();
        gridSearchResult.BestByNdcg.Configuration.ConfigurationId.Should().Be(config1.ConfigurationId);
        gridSearchResult.BestByNdcg.Metrics.NdcgAt10.Should().Be(0.90);
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
        gridSearchResult.BestByLatency.Should().NotBeNull();
        gridSearchResult.BestByLatency.Configuration.ConfigurationId.Should().Be(config2.ConfigurationId);
        gridSearchResult.BestByLatency.Metrics.P95LatencyMs.Should().Be(800);
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
        gridSearchResult.MeetsPhase5Target.Should().BeTrue();
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
        gridSearchResult.MeetsPhase5Target.Should().BeFalse();
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
        Guid.TryParse(gridSearchResult.GridSearchId, out _).Should().BeTrue();
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
        result.IsSuccess.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.SampleCount.Should().Be(50);
        result.DurationMs.Should().Be(1500);
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
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().Be("Connection timeout");
        result.Metrics.Should().Be(EvaluationMetrics.Empty);
        result.SampleCount.Should().Be(0);
        result.DurationMs.Should().Be(500);
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
        gridSearchResult.BestByRecallAt10.Should().BeNull();
        gridSearchResult.BestByNdcg.Should().BeNull();
        gridSearchResult.BestByLatency.Should().BeNull();
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
