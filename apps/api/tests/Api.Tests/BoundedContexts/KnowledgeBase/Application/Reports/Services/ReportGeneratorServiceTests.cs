using Api.BoundedContexts.KnowledgeBase.Application.Reports.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;
using Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;
using Api.BoundedContexts.KnowledgeBase.Domain.Reports;
using Xunit;
using FluentAssertions;
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
        markdown.Should().Contain("# RAG Pipeline Evaluation Report");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsGeneratedDate()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().Contain("**Generated**:");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsDatasetName()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().Contain("**Dataset**: test-dataset");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsSampleCount()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().Contain("**Samples**:");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsExecutiveSummary()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().Contain("## Executive Summary");
        markdown.Should().Contain("**Configurations Evaluated**:");
        markdown.Should().Contain("**Successful Evaluations**:");
        markdown.Should().Contain("**Meeting Phase 5 Target**:");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsBestMetricsTable()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("### Best Metrics Achieved");
        markdown.Should().ContainEquivalentOf("| Metric | Value |");
        markdown.Should().ContainEquivalentOf("Best Recall@10");
        markdown.Should().ContainEquivalentOf("Best nDCG@10");
        markdown.Should().ContainEquivalentOf("Best P95 Latency");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsPhase5TargetStatus()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("## Phase 5 Target Status");
        markdown.Should().ContainEquivalentOf("| Target | Required | Achieved | Status |");
        markdown.Should().ContainEquivalentOf("Recall@10");
        markdown.Should().ContainEquivalentOf("P95 Latency");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsConfigurationComparison()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("## Configuration Comparison");
        markdown.Should().ContainEquivalentOf("| Configuration | Chunking | Quantization | Reranking |");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsBestConfigurations()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("## Best Configurations");
        markdown.Should().ContainEquivalentOf("### Best by Recall@10");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsRecommendations()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("## Recommendations");
    }

    [Fact]
    public void GenerateMarkdownReport_ContainsFooter()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("ADR-016 Phase 5 Evaluation Framework");
        markdown.Should().ContainEquivalentOf("Grid Search ID:");
        markdown.Should().ContainEquivalentOf("Total Duration:");
    }

    [Fact]
    public void GenerateMarkdownReport_WithMeetsTarget_ContainsPassedStatus()
    {
        // Arrange
        var report = CreateReportWithMeetsTarget(true);

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("**PASSED**");
    }

    [Fact]
    public void GenerateMarkdownReport_WithNotMeetsTarget_ContainsNotMetStatus()
    {
        // Arrange
        var report = CreateReportWithMeetsTarget(false);

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("**NOT MET**");
    }

    [Fact]
    public void GenerateMarkdownReport_WithMeetsTarget_ContainsNextSteps()
    {
        // Arrange
        var report = CreateReportWithMeetsTarget(true);

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("### Phase 5 Target Met");
        markdown.Should().ContainEquivalentOf("**Next Steps**:");
    }

    [Fact]
    public void GenerateMarkdownReport_WithNotMeetsTarget_ContainsImprovementSuggestions()
    {
        // Arrange
        var report = CreateReportWithMeetsTarget(false);

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().ContainEquivalentOf("### Phase 5 Target Not Met");
        markdown.Should().ContainEquivalentOf("**Improvement Suggestions**:");
    }

    [Fact]
    public void GenerateComparisonTable_ContainsTableHeaders()
    {
        // Arrange
        var gridSearchResult = CreateSampleGridSearchResult();

        // Act
        var table = _service.GenerateComparisonTable(gridSearchResult);

        // Assert
        table.Should().ContainEquivalentOf("## Configuration Comparison");
        table.Should().ContainEquivalentOf("| Configuration | Chunking | Quantization | Reranking |");
        table.Should().ContainEquivalentOf("Recall@5");
        table.Should().ContainEquivalentOf("Recall@10");
        table.Should().ContainEquivalentOf("nDCG@10");
        table.Should().ContainEquivalentOf("MRR");
        table.Should().ContainEquivalentOf("P95 (ms)");
        table.Should().ContainEquivalentOf("Status");
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
        table.Should().Contain(configs[0].ConfigurationId);
        table.Should().Contain(configs[1].ConfigurationId);
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
        (table.Contains("✅") || table.Contains("⚠️")).Should().BeTrue();
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
        table.Should().Contain("❌ Failed");
    }

    [Fact]
    public void GenerateMarkdownReport_IsNotEmpty()
    {
        // Arrange
        var report = CreateSampleReport();

        // Act
        var markdown = _service.GenerateMarkdownReport(report);

        // Assert
        markdown.Should().NotBeNull();
        markdown.Should().NotBeEmpty();
        (markdown.Length > 100).Should().BeTrue(); // Should be a substantial report
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
