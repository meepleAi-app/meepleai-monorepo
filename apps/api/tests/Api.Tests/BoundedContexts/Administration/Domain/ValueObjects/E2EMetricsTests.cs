using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Enums;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

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
        metrics.Coverage.Should().Be(0m);
        metrics.PassRate.Should().Be(0m);
        metrics.FlakyRate.Should().Be(0m);
        metrics.ExecutionTime.Should().Be(0m);
        metrics.TotalTests.Should().Be(0);
        metrics.PassedTests.Should().Be(0);
        metrics.FailedTests.Should().Be(0);
        metrics.SkippedTests.Should().Be(0);
        metrics.FlakyTests.Should().Be(0);
        metrics.Status.Should().Be(TestExecutionStatus.NoData);
    }

    [Fact]
    public void Empty_IsSameAsCreateDefault()
    {
        // Act
        var empty = E2EMetrics.Empty;

        // Assert
        empty.Coverage.Should().Be(0m);
        empty.PassRate.Should().Be(0m);
        empty.Status.Should().Be(TestExecutionStatus.NoData);
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
        metrics.Coverage.Should().Be(100m);
        metrics.PassRate.Should().Be(100m);
        metrics.FlakyRate.Should().Be(0m);
        metrics.ExecutionTime.Should().Be(50m); // 5000ms / 100 tests
        metrics.TotalTests.Should().Be(100);
        metrics.PassedTests.Should().Be(100);
        metrics.FailedTests.Should().Be(0);
        metrics.SkippedTests.Should().Be(0);
        metrics.FlakyTests.Should().Be(0);
        metrics.LastRunAt.Should().Be(lastRun);
        metrics.Status.Should().Be(TestExecutionStatus.Pass);
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
        metrics.PassRate.Should().Be(95m);
        metrics.FlakyRate.Should().Be(5m);
        metrics.Status.Should().Be(TestExecutionStatus.Pass);
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
        metrics.PassRate.Should().Be(94m);
        metrics.FlakyRate.Should().Be(6m);
        metrics.Status.Should().Be(TestExecutionStatus.Warning);
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
        metrics.PassRate.Should().Be(80m);
        metrics.FlakyRate.Should().Be(10m);
        metrics.Status.Should().Be(TestExecutionStatus.Warning);
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
        metrics.PassRate.Should().Be(79m);
        metrics.Status.Should().Be(TestExecutionStatus.Fail);
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
        metrics.PassRate.Should().Be(95m);
        metrics.FlakyRate.Should().Be(11m);
        metrics.Status.Should().Be(TestExecutionStatus.Fail);
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
        metrics.Coverage.Should().Be(0m);
        metrics.PassRate.Should().Be(0m);
        metrics.FlakyRate.Should().Be(0m);
        metrics.ExecutionTime.Should().Be(0m);
        metrics.Status.Should().Be(TestExecutionStatus.Fail);
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
        metrics.PassRate.Should().Be(90m); // passedTests / totalTests * 100
        metrics.SkippedTests.Should().Be(10);
        metrics.Status.Should().Be(TestExecutionStatus.Warning);
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
        metrics.MeetsQualityStandards.Should().BeTrue();
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
        metrics.MeetsQualityStandards.Should().BeFalse();
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
        metrics.MeetsQualityStandards.Should().BeFalse();
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
        metrics.MeetsQualityStandards.Should().BeFalse();
    }

    #endregion

    #region Validation Tests - Negative Values

    [Fact]
    public void FromTestRun_NegativeTotalTests_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            E2EMetrics.FromTestRun(-1, 0, 0, 0, 0, 0, DateTime.UtcNow);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("totalTests");
    }

    [Fact]
    public void FromTestRun_NegativePassedTests_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            E2EMetrics.FromTestRun(10, -1, 0, 0, 0, 0, DateTime.UtcNow);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("passedTests");
    }

    [Fact]
    public void FromTestRun_NegativeFailedTests_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            E2EMetrics.FromTestRun(10, 0, -1, 0, 0, 0, DateTime.UtcNow);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("failedTests");
    }

    [Fact]
    public void FromTestRun_NegativeSkippedTests_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            E2EMetrics.FromTestRun(10, 0, 0, -1, 0, 0, DateTime.UtcNow);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("skippedTests");
    }

    [Fact]
    public void FromTestRun_NegativeFlakyTests_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            E2EMetrics.FromTestRun(10, 0, 0, 0, -1, 0, DateTime.UtcNow);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("flakyTests");
    }

    [Fact]
    public void FromTestRun_NegativeDuration_ThrowsValidationException()
    {
        // Act & Assert
        var act = () =>
            E2EMetrics.FromTestRun(10, 10, 0, 0, 0, -1, DateTime.UtcNow);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("durationMs");
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
        metrics2.Should().Be(metrics1);
        (metrics1 == metrics2).Should().BeTrue();
        (metrics1 != metrics2).Should().BeFalse();
    }

    [Fact]
    public void Equality_DifferentPassRate_AreNotEqual()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var metrics1 = E2EMetrics.FromTestRun(100, 95, 5, 0, 3, 1000, now);
        var metrics2 = E2EMetrics.FromTestRun(100, 90, 10, 0, 3, 1000, now);

        // Assert
        metrics2.Should().NotBe(metrics1);
        (metrics1 == metrics2).Should().BeFalse();
        (metrics1 != metrics2).Should().BeTrue();
    }

    [Fact]
    public void Equality_DifferentTimestamp_AreNotEqual()
    {
        // Arrange
        var metrics1 = E2EMetrics.FromTestRun(100, 95, 5, 0, 3, 1000, DateTime.UtcNow);
        var metrics2 = E2EMetrics.FromTestRun(100, 95, 5, 0, 3, 1000, DateTime.UtcNow.AddMinutes(1));

        // Assert
        metrics2.Should().NotBe(metrics1);
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
        result.Should().Contain("Coverage=");
        result.Should().Contain("Pass=");
        result.Should().Contain("Flaky=");
        result.Should().Contain("Status=");
        result.Should().StartWith("E2E:");
    }

    [Fact]
    public void ToString_FormatsPercentagesCorrectly()
    {
        // Arrange
        var metrics = E2EMetrics.FromTestRun(100, 95, 5, 0, 5, 1000, DateTime.UtcNow);

        // Act
        var result = metrics.ToString();

        // Assert - Check for percentages with culture-independent assertion
        result.Should().Contain("Pass=95");
        result.Should().Contain("Flaky=5");
    }

    #endregion
}