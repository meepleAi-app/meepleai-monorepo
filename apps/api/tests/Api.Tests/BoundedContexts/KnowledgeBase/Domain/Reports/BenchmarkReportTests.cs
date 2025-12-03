using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using Api.BoundedContexts.KnowledgeBase.Domain.Reports;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Reports;

/// <summary>
/// Unit tests for BenchmarkReport and ReportSummary.
/// ADR-016 Phase 5: Validates benchmark report creation and summary calculations.
/// </summary>
public class BenchmarkReportTests
{
    [Fact]
    public void FromGridSearchResult_WithValidGridSearchResult_CreatesReport()
    {
        // Arrange
        var gridSearchResult = CreateSampleGridSearchResult();

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.NotNull(report);
        Assert.Contains("test-dataset", report.Title);
        Assert.Equal("test-dataset", report.DatasetName);
        Assert.NotNull(report.GridSearchResult);
    }

    [Fact]
    public void GeneratedAt_IsSetToCurrentUtcTime()
    {
        // Arrange
        var gridSearchResult = CreateSampleGridSearchResult();
        var beforeCreation = DateTime.UtcNow;

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);
        var afterCreation = DateTime.UtcNow;

        // Assert
        Assert.InRange(report.GeneratedAt, beforeCreation, afterCreation);
    }

    [Fact]
    public void Targets_ReturnsDefaultPhase5Targets()
    {
        // Arrange
        var gridSearchResult = CreateSampleGridSearchResult();

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.Equal(0.70, report.Targets.MinRecallAt10);
        Assert.Equal(1500.0, report.Targets.MaxP95LatencyMs);
    }

    [Fact]
    public void Summary_TotalConfigurations_MatchesGridSearchResult()
    {
        // Arrange
        var gridSearchResult = CreateSampleGridSearchResult();

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.Equal(gridSearchResult.ConfigurationCount, report.Summary.TotalConfigurations);
    }

    [Fact]
    public void Summary_SuccessfulConfigurations_MatchesGridSearchResult()
    {
        // Arrange
        var gridSearchResult = CreateSampleGridSearchResult();

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.Equal(gridSearchResult.SuccessfulCount, report.Summary.SuccessfulConfigurations);
    }

    [Fact]
    public void Summary_BestRecallAt10_ReturnsHighestValue()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        var metrics1 = EvaluationMetrics.Create(0.6, 0.70, 0.65, 0.7, 1200, 0.75, 50);
        var metrics2 = EvaluationMetrics.Create(0.7, 0.85, 0.80, 0.8, 1400, 0.85, 50); // Highest recall

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500)
        }.AsReadOnly();

        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.Equal(0.85, report.Summary.BestRecallAt10);
    }

    [Fact]
    public void Summary_BestNdcgAt10_ReturnsHighestValue()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        var metrics1 = EvaluationMetrics.Create(0.6, 0.70, 0.90, 0.7, 1200, 0.75, 50); // Highest nDCG
        var metrics2 = EvaluationMetrics.Create(0.7, 0.85, 0.70, 0.8, 1400, 0.85, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500)
        }.AsReadOnly();

        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.Equal(0.90, report.Summary.BestNdcgAt10);
    }

    [Fact]
    public void Summary_BestP95LatencyMs_ReturnsLowestValue()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        var metrics1 = EvaluationMetrics.Create(0.6, 0.70, 0.65, 0.7, 1500, 0.75, 50);
        var metrics2 = EvaluationMetrics.Create(0.7, 0.85, 0.80, 0.8, 900, 0.85, 50); // Lowest latency

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500)
        }.AsReadOnly();

        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.Equal(900, report.Summary.BestP95LatencyMs);
    }

    [Fact]
    public void Summary_ConfigurationsMeetingTarget_CountsCorrectly()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];
        var config3 = configs[2];

        // Config1: Meets target (Recall >= 70%, P95 < 1500ms)
        var metrics1 = EvaluationMetrics.Create(0.65, 0.75, 0.70, 0.7, 1200, 0.80, 50);
        // Config2: Low recall
        var metrics2 = EvaluationMetrics.Create(0.5, 0.55, 0.50, 0.5, 1000, 0.60, 50);
        // Config3: Meets target
        var metrics3 = EvaluationMetrics.Create(0.7, 0.80, 0.75, 0.75, 1100, 0.85, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500),
            ConfigurationResult.Success(config3, metrics3, 50, 2000)
        }.AsReadOnly();

        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.Equal(2, report.Summary.ConfigurationsMeetingTarget);
    }

    [Fact]
    public void Summary_MeetsPhase5Target_WhenAnyConfigMeetsTarget()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config = configs[0];

        // Metrics that meet Phase 5 targets
        var metrics = EvaluationMetrics.Create(0.7, 0.75, 0.70, 0.7, 1200, 0.80, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config, metrics, 50, 1000)
        }.AsReadOnly();

        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.True(report.Summary.MeetsPhase5Target);
    }

    [Fact]
    public void Summary_RecommendedConfiguration_ReturnsBestByRecallId()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        var metrics1 = EvaluationMetrics.Create(0.6, 0.70, 0.65, 0.7, 1200, 0.75, 50);
        var metrics2 = EvaluationMetrics.Create(0.7, 0.85, 0.80, 0.8, 1100, 0.85, 50); // Best recall, meets target

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500)
        }.AsReadOnly();

        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert
        Assert.Equal(config2.ConfigurationId, report.Summary.RecommendedConfiguration);
    }

    [Fact]
    public void Summary_WithNoSuccessfulConfigs_ReturnsDefaultValues()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config = configs[0];

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Failure(config, "Error", 500)
        }.AsReadOnly();

        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Act
        var report = BenchmarkReport.FromGridSearchResult(gridSearchResult);

        // Assert - When no successful configs, Max() and Min() on empty sequences throw
        // The report should handle this gracefully
        Assert.Equal(0, report.Summary.SuccessfulConfigurations);
        Assert.Null(report.Summary.RecommendedConfiguration);
        Assert.False(report.Summary.MeetsPhase5Target);
    }

    [Fact]
    public void Phase5Targets_Default_HasCorrectValues()
    {
        // Arrange & Act
        var targets = Phase5Targets.Default;

        // Assert
        Assert.Equal(0.70, targets.MinRecallAt10);
        Assert.Equal(1500.0, targets.MaxP95LatencyMs);
    }

    [Fact]
    public void Phase5Targets_Custom_CanBeCreated()
    {
        // Arrange & Act
        var targets = new Phase5Targets
        {
            MinRecallAt10 = 0.80,
            MaxP95LatencyMs = 1000.0
        };

        // Assert
        Assert.Equal(0.80, targets.MinRecallAt10);
        Assert.Equal(1000.0, targets.MaxP95LatencyMs);
    }

    private static GridSearchResult CreateSampleGridSearchResult()
    {
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        var metrics1 = EvaluationMetrics.Create(0.6, 0.70, 0.65, 0.7, 1200, 0.75, 50);
        var metrics2 = EvaluationMetrics.Create(0.7, 0.80, 0.75, 0.8, 1100, 0.85, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Success(config2, metrics2, 50, 1500)
        }.AsReadOnly();

        return GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);
    }
}
