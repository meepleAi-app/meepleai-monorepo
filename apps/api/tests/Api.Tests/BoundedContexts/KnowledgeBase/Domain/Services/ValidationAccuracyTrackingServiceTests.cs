using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Unit tests for ValidationAccuracyTrackingService.
/// BGAI-039: Validation accuracy baseline measurement.
/// </summary>
public class ValidationAccuracyTrackingServiceTests
{
    private readonly ValidationAccuracyTrackingService _service;

    public ValidationAccuracyTrackingServiceTests()
    {
        _service = new ValidationAccuracyTrackingService();
    }

    private static PromptEvaluationResult CreateEvaluationResult(int totalQueries, int passedAll)
    {
        var queryResults = new List<QueryEvaluationResult>();

        for (var i = 0; i < passedAll; i++)
        {
            queryResults.Add(new QueryEvaluationResult
            {
                TestCaseId = $"test-{i}",
                Query = $"Query {i}",
                Response = $"Response {i}",
                IsAccurate = true,
                IsRelevant = true,
                IsComplete = true,
                IsClear = true,
                HasGoodCitationQuality = true
            });
        }

        for (var i = passedAll; i < totalQueries; i++)
        {
            queryResults.Add(new QueryEvaluationResult
            {
                TestCaseId = $"test-{i}",
                Query = $"Query {i}",
                Response = $"Response {i}",
                IsAccurate = false,
                IsRelevant = false,
                IsComplete = false,
                IsClear = false,
                HasGoodCitationQuality = false
            });
        }

        return new PromptEvaluationResult
        {
            EvaluationId = Guid.NewGuid().ToString(),
            TemplateId = Guid.NewGuid().ToString(),
            VersionId = Guid.NewGuid().ToString(),
            DatasetId = "test-dataset",
            TotalQueries = totalQueries,
            Metrics = new EvaluationMetrics(),
            QueryResults = queryResults
        };
    }

    [Fact]
    public void CalculateAccuracyMetrics_WithPerfectPrediction_ReturnsHighAccuracy()
    {
        // Arrange
        var evaluationResult = CreateEvaluationResult(totalQueries: 100, passedAll: 80);
        var expectedValidCount = 80;

        // Act
        var metrics = _service.CalculateAccuracyMetrics(evaluationResult, expectedValidCount);

        // Assert
        Assert.Equal(80, metrics.TruePositives);
        Assert.Equal(20, metrics.TrueNegatives);
        Assert.Equal(0, metrics.FalsePositives);
        Assert.Equal(0, metrics.FalseNegatives);
        Assert.Equal(1.0, metrics.Accuracy);
        Assert.Equal(1.0, metrics.Precision);
        Assert.Equal(1.0, metrics.Recall);
        Assert.True(metrics.MeetsBaselineThreshold);
    }

    [Fact]
    public void CalculateAccuracyMetrics_WithFalsePositives_ReturnsCorrectMetrics()
    {
        // Arrange - Model says 90 are valid, but only 80 are actually valid
        var evaluationResult = CreateEvaluationResult(totalQueries: 100, passedAll: 90);
        var expectedValidCount = 80;

        // Act
        var metrics = _service.CalculateAccuracyMetrics(evaluationResult, expectedValidCount);

        // Assert
        Assert.Equal(80, metrics.TruePositives); // Min of expected (80) and actual (90)
        Assert.Equal(10, metrics.TrueNegatives); // Min of expected invalid (20) and actual invalid (10)
        Assert.Equal(10, metrics.FalsePositives); // Actual (90) - Expected (80)
        Assert.Equal(0, metrics.FalseNegatives); // Expected (80) - Actual (90) capped at 0
        Assert.Equal(0.90, metrics.Accuracy); // (80 + 10) / 100
    }

    [Fact]
    public void CalculateAccuracyMetrics_WithFalseNegatives_ReturnsCorrectMetrics()
    {
        // Arrange - Model says 70 are valid, but 80 are actually valid
        var evaluationResult = CreateEvaluationResult(totalQueries: 100, passedAll: 70);
        var expectedValidCount = 80;

        // Act
        var metrics = _service.CalculateAccuracyMetrics(evaluationResult, expectedValidCount);

        // Assert
        Assert.Equal(70, metrics.TruePositives); // Min of expected (80) and actual (70)
        Assert.Equal(20, metrics.TrueNegatives); // Min of expected invalid (20) and actual invalid (30)
        Assert.Equal(0, metrics.FalsePositives); // Actual (70) - Expected (80) capped at 0
        Assert.Equal(10, metrics.FalseNegatives); // Expected (80) - Actual (70)
        Assert.Equal(0.90, metrics.Accuracy); // (70 + 20) / 100
    }

    [Fact]
    public void CalculateAccuracyMetrics_WithNullEvaluation_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.CalculateAccuracyMetrics(null!, 50));
    }

    [Fact]
    public void CalculateAccuracyMetrics_WithNegativeExpectedValid_ThrowsArgumentException()
    {
        // Arrange
        var evaluationResult = CreateEvaluationResult(totalQueries: 100, passedAll: 80);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.CalculateAccuracyMetrics(evaluationResult, -1));
    }

    [Fact]
    public void CalculateAccuracyMetrics_WithExpectedValidExceedingTotal_ThrowsArgumentException()
    {
        // Arrange
        var evaluationResult = CreateEvaluationResult(totalQueries: 100, passedAll: 80);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.CalculateAccuracyMetrics(evaluationResult, 101));
    }

    [Fact]
    public void CalculateMetricDimensionAccuracy_WithAccuracyDimension_CalculatesCorrectly()
    {
        // Arrange
        var evaluationResult = CreateEvaluationResult(totalQueries: 100, passedAll: 85);
        var expectedValidCount = 80;

        // Act - Test only the IsAccurate dimension
        var metrics = _service.CalculateMetricDimensionAccuracy(
            evaluationResult,
            expectedValidCount,
            r => r.IsAccurate);

        // Assert
        Assert.Equal(80, metrics.TruePositives);
        Assert.Equal(15, metrics.TrueNegatives);
        Assert.Equal(5, metrics.FalsePositives);
        Assert.Equal(0, metrics.FalseNegatives);
    }

    [Fact]
    public void CalculateMetricDimensionAccuracy_WithNullSelector_ThrowsArgumentNullException()
    {
        // Arrange
        var evaluationResult = CreateEvaluationResult(totalQueries: 100, passedAll: 80);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.CalculateMetricDimensionAccuracy(evaluationResult, 80, null!));
    }

    [Fact]
    public void CalculateAggregatedAccuracy_WithMultipleEvaluations_AggregatesCorrectly()
    {
        // Arrange
        var evaluations = new List<PromptEvaluationResult>
        {
            CreateEvaluationResult(totalQueries: 100, passedAll: 85),
            CreateEvaluationResult(totalQueries: 50, passedAll: 45),
            CreateEvaluationResult(totalQueries: 75, passedAll: 60)
        };
        var expectedValidCounts = new List<int> { 80, 40, 65 };

        // Act
        var metrics = _service.CalculateAggregatedAccuracy(evaluations, expectedValidCounts);

        // Assert
        // Eval 1: TP=80, TN=15, FP=5, FN=0
        // Eval 2: TP=40, TN=5, FP=5, FN=0
        // Eval 3: TP=60, TN=10, FP=0, FN=5
        // Total: TP=180, TN=30, FP=10, FN=5
        Assert.Equal(180, metrics.TruePositives);
        Assert.Equal(30, metrics.TrueNegatives);
        Assert.Equal(10, metrics.FalsePositives);
        Assert.Equal(5, metrics.FalseNegatives);
        Assert.Equal(225, metrics.Total);
        // Accuracy = (180 + 30) / 225 = 210 / 225 = 0.9333
        Assert.Equal(0.9333, metrics.Accuracy, precision: 4);
    }

    [Fact]
    public void CalculateAggregatedAccuracy_WithEmptyList_ThrowsArgumentException()
    {
        // Arrange
        var emptyList = new List<PromptEvaluationResult>();
        var expectedCounts = new List<int>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.CalculateAggregatedAccuracy(emptyList, expectedCounts));
    }

    [Fact]
    public void CalculateAggregatedAccuracy_WithMismatchedCounts_ThrowsArgumentException()
    {
        // Arrange
        var evaluations = new List<PromptEvaluationResult>
        {
            CreateEvaluationResult(totalQueries: 100, passedAll: 80)
        };
        var expectedCounts = new List<int> { 80, 70 }; // Mismatched length

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.CalculateAggregatedAccuracy(evaluations, expectedCounts));
    }

    [Fact]
    public void GenerateAccuracyReport_WithHighAccuracy_ReturnsPositiveReport()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 85,
            trueNegatives: 10,
            falsePositives: 3,
            falseNegatives: 2);

        // Act
        var report = _service.GenerateAccuracyReport(metrics, "Overall Validation");

        // Assert
        Assert.Equal("Overall Validation", report.Context);
        Assert.True(report.MeetsBaseline);
        Assert.Equal(ValidationAccuracyLevel.Excellent, report.QualityLevel);
        Assert.Contains("Overall Validation", report.Summary);
        Assert.Contains("0.95", report.Summary); // Accuracy percentage
        Assert.NotEmpty(report.Recommendations);
    }

    [Fact]
    public void GenerateAccuracyReport_WithLowAccuracy_ReturnsWarningReport()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 60,
            trueNegatives: 10,
            falsePositives: 20,
            falseNegatives: 10);

        // Act
        var report = _service.GenerateAccuracyReport(metrics, "Layer 1: Confidence");

        // Assert
        Assert.Equal("Layer 1: Confidence", report.Context);
        Assert.False(report.MeetsBaseline); // Accuracy = 70%, below 80%
        Assert.Equal(ValidationAccuracyLevel.Fair, report.QualityLevel);
        Assert.Contains("⚠️", report.Summary); // Warning emoji
        Assert.NotEmpty(report.Recommendations);
        Assert.Contains(report.Recommendations, r => r.Contains("below baseline threshold"));
    }

    [Fact]
    public void GenerateAccuracyReport_WithLowPrecision_RecommendsFixing()
    {
        // Arrange - Low precision (high false positives)
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 50,
            trueNegatives: 10,
            falsePositives: 30, // High false positives
            falseNegatives: 10);

        // Act
        var report = _service.GenerateAccuracyReport(metrics, "Test Context");

        // Assert
        Assert.Contains(report.Recommendations, r => r.Contains("precision"));
        Assert.Contains(report.Recommendations, r => r.Contains("false positive"));
    }

    [Fact]
    public void GenerateAccuracyReport_WithLowRecall_RecommendsFixing()
    {
        // Arrange - Low recall (high false negatives)
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 50,
            trueNegatives: 30,
            falsePositives: 5,
            falseNegatives: 15); // High false negatives

        // Act
        var report = _service.GenerateAccuracyReport(metrics, "Test Context");

        // Assert
        Assert.Contains(report.Recommendations, r => r.Contains("recall"));
        Assert.Contains(report.Recommendations, r => r.Contains("false negative"));
    }

    [Fact]
    public void GenerateAccuracyReport_WithPerfectMetrics_ReturnsPositiveRecommendation()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 90,
            trueNegatives: 10,
            falsePositives: 0,
            falseNegatives: 0);

        // Act
        var report = _service.GenerateAccuracyReport(metrics, "Perfect Validation");

        // Assert
        Assert.True(report.MeetsBaseline);
        Assert.Equal(ValidationAccuracyLevel.Excellent, report.QualityLevel);
        Assert.Contains(report.Recommendations, r => r.Contains("performing well"));
    }

    [Fact]
    public void GenerateAccuracyReport_WithNullMetrics_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            _service.GenerateAccuracyReport(null!, "Test Context"));
    }

    [Fact]
    public void GenerateAccuracyReport_WithNullContext_ThrowsArgumentException()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(80, 15, 3, 2);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.GenerateAccuracyReport(metrics, null!));
    }

    [Fact]
    public void GenerateAccuracyReport_WithEmptyContext_ThrowsArgumentException()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(80, 15, 3, 2);

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            _service.GenerateAccuracyReport(metrics, ""));
    }

    [Fact]
    public void MinimumAccuracyThreshold_IsPointEight()
    {
        // Assert
        Assert.Equal(0.80, ValidationAccuracyTrackingService.MinimumAccuracyThreshold);
    }

    [Fact]
    public void GenerateAccuracyReport_IncludesTimestamp()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(80, 15, 3, 2);
        var before = DateTime.UtcNow;

        // Act
        var report = _service.GenerateAccuracyReport(metrics, "Test Context");

        // Assert
        var after = DateTime.UtcNow;
        Assert.InRange(report.Timestamp, before, after);
    }

    [Theory]
    [InlineData(90, 5, 3, 2, ValidationAccuracyLevel.Excellent)] // 95% accuracy
    [InlineData(80, 12, 5, 3, ValidationAccuracyLevel.VeryGood)] // 92% accuracy
    [InlineData(70, 15, 10, 5, ValidationAccuracyLevel.Good)] // 85% accuracy
    [InlineData(60, 15, 15, 10, ValidationAccuracyLevel.Fair)] // 75% accuracy
    [InlineData(50, 15, 20, 15, ValidationAccuracyLevel.Poor)] // 65% accuracy
    [InlineData(40, 10, 25, 25, ValidationAccuracyLevel.Critical)] // 50% accuracy
    public void GenerateAccuracyReport_WithVaryingMetrics_ReturnsCorrectQualityLevel(
        int tp, int tn, int fp, int fn, ValidationAccuracyLevel expectedLevel)
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(tp, tn, fp, fn);

        // Act
        var report = _service.GenerateAccuracyReport(metrics, "Test Context");

        // Assert
        Assert.Equal(expectedLevel, report.QualityLevel);
    }
}
