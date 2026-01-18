using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Enums;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Unit tests for E2EMetrics value object.
/// Issue #2381: Comprehensive validation logic testing for E2E testing metrics.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class E2EMetricsTests
{
    #region CreateDefault Tests

    [Fact]
    public void CreateDefault_ReturnsZeroMetrics()
    {
        // Act
        var metrics = E2EMetrics.CreateDefault();

        // Assert
        Assert.Equal(0m, metrics.Coverage);
        Assert.Equal(0m, metrics.PassRate);
        Assert.Equal(0m, metrics.FlakyRate);
        Assert.Equal(0m, metrics.ExecutionTime);
        Assert.Equal(0, metrics.TotalTests);
        Assert.Equal(0, metrics.PassedTests);
        Assert.Equal(0, metrics.FailedTests);
        Assert.Equal(0, metrics.SkippedTests);
        Assert.Equal(0, metrics.FlakyTests);
        Assert.Equal(TestExecutionStatus.NoData, metrics.Status);
    }

    [Fact]
    public void Empty_IsSameAsCreateDefault()
    {
        // Act
        var empty = E2EMetrics.Empty;

        // Assert
        Assert.Equal(0m, empty.Coverage);
        Assert.Equal(0m, empty.PassRate);
        Assert.Equal(TestExecutionStatus.NoData, empty.Status);
    }

    #endregion

    #region FromTestRun Factory Method Tests

    [Fact]
    public void FromTestRun_PerfectResults_CreatesPassStatus()
    {
        // Arrange - 100% pass rate, 0% flaky
        var lastRun = DateTime.UtcNow;

        // Act
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 100,
            failedTests: 0,
            skippedTests: 0,
            flakyTests: 0,
            durationMs: 5000,
            lastRunAt: lastRun);

        // Assert
        Assert.Equal(100m, metrics.Coverage);
        Assert.Equal(100m, metrics.PassRate);
        Assert.Equal(0m, metrics.FlakyRate);
        Assert.Equal(50m, metrics.ExecutionTime); // 5000ms / 100 tests
        Assert.Equal(100, metrics.TotalTests);
        Assert.Equal(100, metrics.PassedTests);
        Assert.Equal(0, metrics.FailedTests);
        Assert.Equal(0, metrics.SkippedTests);
        Assert.Equal(0, metrics.FlakyTests);
        Assert.Equal(lastRun, metrics.LastRunAt);
        Assert.Equal(TestExecutionStatus.Pass, metrics.Status);
    }

    [Fact]
    public void FromTestRun_95PercentPassRate_CreatesPassStatus()
    {
        // Arrange - Exactly at pass threshold (95% pass, 5% flaky)
        // Act
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 95,
            failedTests: 5,
            skippedTests: 0,
            flakyTests: 5,
            durationMs: 1000,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(95m, metrics.PassRate);
        Assert.Equal(5m, metrics.FlakyRate);
        Assert.Equal(TestExecutionStatus.Pass, metrics.Status);
    }

    [Fact]
    public void FromTestRun_94PercentPassRate_CreatesWarningStatus()
    {
        // Arrange - Just below pass threshold (94% pass, 6% flaky)
        // Act
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 94,
            failedTests: 6,
            skippedTests: 0,
            flakyTests: 6,
            durationMs: 1000,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(94m, metrics.PassRate);
        Assert.Equal(6m, metrics.FlakyRate);
        Assert.Equal(TestExecutionStatus.Warning, metrics.Status);
    }

    [Fact]
    public void FromTestRun_80PercentPassRate_CreatesWarningStatus()
    {
        // Arrange - At warning threshold (80% pass, 10% flaky)
        // Act
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 80,
            failedTests: 20,
            skippedTests: 0,
            flakyTests: 10,
            durationMs: 1000,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(80m, metrics.PassRate);
        Assert.Equal(10m, metrics.FlakyRate);
        Assert.Equal(TestExecutionStatus.Warning, metrics.Status);
    }

    [Fact]
    public void FromTestRun_79PercentPassRate_CreatesFailStatus()
    {
        // Arrange - Below warning threshold
        // Act
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 79,
            failedTests: 21,
            skippedTests: 0,
            flakyTests: 0,
            durationMs: 1000,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(79m, metrics.PassRate);
        Assert.Equal(TestExecutionStatus.Fail, metrics.Status);
    }

    [Fact]
    public void FromTestRun_HighFlakyRate_CreatesFailStatus()
    {
        // Arrange - High pass rate but excessive flakiness (95% pass, 11% flaky)
        // Act
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 95,
            failedTests: 5,
            skippedTests: 0,
            flakyTests: 11,
            durationMs: 1000,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(95m, metrics.PassRate);
        Assert.Equal(11m, metrics.FlakyRate);
        Assert.Equal(TestExecutionStatus.Fail, metrics.Status);
    }

    [Fact]
    public void FromTestRun_ZeroTests_CreatesZeroMetrics()
    {
        // Act
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 0,
            passedTests: 0,
            failedTests: 0,
            skippedTests: 0,
            flakyTests: 0,
            durationMs: 0,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(0m, metrics.Coverage);
        Assert.Equal(0m, metrics.PassRate);
        Assert.Equal(0m, metrics.FlakyRate);
        Assert.Equal(0m, metrics.ExecutionTime);
        Assert.Equal(TestExecutionStatus.Fail, metrics.Status);
    }

    [Fact]
    public void FromTestRun_WithSkippedTests_CalculatesCorrectly()
    {
        // Arrange - 10 skipped tests shouldn't affect pass rate calculation
        // Act
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 90,
            failedTests: 0,
            skippedTests: 10,
            flakyTests: 0,
            durationMs: 2000,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.Equal(90m, metrics.PassRate); // passedTests / totalTests * 100
        Assert.Equal(10, metrics.SkippedTests);
        Assert.Equal(TestExecutionStatus.Warning, metrics.Status);
    }

    #endregion

    #region MeetsQualityStandards Tests

    [Fact]
    public void MeetsQualityStandards_AllThresholdsMet_ReturnsTrue()
    {
        // Arrange - Coverage >= 90%, PassRate >= 95%, FlakyRate <= 5%
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 96,
            failedTests: 4,
            skippedTests: 0,
            flakyTests: 4,
            durationMs: 1000,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.True(metrics.MeetsQualityStandards);
    }

    [Fact]
    public void MeetsQualityStandards_LowCoverage_ReturnsFalse()
    {
        // Arrange - Coverage below 90% (passedTests/totalTests determines coverage)
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 89,
            failedTests: 11,
            skippedTests: 0,
            flakyTests: 0,
            durationMs: 1000,
            lastRunAt: DateTime.UtcNow);

        // Assert - Coverage = 89% (< 90% threshold)
        Assert.False(metrics.MeetsQualityStandards);
    }

    [Fact]
    public void MeetsQualityStandards_LowPassRate_ReturnsFalse()
    {
        // Arrange - PassRate below 95%
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 94,
            failedTests: 6,
            skippedTests: 0,
            flakyTests: 0,
            durationMs: 1000,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.False(metrics.MeetsQualityStandards);
    }

    [Fact]
    public void MeetsQualityStandards_HighFlakyRate_ReturnsFalse()
    {
        // Arrange - FlakyRate above 5%
        var metrics = E2EMetrics.FromTestRun(
            totalTests: 100,
            passedTests: 96,
            failedTests: 4,
            skippedTests: 0,
            flakyTests: 6,
            durationMs: 1000,
            lastRunAt: DateTime.UtcNow);

        // Assert
        Assert.False(metrics.MeetsQualityStandards);
    }

    #endregion

    #region Validation Tests - Negative Values

    [Fact]
    public void FromTestRun_NegativeTotalTests_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            E2EMetrics.FromTestRun(-1, 0, 0, 0, 0, 0, DateTime.UtcNow));

        Assert.Contains("totalTests", exception.Message);
    }

    [Fact]
    public void FromTestRun_NegativePassedTests_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            E2EMetrics.FromTestRun(10, -1, 0, 0, 0, 0, DateTime.UtcNow));

        Assert.Contains("passedTests", exception.Message);
    }

    [Fact]
    public void FromTestRun_NegativeFailedTests_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            E2EMetrics.FromTestRun(10, 0, -1, 0, 0, 0, DateTime.UtcNow));

        Assert.Contains("failedTests", exception.Message);
    }

    [Fact]
    public void FromTestRun_NegativeSkippedTests_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            E2EMetrics.FromTestRun(10, 0, 0, -1, 0, 0, DateTime.UtcNow));

        Assert.Contains("skippedTests", exception.Message);
    }

    [Fact]
    public void FromTestRun_NegativeFlakyTests_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            E2EMetrics.FromTestRun(10, 0, 0, 0, -1, 0, DateTime.UtcNow));

        Assert.Contains("flakyTests", exception.Message);
    }

    [Fact]
    public void FromTestRun_NegativeDuration_ThrowsValidationException()
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            E2EMetrics.FromTestRun(10, 10, 0, 0, 0, -1, DateTime.UtcNow));

        Assert.Contains("durationMs", exception.Message);
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics1 = E2EMetrics.FromTestRun(100, 95, 5, 0, 3, 1000, now);
        var metrics2 = E2EMetrics.FromTestRun(100, 95, 5, 0, 3, 1000, now);

        // Assert
        Assert.Equal(metrics1, metrics2);
        Assert.True(metrics1 == metrics2);
        Assert.False(metrics1 != metrics2);
    }

    [Fact]
    public void Equality_DifferentPassRate_AreNotEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics1 = E2EMetrics.FromTestRun(100, 95, 5, 0, 3, 1000, now);
        var metrics2 = E2EMetrics.FromTestRun(100, 90, 10, 0, 3, 1000, now);

        // Assert
        Assert.NotEqual(metrics1, metrics2);
        Assert.False(metrics1 == metrics2);
        Assert.True(metrics1 != metrics2);
    }

    [Fact]
    public void Equality_DifferentTimestamp_AreNotEqual()
    {
        // Arrange
        var metrics1 = E2EMetrics.FromTestRun(100, 95, 5, 0, 3, 1000, DateTime.UtcNow);
        var metrics2 = E2EMetrics.FromTestRun(100, 95, 5, 0, 3, 1000, DateTime.UtcNow.AddMinutes(1));

        // Assert
        Assert.NotEqual(metrics1, metrics2);
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ContainsKeyMetrics()
    {
        // Arrange
        var metrics = E2EMetrics.FromTestRun(100, 95, 5, 0, 3, 1000, DateTime.UtcNow);

        // Act
        var result = metrics.ToString();

        // Assert
        Assert.Contains("Coverage=", result);
        Assert.Contains("Pass=", result);
        Assert.Contains("Flaky=", result);
        Assert.Contains("Status=", result);
        Assert.StartsWith("E2E:", result);
    }

    [Fact]
    public void ToString_FormatsPercentagesCorrectly()
    {
        // Arrange
        var metrics = E2EMetrics.FromTestRun(100, 95, 5, 0, 5, 1000, DateTime.UtcNow);

        // Act
        var result = metrics.ToString();

        // Assert - Check for percentages with culture-independent assertion
        Assert.Contains("Pass=95", result);
        Assert.Contains("Flaky=5", result);
    }

    #endregion
}
