using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Unit tests for ValidationAccuracyMetrics value object.
/// BGAI-039: Validation accuracy baseline measurement.
/// </summary>
public class ValidationAccuracyMetricsTests
{
    [Fact]
    public void Create_WithValidValues_ReturnsMetrics()
    {
        // Arrange
        var tp = 80;
        var tn = 15;
        var fp = 3;
        var fn = 2;

        // Act
        var metrics = ValidationAccuracyMetrics.Create(tp, tn, fp, fn);

        // Assert
        Assert.Equal(80, metrics.TruePositives);
        Assert.Equal(15, metrics.TrueNegatives);
        Assert.Equal(3, metrics.FalsePositives);
        Assert.Equal(2, metrics.FalseNegatives);
        Assert.Equal(100, metrics.Total);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, -1, 0, 0)]
    [InlineData(0, 0, -1, 0)]
    [InlineData(0, 0, 0, -1)]
    public void Create_WithNegativeValues_ThrowsArgumentException(int tp, int tn, int fp, int fn)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => ValidationAccuracyMetrics.Create(tp, tn, fp, fn));
    }

    [Fact]
    public void Precision_WithValidMetrics_CalculatesCorrectly()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 80,  // Correctly identified as valid
            trueNegatives: 15,  // Correctly identified as invalid
            falsePositives: 5,  // Incorrectly identified as valid
            falseNegatives: 0); // Incorrectly identified as invalid

        // Act
        var precision = metrics.Precision;

        // Assert
        // Precision = TP / (TP + FP) = 80 / (80 + 5) = 80 / 85 = 0.9412
        Assert.Equal(0.9412, precision, precision: 4);
    }

    [Fact]
    public void Precision_WithZeroDenominator_ReturnsZero()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(0, 10, 0, 5);

        // Act
        var precision = metrics.Precision;

        // Assert
        Assert.Equal(0.0, precision);
    }

    [Fact]
    public void Recall_WithValidMetrics_CalculatesCorrectly()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 80,  // Correctly identified as valid
            trueNegatives: 15,  // Correctly identified as invalid
            falsePositives: 5,  // Incorrectly identified as valid
            falseNegatives: 10); // Incorrectly identified as invalid

        // Act
        var recall = metrics.Recall;

        // Assert
        // Recall = TP / (TP + FN) = 80 / (80 + 10) = 80 / 90 = 0.8889
        Assert.Equal(0.8889, recall, precision: 4);
    }

    [Fact]
    public void Recall_WithZeroDenominator_ReturnsZero()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(0, 10, 5, 0);

        // Act
        var recall = metrics.Recall;

        // Assert
        Assert.Equal(0.0, recall);
    }

    [Fact]
    public void F1Score_WithValidMetrics_CalculatesCorrectly()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 80,
            trueNegatives: 15,
            falsePositives: 5,
            falseNegatives: 10);

        // Act
        var f1 = metrics.F1Score;

        // Assert
        // Precision = 80 / 85 = 0.9412
        // Recall = 80 / 90 = 0.8889
        // F1 = 2 * (0.9412 * 0.8889) / (0.9412 + 0.8889) = 0.9143
        Assert.Equal(0.9143, f1, precision: 4);
    }

    [Fact]
    public void F1Score_WithZeroPrecisionAndRecall_ReturnsZero()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(0, 10, 0, 0);

        // Act
        var f1 = metrics.F1Score;

        // Assert
        Assert.Equal(0.0, f1);
    }

    [Fact]
    public void Accuracy_WithValidMetrics_CalculatesCorrectly()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 85,  // Correct positives
            trueNegatives: 10,  // Correct negatives
            falsePositives: 3,  // Wrong positives
            falseNegatives: 2); // Wrong negatives

        // Act
        var accuracy = metrics.Accuracy;

        // Assert
        // Accuracy = (TP + TN) / Total = (85 + 10) / 100 = 95 / 100 = 0.95
        Assert.Equal(0.95, accuracy);
    }

    [Fact]
    public void Accuracy_WithZeroTotal_ReturnsZero()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(0, 0, 0, 0);

        // Act
        var accuracy = metrics.Accuracy;

        // Assert
        Assert.Equal(0.0, accuracy);
    }

    [Fact]
    public void Specificity_WithValidMetrics_CalculatesCorrectly()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 80,
            trueNegatives: 15,  // Correctly identified as invalid
            falsePositives: 5,  // Incorrectly identified as valid
            falseNegatives: 10);

        // Act
        var specificity = metrics.Specificity;

        // Assert
        // Specificity = TN / (TN + FP) = 15 / (15 + 5) = 15 / 20 = 0.75
        Assert.Equal(0.75, specificity);
    }

    [Fact]
    public void MatthewsCorrelationCoefficient_WithValidMetrics_CalculatesCorrectly()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 50,
            trueNegatives: 40,
            falsePositives: 10,
            falseNegatives: 5);

        // Act
        var mcc = metrics.MatthewsCorrelationCoefficient;

        // Assert
        // MCC = (TP*TN - FP*FN) / sqrt((TP+FP)(TP+FN)(TN+FP)(TN+FN))
        // MCC = (50*40 - 10*5) / sqrt((60)(55)(50)(45))
        // MCC = (2000 - 50) / sqrt(7425000) = 1950 / 2724.9 ≈ 0.7155
        Assert.InRange(mcc, 0.71, 0.72);
    }

    [Fact]
    public void MatthewsCorrelationCoefficient_WithZeroDenominator_ReturnsZero()
    {
        // Arrange
        var metrics = ValidationAccuracyMetrics.Create(0, 0, 0, 0);

        // Act
        var mcc = metrics.MatthewsCorrelationCoefficient;

        // Assert
        Assert.Equal(0.0, mcc);
    }

    [Theory]
    [InlineData(0.95, true)]
    [InlineData(0.80, true)]
    [InlineData(0.79, false)]
    [InlineData(0.50, false)]
    public void MeetsBaselineThreshold_WithVaryingAccuracy_ReturnsCorrectResult(double accuracy, bool expectedMeets)
    {
        // Arrange
        var total = 100;
        var correctCount = (int)(accuracy * total);
        var incorrectCount = total - correctCount;

        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: correctCount,
            trueNegatives: 0,
            falsePositives: incorrectCount,
            falseNegatives: 0);

        // Act
        var meetsBaseline = metrics.MeetsBaselineThreshold;

        // Assert
        Assert.Equal(expectedMeets, meetsBaseline);
    }

    [Theory]
    [InlineData(0.95, ValidationAccuracyLevel.Excellent)]
    [InlineData(0.92, ValidationAccuracyLevel.VeryGood)]
    [InlineData(0.85, ValidationAccuracyLevel.Good)]
    [InlineData(0.75, ValidationAccuracyLevel.Fair)]
    [InlineData(0.65, ValidationAccuracyLevel.Poor)]
    [InlineData(0.50, ValidationAccuracyLevel.Critical)]
    public void QualityLevel_WithVaryingAccuracy_ReturnsCorrectLevel(double accuracy, ValidationAccuracyLevel expectedLevel)
    {
        // Arrange
        var total = 100;
        var correctCount = (int)(accuracy * total);
        var incorrectCount = total - correctCount;

        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: correctCount,
            trueNegatives: 0,
            falsePositives: incorrectCount,
            falseNegatives: 0);

        // Act
        var level = metrics.QualityLevel;

        // Assert
        Assert.Equal(expectedLevel, level);
    }

    [Fact]
    public void Empty_ReturnsAllZeros()
    {
        // Act
        var metrics = ValidationAccuracyMetrics.Empty;

        // Assert
        Assert.Equal(0, metrics.TruePositives);
        Assert.Equal(0, metrics.TrueNegatives);
        Assert.Equal(0, metrics.FalsePositives);
        Assert.Equal(0, metrics.FalseNegatives);
        Assert.Equal(0, metrics.Total);
        Assert.Equal(0.0, metrics.Precision);
        Assert.Equal(0.0, metrics.Recall);
        Assert.Equal(0.0, metrics.F1Score);
        Assert.Equal(0.0, metrics.Accuracy);
    }

    [Fact]
    public void PerfectMetrics_AllCorrect_ReturnsOnePointZero()
    {
        // Arrange - Perfect classification (all correct)
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 90,
            trueNegatives: 10,
            falsePositives: 0,
            falseNegatives: 0);

        // Act & Assert
        Assert.Equal(1.0, metrics.Precision);
        Assert.Equal(1.0, metrics.Recall);
        Assert.Equal(1.0, metrics.F1Score);
        Assert.Equal(1.0, metrics.Accuracy);
        Assert.Equal(1.0, metrics.Specificity);
        Assert.True(metrics.MeetsBaselineThreshold);
        Assert.Equal(ValidationAccuracyLevel.Excellent, metrics.QualityLevel);
    }

    [Fact]
    public void WorstMetrics_AllWrong_ReturnsZero()
    {
        // Arrange - Worst classification (all wrong)
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 0,
            trueNegatives: 0,
            falsePositives: 10,
            falseNegatives: 90);

        // Act & Assert
        Assert.Equal(0.0, metrics.Precision);
        Assert.Equal(0.0, metrics.Recall);
        Assert.Equal(0.0, metrics.F1Score);
        Assert.Equal(0.0, metrics.Accuracy);
        Assert.Equal(0.0, metrics.Specificity);
        Assert.False(metrics.MeetsBaselineThreshold);
        Assert.Equal(ValidationAccuracyLevel.Critical, metrics.QualityLevel);
    }

    [Fact]
    public void BaselineThreshold_ExactlyAtThreshold_MeetsBaseline()
    {
        // Arrange - Exactly 80% accuracy
        var metrics = ValidationAccuracyMetrics.Create(
            truePositives: 80,
            trueNegatives: 0,
            falsePositives: 20,
            falseNegatives: 0);

        // Act & Assert
        Assert.Equal(0.80, metrics.Accuracy);
        Assert.True(metrics.MeetsBaselineThreshold);
        Assert.Equal(ValidationAccuracyLevel.Good, metrics.QualityLevel);
    }
}

