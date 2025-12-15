using Api.SharedKernel.Domain.ValueObjects;

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
        if (coverage < 0 || coverage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(coverage), "Coverage must be between 0 and 100");
        }

        if (passRate < 0 || passRate > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(passRate), "Pass rate must be between 0 and 100");
        }

        if (flakyRate < 0 || flakyRate > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(flakyRate), "Flaky rate must be between 0 and 100");
        }

        if (totalTests < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTests), "Total tests cannot be negative");
        }

        if (passedTests < 0) throw new ArgumentOutOfRangeException(nameof(passedTests), "Passed tests cannot be negative");
        if (failedTests < 0) throw new ArgumentOutOfRangeException(nameof(failedTests), "Failed tests cannot be negative");
        if (skippedTests < 0) throw new ArgumentOutOfRangeException(nameof(skippedTests), "Skipped tests cannot be negative");
        if (flakyTests < 0) throw new ArgumentOutOfRangeException(nameof(flakyTests), "Flaky tests cannot be negative");
        if (executionTime < 0) throw new ArgumentOutOfRangeException(nameof(executionTime), "Execution time cannot be negative");

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status cannot be empty", nameof(status));
        }

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
    /// Determines if E2E metrics meet quality standards
    /// (Coverage >= 90%, Pass rate >= 95%, Flaky rate <= 5%)
    /// </summary>
    public bool MeetsQualityStandards =>
        Coverage >= 90 &&
        PassRate >= 95 &&
        FlakyRate <= 5;

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
