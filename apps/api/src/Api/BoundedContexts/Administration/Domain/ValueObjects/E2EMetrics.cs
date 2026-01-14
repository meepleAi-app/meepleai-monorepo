using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Guards;

namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value object for E2E testing metrics (Issue #2139)
/// Represents Playwright test execution results
/// </summary>
internal sealed class E2EMetrics : ValueObject
{
    /// <summary>
    /// Test coverage percentage (0-100)
    /// </summary>
    public decimal Coverage { get; }

    /// <summary>
    /// Test pass rate percentage (0-100)
    /// </summary>
    public decimal PassRate { get; }

    /// <summary>
    /// Flaky test rate percentage (0-100)
    /// </summary>
    public decimal FlakyRate { get; }

    /// <summary>
    /// Average test execution time (ms)
    /// </summary>
    public decimal ExecutionTime { get; }

    /// <summary>
    /// Total number of tests
    /// </summary>
    public int TotalTests { get; }

    /// <summary>
    /// Number of passed tests
    /// </summary>
    public int PassedTests { get; }

    /// <summary>
    /// Number of failed tests
    /// </summary>
    public int FailedTests { get; }

    /// <summary>
    /// Number of skipped tests
    /// </summary>
    public int SkippedTests { get; }

    /// <summary>
    /// Number of flaky tests
    /// </summary>
    public int FlakyTests { get; }

    /// <summary>
    /// Timestamp of the last test run
    /// </summary>
    public DateTime LastRunAt { get; }

    /// <summary>
    /// Overall test suite status ("pass", "warning", "fail")
    /// </summary>
    public string Status { get; }

    public E2EMetrics(
        decimal coverage,
        decimal passRate,
        decimal flakyRate,
        decimal executionTime,
        int totalTests,
        int passedTests,
        int failedTests,
        int skippedTests,
        int flakyTests,
        DateTime lastRunAt,
        string status)
    {
        Guard.AgainstOutOfRange(coverage, nameof(coverage), QualityThresholds.MinimumPercentage, QualityThresholds.MaximumPercentage);
        Guard.AgainstOutOfRange(passRate, nameof(passRate), QualityThresholds.MinimumPercentage, QualityThresholds.MaximumPercentage);
        Guard.AgainstOutOfRange(flakyRate, nameof(flakyRate), QualityThresholds.MinimumPercentage, QualityThresholds.MaximumPercentage);
        Guard.AgainstNegative(executionTime, nameof(executionTime));
        Guard.AgainstNegative(totalTests, nameof(totalTests));
        Guard.AgainstNegative(passedTests, nameof(passedTests));
        Guard.AgainstNegative(failedTests, nameof(failedTests));
        Guard.AgainstNegative(skippedTests, nameof(skippedTests));
        Guard.AgainstNegative(flakyTests, nameof(flakyTests));
        Guard.AgainstNullOrWhiteSpace(status, nameof(status));

        Coverage = coverage;
        PassRate = passRate;
        FlakyRate = flakyRate;
        ExecutionTime = executionTime;
        TotalTests = totalTests;
        PassedTests = passedTests;
        FailedTests = failedTests;
        SkippedTests = skippedTests;
        FlakyTests = flakyTests;
        LastRunAt = lastRunAt;
        Status = status;
    }

    /// <summary>
    /// Determines if E2E metrics meet quality standards.
    /// Coverage &gt;= 90%, Pass rate &gt;= 95%, Flaky rate &lt;= 5%.
    /// </summary>
    public bool MeetsQualityStandards =>
        Coverage >= QualityThresholds.MinimumCoverage &&
        PassRate >= QualityThresholds.MinimumPassRate &&
        FlakyRate <= QualityThresholds.MaximumFlakyRate;

    /// <summary>
    /// Creates an empty E2EMetrics instance for initial state.
    /// </summary>
    public static E2EMetrics Empty() => new(
        coverage: 0m,
        passRate: 0m,
        flakyRate: 0m,
        executionTime: 0m,
        totalTests: 0,
        passedTests: 0,
        failedTests: 0,
        skippedTests: 0,
        flakyTests: 0,
        lastRunAt: DateTime.MinValue,
        status: "pending");

    /// <summary>
    /// Creates E2EMetrics from raw test results.
    /// </summary>
    /// <param name="passedTests">Number of tests that passed</param>
    /// <param name="failedTests">Number of tests that failed</param>
    /// <param name="skippedTests">Number of tests that were skipped</param>
    /// <param name="flakyTests">Number of tests identified as flaky</param>
    /// <param name="executionTimeMs">Total execution time in milliseconds</param>
    /// <param name="coveragePercent">Code coverage percentage (0-100)</param>
    /// <returns>Calculated E2EMetrics instance</returns>
    public static E2EMetrics FromTestResults(
        int passedTests,
        int failedTests,
        int skippedTests,
        int flakyTests,
        decimal executionTimeMs,
        decimal coveragePercent)
    {
        var totalTests = passedTests + failedTests + skippedTests;
        var passRate = totalTests > 0 ? (decimal)passedTests / totalTests * 100 : 0m;
        var flakyRate = totalTests > 0 ? (decimal)flakyTests / totalTests * 100 : 0m;

        var status = failedTests > 0 ? "fail"
            : flakyTests > 0 ? "warning"
            : "pass";

        return new E2EMetrics(
            coverage: coveragePercent,
            passRate: passRate,
            flakyRate: flakyRate,
            executionTime: executionTimeMs,
            totalTests: totalTests,
            passedTests: passedTests,
            failedTests: failedTests,
            skippedTests: skippedTests,
            flakyTests: flakyTests,
            lastRunAt: DateTime.UtcNow,
            status: status);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Coverage;
        yield return PassRate;
        yield return FlakyRate;
        yield return ExecutionTime;
        yield return TotalTests;
        yield return PassedTests;
        yield return FailedTests;
        yield return SkippedTests;
        yield return FlakyTests;
        yield return LastRunAt;
        yield return Status;
    }

    public override string ToString() =>
        $"E2E: Coverage={Coverage:F1}%, Pass={PassRate:F1}%, Flaky={FlakyRate:F1}%, Execution={ExecutionTime}ms, Status={Status}";
}
