using Api.BoundedContexts.KnowledgeBase.Application.Reports.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using Api.BoundedContexts.KnowledgeBase.Domain.Reports;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Reports.Services;

/// <summary>
/// Unit tests for ReportGeneratorService.
/// ADR-016 Phase 5: Validates markdown report generation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ReportGeneratorServiceTests
{
    private readonly ReportGeneratorService _service;

    public ReportGeneratorServiceTests()
    {
        _service = new ReportGeneratorService();
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsTitle()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("# RAG Pipeline Evaluation Report", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsGeneratedDate()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("**Generated**:", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsDatasetName()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("**Dataset**: test-dataset", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsSampleCount()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("**Samples**:", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsExecutiveSummary()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("## Executive Summary", markdown);
        Assert.Contains("**Configurations Evaluated**:", markdown);
        Assert.Contains("**Successful Evaluations**:", markdown);
        Assert.Contains("**Meeting Phase 5 Target**:", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsBestMetricsTable()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("### Best Metrics Achieved", markdown);
        Assert.Contains("| Metric | Value |", markdown);
        Assert.Contains("Best Recall@10", markdown);
        Assert.Contains("Best nDCG@10", markdown);
        Assert.Contains("Best P95 Latency", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsPhase5TargetStatus()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("## Phase 5 Target Status", markdown);
        Assert.Contains("| Target | Required | Achieved | Status |", markdown);
        Assert.Contains("Recall@10", markdown);
        Assert.Contains("P95 Latency", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsConfigurationComparison()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("## Configuration Comparison", markdown);
        Assert.Contains("| Configuration | Chunking | Quantization | Reranking |", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsBestConfigurations()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("## Best Configurations", markdown);
        Assert.Contains("### Best by Recall@10", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsRecommendations()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("## Recommendations", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsFooter()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("ADR-016 Phase 5 Evaluation Framework", markdown);
        Assert.Contains("Grid Search ID:", markdown);
        Assert.Contains("Total Duration:", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_WithMeetsTarget_ContainsPassedStatus()
    {
        // Arrange
        var report = CreateReportWithMeetsTarget(true);

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("**PASSED**", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_WithNotMeetsTarget_ContainsNotMetStatus()
    {
        // Arrange
        var report = CreateReportWithMeetsTarget(false);

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("**NOT MET**", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_WithMeetsTarget_ContainsNextSteps()
    {
        // Arrange
        var report = CreateReportWithMeetsTarget(true);

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("### Phase 5 Target Met", markdown);
        Assert.Contains("**Next Steps**:", markdown);
    }

    [Fact]
    public void GenerateMarkdownReport_WithNotMeetsTarget_ContainsImprovementSuggestions()
    {
        // Arrange
        var report = CreateReportWithMeetsTarget(false);

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.Contains("### Phase 5 Target Not Met", markdown);
        Assert.Contains("**Improvement Suggestions**:", markdown);
    }

    [Fact]
    public void GenerateComparisonTable_ContainsTableHeaders()
    {
        // Arrange
        var gridSearchResult = CreateSampleGridSearchResult();

        // Act
        var table = _service.GenerateComparisonTable(gridSearchResult);

        // Assert
        Assert.Contains("## Configuration Comparison", table);
        Assert.Contains("| Configuration | Chunking | Quantization | Reranking |", table);
        Assert.Contains("Recall@5", table);
        Assert.Contains("Recall@10", table);
        Assert.Contains("nDCG@10", table);
        Assert.Contains("MRR", table);
        Assert.Contains("P95 (ms)", table);
        Assert.Contains("Status", table);
    }

    [Fact]
    public void GenerateComparisonTable_ContainsConfigurationRows()
    {
        // Arrange
        var gridSearchResult = CreateSampleGridSearchResult();

        // Act
        var table = _service.GenerateComparisonTable(gridSearchResult);

        // Assert
        // Should contain the configuration IDs
        var configs = GridSearchConfiguration.GetAllConfigurations();
        Assert.Contains(configs[0].ConfigurationId, table);
        Assert.Contains(configs[1].ConfigurationId, table);
    }

    [Fact]
    public void GenerateComparisonTable_ShowsStatusEmoji()
    {
        // Arrange
        var gridSearchResult = CreateSampleGridSearchResult();

        // Act
        var table = _service.GenerateComparisonTable(gridSearchResult);

        // Assert
        // Should contain status emojis (checkmark for meets target, warning for doesn't)
        Assert.True(table.Contains("✅") || table.Contains("⚠️"));
    }

    [Fact]
    public void GenerateComparisonTable_WithFailedConfig_ShowsFailedStatus()
    {
        // Arrange
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config1 = configs[0];
        var config2 = configs[1];

        var metrics1 = EvaluationMetrics.Create(0.7, 0.80, 0.75, 0.8, 1200, 0.85, 50);

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config1, metrics1, 50, 1000),
            ConfigurationResult.Failure(config2, "Connection error", 500)
        }.AsReadOnly();

        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        // Act
        var table = _service.GenerateComparisonTable(gridSearchResult);

        // Assert
        Assert.Contains("❌ Failed", table);
    }

    [Fact]
    public void GenerateMarkdownReport_IsNotEmpty()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        Assert.NotNull(markdown);
        Assert.NotEmpty(markdown);
        Assert.True(markdown.Length > 100); // Should be a substantial report
    }

    private static BenchmarkReport CreateSampleReport()
    {
        var gridSearchResult = CreateSampleGridSearchResult();
        return BenchmarkReport.FromGridSearchResult(gridSearchResult);
    }

    private static BenchmarkReport CreateReportWithMeetsTarget(bool meetsTarget)
    {
        var configs = GridSearchConfiguration.GetAllConfigurations();
        var config = configs[0];

        var metrics = meetsTarget
            ? EvaluationMetrics.Create(0.7, 0.80, 0.75, 0.8, 1200, 0.85, 50) // Meets target
            : EvaluationMetrics.Create(0.5, 0.55, 0.50, 0.5, 2000, 0.60, 50); // Doesn't meet target

        var results = new List<ConfigurationResult>
        {
            ConfigurationResult.Success(config, metrics, 50, 1000)
        }.AsReadOnly();

        var gridSearchResult = GridSearchResult.Create(
            "test-dataset",
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(1),
            results);

        return BenchmarkReport.FromGridSearchResult(gridSearchResult);
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
