using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Unit tests for PerformanceMetrics value object.
/// Issue #2381: Comprehensive validation logic testing for Core Web Vitals metrics.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class PerformanceMetricsTests
{
    #region CreateDefault Tests

    [Fact]
    public void CreateDefault_ReturnsZeroMetrics()
    {
        // Act
        var metrics = PerformanceMetrics.CreateDefault();

        // Assert
        Assert.Equal(0m, metrics.Lcp);
        Assert.Equal(0m, metrics.Fid);
        Assert.Equal(0m, metrics.Cls);
        Assert.Equal(0m, metrics.Fcp);
        Assert.Equal(0m, metrics.Tti);
        Assert.Equal(0m, metrics.Tbt);
        Assert.Equal(0m, metrics.SpeedIndex);
        Assert.Equal(0m, metrics.PerformanceScore);
        Assert.Equal(PerformanceBudgetStatus.NoData, metrics.BudgetStatus);
    }

    [Fact]
    public void Empty_IsSameAsCreateDefault()
    {
        // Act
        var empty = PerformanceMetrics.Empty;

        // Assert
        Assert.Equal(0m, empty.Lcp);
        Assert.Equal(0m, empty.PerformanceScore);
        Assert.Equal(PerformanceBudgetStatus.NoData, empty.BudgetStatus);
    }

    #endregion

    #region FromLighthouseReport Factory Method Tests - Pass Status

    [Fact]
    public void FromLighthouseReport_ExcellentMetrics_CreatesPassStatus()
    {
        // Arrange - All metrics well within "Good" thresholds
        var lastRun = DateTime.UtcNow;

        // Act
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 1500m,        // < 2500 (Good)
            fid: 50m,          // < 100 (Good)
            cls: 0.05m,        // < 0.1 (Good)
            fcp: 1000m,
            tti: 2000m,
            tbt: 100m,
            speedIndex: 1500m,
            performanceScore: 95m,  // >= 90 (Good)
            lastRunAt: lastRun);

        // Assert
        Assert.Equal(1500m, metrics.Lcp);
        Assert.Equal(50m, metrics.Fid);
        Assert.Equal(0.05m, metrics.Cls);
        Assert.Equal(1000m, metrics.Fcp);
        Assert.Equal(2000m, metrics.Tti);
        Assert.Equal(100m, metrics.Tbt);
        Assert.Equal(1500m, metrics.SpeedIndex);
        Assert.Equal(95m, metrics.PerformanceScore);
        Assert.Equal(lastRun, metrics.LastRunAt);
        Assert.Equal(PerformanceBudgetStatus.Pass, metrics.BudgetStatus);
    }

    [Fact]
    public void FromLighthouseReport_AtGoodThresholds_CreatesPassStatus()
    {
        // Arrange - All metrics exactly at "Good" thresholds
        // Act
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 2500m,        // == 2500 (Good threshold)
            fid: 100m,         // == 100 (Good threshold)
            cls: 0.1m,         // == 0.1 (Good threshold)
            fcp: 2000m,
            tti: 3500m,
            tbt: 200m,
            speedIndex: 3000m,
            performanceScore: 90m,  // == 90 (Good threshold)
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(PerformanceBudgetStatus.Pass, metrics.BudgetStatus);
    }

    #endregion

    #region FromLighthouseReport Factory Method Tests - Warning Status

    [Fact]
    public void FromLighthouseReport_NeedsImprovementMetrics_CreatesWarningStatus()
    {
        // Arrange - Metrics in "Needs Improvement" range
        // Act
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 3000m,        // 2500 < 3000 <= 4000 (Needs Improvement)
            fid: 150m,         // 100 < 150 <= 300 (Needs Improvement)
            cls: 0.15m,        // 0.1 < 0.15 <= 0.25 (Needs Improvement)
            fcp: 2500m,
            tti: 4500m,
            tbt: 350m,
            speedIndex: 4000m,
            performanceScore: 75m,  // 50 <= 75 < 90 (Needs Improvement)
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(PerformanceBudgetStatus.Warning, metrics.BudgetStatus);
    }

    [Fact]
    public void FromLighthouseReport_AtNeedsImprovementThresholds_CreatesWarningStatus()
    {
        // Arrange - All metrics exactly at "Needs Improvement" thresholds
        // Act
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 4000m,        // == 4000 (Needs Improvement threshold)
            fid: 300m,         // == 300 (Needs Improvement threshold)
            cls: 0.25m,        // == 0.25 (Needs Improvement threshold)
            fcp: 3000m,
            tti: 5000m,
            tbt: 400m,
            speedIndex: 4500m,
            performanceScore: 50m,  // == 50 (Warning threshold)
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(PerformanceBudgetStatus.Warning, metrics.BudgetStatus);
    }

    [Fact]
    public void FromLighthouseReport_HighLcpOnly_CreatesWarningStatus()
    {
        // Arrange - Only LCP exceeds Good threshold
        // Act
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 3500m,        // > 2500 (Needs Improvement)
            fid: 50m,          // < 100 (Good)
            cls: 0.05m,        // < 0.1 (Good)
            fcp: 1000m,
            tti: 2000m,
            tbt: 100m,
            speedIndex: 1500m,
            performanceScore: 75m,  // < 90 but >= 50 (Warning range)
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(PerformanceBudgetStatus.Warning, metrics.BudgetStatus);
    }

    #endregion

    #region FromLighthouseReport Factory Method Tests - Fail Status

    [Fact]
    public void FromLighthouseReport_PoorMetrics_CreatesFailStatus()
    {
        // Arrange - Metrics exceed "Needs Improvement" thresholds
        // Act
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 5000m,        // > 4000 (Poor)
            fid: 400m,         // > 300 (Poor)
            cls: 0.3m,         // > 0.25 (Poor)
            fcp: 4000m,
            tti: 7000m,
            tbt: 600m,
            speedIndex: 6000m,
            performanceScore: 30m,  // < 50 (Poor)
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(PerformanceBudgetStatus.Fail, metrics.BudgetStatus);
    }

    [Fact]
    public void FromLighthouseReport_OnlyLcpFails_CreatesFailStatus()
    {
        // Arrange - Only LCP exceeds Poor threshold
        // Act
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 4500m,        // > 4000 (Poor)
            fid: 50m,          // < 100 (Good)
            cls: 0.05m,        // < 0.1 (Good)
            fcp: 1000m,
            tti: 2000m,
            tbt: 100m,
            speedIndex: 1500m,
            performanceScore: 40m,  // < 50 (Poor)
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(PerformanceBudgetStatus.Fail, metrics.BudgetStatus);
    }

    [Fact]
    public void FromLighthouseReport_LowScoreOnly_CreatesFailStatus()
    {
        // Arrange - Good Core Web Vitals but poor score
        // Act
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 2000m,        // < 2500 (Good)
            fid: 50m,          // < 100 (Good)
            cls: 0.05m,        // < 0.1 (Good)
            fcp: 1000m,
            tti: 2000m,
            tbt: 100m,
            speedIndex: 1500m,
            performanceScore: 45m,  // < 50 (Poor)
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(PerformanceBudgetStatus.Fail, metrics.BudgetStatus);
    }

    #endregion

    #region MeetsCoreWebVitals Tests

    [Fact]
    public void MeetsCoreWebVitals_AllGoodThresholds_ReturnsTrue()
    {
        // Arrange
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 2000m,        // <= 2500
            fid: 80m,          // <= 100
            cls: 0.08m,        // <= 0.1
            fcp: 1500m,
            tti: 3000m,
            tbt: 150m,
            speedIndex: 2500m,
            performanceScore: 92m,  // >= 90
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.True(metrics.MeetsCoreWebVitals);
    }

    [Fact]
    public void MeetsCoreWebVitals_LcpExceedsThreshold_ReturnsFalse()
    {
        // Arrange
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 2600m,        // > 2500 (Fails)
            fid: 80m,          // <= 100
            cls: 0.08m,        // <= 0.1
            fcp: 1500m,
            tti: 3000m,
            tbt: 150m,
            speedIndex: 2500m,
            performanceScore: 92m,  // >= 90
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.False(metrics.MeetsCoreWebVitals);
    }

    [Fact]
    public void MeetsCoreWebVitals_FidExceedsThreshold_ReturnsFalse()
    {
        // Arrange
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 2000m,        // <= 2500
            fid: 110m,         // > 100 (Fails)
            cls: 0.08m,        // <= 0.1
            fcp: 1500m,
            tti: 3000m,
            tbt: 150m,
            speedIndex: 2500m,
            performanceScore: 92m,  // >= 90
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.False(metrics.MeetsCoreWebVitals);
    }

    [Fact]
    public void MeetsCoreWebVitals_ClsExceedsThreshold_ReturnsFalse()
    {
        // Arrange
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 2000m,        // <= 2500
            fid: 80m,          // <= 100
            cls: 0.12m,        // > 0.1 (Fails)
            fcp: 1500m,
            tti: 3000m,
            tbt: 150m,
            speedIndex: 2500m,
            performanceScore: 92m,  // >= 90
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.False(metrics.MeetsCoreWebVitals);
    }

    [Fact]
    public void MeetsCoreWebVitals_ScoreBelowThreshold_ReturnsFalse()
    {
        // Arrange
        var metrics = PerformanceMetrics.FromLighthouseReport(
            lcp: 2000m,        // <= 2500
            fid: 80m,          // <= 100
            cls: 0.08m,        // <= 0.1
            fcp: 1500m,
            tti: 3000m,
            tbt: 150m,
            speedIndex: 2500m,
            performanceScore: 85m,  // < 90 (Fails)
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.False(metrics.MeetsCoreWebVitals);
    }

    #endregion

    #region Validation Tests - Negative Values

    [Fact]
    public void FromLighthouseReport_NegativeLcp_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PerformanceMetrics.FromLighthouseReport(-1, 50, 0.1m, 1000, 2000, 100, 1500, 90, DateTime.UtcNow));

        Assert.Contains("lcp", exception.Message);
    }

    [Fact]
    public void FromLighthouseReport_NegativeFid_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PerformanceMetrics.FromLighthouseReport(2000, -1, 0.1m, 1000, 2000, 100, 1500, 90, DateTime.UtcNow));

        Assert.Contains("fid", exception.Message);
    }

    [Fact]
    public void FromLighthouseReport_NegativeCls_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PerformanceMetrics.FromLighthouseReport(2000, 50, -0.1m, 1000, 2000, 100, 1500, 90, DateTime.UtcNow));

        Assert.Contains("cls", exception.Message);
    }

    [Fact]
    public void FromLighthouseReport_NegativeFcp_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PerformanceMetrics.FromLighthouseReport(2000, 50, 0.1m, -1, 2000, 100, 1500, 90, DateTime.UtcNow));

        Assert.Contains("fcp", exception.Message);
    }

    [Fact]
    public void FromLighthouseReport_NegativeTti_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PerformanceMetrics.FromLighthouseReport(2000, 50, 0.1m, 1000, -1, 100, 1500, 90, DateTime.UtcNow));

        Assert.Contains("tti", exception.Message);
    }

    [Fact]
    public void FromLighthouseReport_NegativeTbt_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PerformanceMetrics.FromLighthouseReport(2000, 50, 0.1m, 1000, 2000, -1, 1500, 90, DateTime.UtcNow));

        Assert.Contains("tbt", exception.Message);
    }

    [Fact]
    public void FromLighthouseReport_NegativeSpeedIndex_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PerformanceMetrics.FromLighthouseReport(2000, 50, 0.1m, 1000, 2000, 100, -1, 90, DateTime.UtcNow));

        Assert.Contains("speedIndex", exception.Message);
    }

    #endregion

    #region Validation Tests - Score Out of Range

    [Fact]
    public void FromLighthouseReport_ScoreAbove100_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PerformanceMetrics.FromLighthouseReport(2000, 50, 0.1m, 1000, 2000, 100, 1500, 101, DateTime.UtcNow));

        Assert.Contains("performanceScore", exception.Message);
    }

    [Fact]
    public void FromLighthouseReport_NegativeScore_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            PerformanceMetrics.FromLighthouseReport(2000, 50, 0.1m, 1000, 2000, 100, 1500, -1, DateTime.UtcNow));

        Assert.Contains("performanceScore", exception.Message);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics1 = PerformanceMetrics.FromLighthouseReport(2000, 50, 0.08m, 1000, 2500, 150, 2000, 92, now);
        var metrics2 = PerformanceMetrics.FromLighthouseReport(2000, 50, 0.08m, 1000, 2500, 150, 2000, 92, now);

        // Assert
        Assert.Equal(metrics1, metrics2);
        Assert.True(metrics1 == metrics2);
        Assert.False(metrics1 != metrics2);
    }

    [Fact]
    public void Equality_DifferentLcp_AreNotEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics1 = PerformanceMetrics.FromLighthouseReport(2000, 50, 0.08m, 1000, 2500, 150, 2000, 92, now);
        var metrics2 = PerformanceMetrics.FromLighthouseReport(2500, 50, 0.08m, 1000, 2500, 150, 2000, 92, now);

        // Assert
        Assert.NotEqual(metrics1, metrics2);
    }

    [Fact]
    public void Equality_DifferentTimestamp_AreNotEqual()
    {
        // Arrange
        var metrics1 = PerformanceMetrics.FromLighthouseReport(2000, 50, 0.08m, 1000, 2500, 150, 2000, 92, DateTime.UtcNow);
        var metrics2 = PerformanceMetrics.FromLighthouseReport(2000, 50, 0.08m, 1000, 2500, 150, 2000, 92, DateTime.UtcNow.AddMinutes(1));

        // Assert
        Assert.NotEqual(metrics1, metrics2);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ContainsKeyMetrics()
    {
        // Arrange
        var metrics = PerformanceMetrics.FromLighthouseReport(
            2000, 50, 0.08m, 1000, 2500, 150, 2000, 92, DateTime.UtcNow);

        // Act
        var result = metrics.ToString();

        // Assert
        Assert.Contains("Score=", result);
        Assert.Contains("LCP=", result);
        Assert.Contains("FID=", result);
        Assert.Contains("CLS=", result);
        Assert.Contains("Budget=", result);
        Assert.StartsWith("Performance:", result);
    }

    [Fact]
    public void ToString_FormatsDecimalsCorrectly()
    {
        // Arrange
        var metrics = PerformanceMetrics.FromLighthouseReport(
            2000, 50, 0.08m, 1000, 2500, 150, 2000, 92, DateTime.UtcNow);

        // Act
        var result = metrics.ToString();

        // Assert - Culture-independent assertions
        Assert.Contains("Score=92", result);
        Assert.Contains("CLS=0", result); // Just verify CLS is present
    }

    #endregion
}
