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

    /// <summary>
    /// Private constructor for factory methods.
    /// Use FromTestRun() or CreateDefault() to create instances.
    /// </summary>
    private E2EMetrics(
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
    /// Creates E2EMetrics from Playwright test run results.
    /// Automatically calculates coverage, pass rate, flaky rate, and status.
    /// </summary>
    /// <param name="totalTests">Total number of tests executed</param>
    /// <param name="passedTests">Number of tests that passed</param>
    /// <param name="failedTests">Number of tests that failed</param>
    /// <param name="skippedTests">Number of tests that were skipped</param>
    /// <param name="flakyTests">Number of tests that exhibited flaky behavior</param>
    /// <param name="durationMs">Total execution duration in milliseconds</param>
    /// <param name="lastRunAt">Timestamp when the test run occurred</param>
    /// <returns>New E2EMetrics instance with calculated metrics</returns>
    public static E2EMetrics FromTestRun(
        int totalTests,
        int passedTests,
        int failedTests,
        int skippedTests,
        int flakyTests,
        long durationMs,
        DateTime lastRunAt)
    {
        Guard.AgainstNegative(totalTests, nameof(totalTests));
        Guard.AgainstNegative(passedTests, nameof(passedTests));
        Guard.AgainstNegative(failedTests, nameof(failedTests));
        Guard.AgainstNegative(skippedTests, nameof(skippedTests));
        Guard.AgainstNegative(flakyTests, nameof(flakyTests));
        Guard.AgainstNegative(durationMs, nameof(durationMs));

        // Calculate coverage (placeholder logic - will be replaced with actual coverage data from test framework)
        var coverage = totalTests > 0 ? (decimal)passedTests / totalTests * 100 : 0m;

        // Calculate pass rate
        var passRate = totalTests > 0 ? (decimal)passedTests / totalTests * 100 : 0m;

        // Calculate flaky rate
        var flakyRate = totalTests > 0 ? (decimal)flakyTests / totalTests * 100 : 0m;

        // Calculate average execution time
        var executionTime = totalTests > 0 ? (decimal)durationMs / totalTests : 0m;

        // Determine status based on quality thresholds
        var status = CalculateStatus(passRate, flakyRate);

        return new E2EMetrics(
            coverage: coverage,
            passRate: passRate,
            flakyRate: flakyRate,
            executionTime: executionTime,
            totalTests: totalTests,
            passedTests: passedTests,
            failedTests: failedTests,
            skippedTests: skippedTests,
            flakyTests: flakyTests,
            lastRunAt: lastRunAt,
            status: status);
    }

    /// <summary>
    /// Creates default E2EMetrics instance for testing or initialization.
    /// All metrics are set to zero and status is "unknown".
    /// </summary>
    public static E2EMetrics CreateDefault() => new(
        coverage: 0m,
        passRate: 0m,
        flakyRate: 0m,
        executionTime: 0m,
        totalTests: 0,
        passedTests: 0,
        failedTests: 0,
        skippedTests: 0,
        flakyTests: 0,
        lastRunAt: DateTime.UtcNow,
        status: "unknown");

    /// <summary>
    /// Empty E2EMetrics instance with zero values.
    /// </summary>
    public static readonly E2EMetrics Empty = CreateDefault();

    /// <summary>
    /// Calculates test suite status based on pass rate and flaky rate.
    /// Pass: PassRate greater than or equal to 95% AND FlakyRate less than or equal to 5%
    /// Warning: PassRate greater than or equal to 80% AND FlakyRate less than or equal to 10%
    /// Fail: Otherwise
    /// </summary>
    private static string CalculateStatus(decimal passRate, decimal flakyRate)
    {
        if (passRate >= QualityThresholds.MinimumPassRate && flakyRate <= QualityThresholds.MaximumFlakyRate)
            return "pass";
        if (passRate >= QualityThresholds.WarningPassRate && flakyRate <= QualityThresholds.WarningFlakyRate)
            return "warning";
        return "fail";
    }

    /// <summary>
    /// Determines if E2E test metrics meet MeepleAI quality standards.
    /// </summary>
    /// <remarks>
    /// Quality standards defined in ADR-006 (Multi-Layer Validation Architecture):
    /// <list type="bullet">
    /// <item><description>Coverage: &gt;= 90% (industry best practice for critical systems, ensures comprehensive test coverage)</description></item>
    /// <item><description>Pass Rate: &gt;= 95% (allows 5% tolerance for known flaky tests under investigation)</description></item>
    /// <item><description>Flaky Rate: &lt;= 5% (acceptable threshold before CI becomes unreliable, prevents false positives)</description></item>
    /// </list>
    /// These thresholds balance rigorous quality enforcement with practical CI/CD workflow requirements.
    /// </remarks>
    /// <returns>
    /// <c>true</c> if all quality thresholds are met; otherwise <c>false</c>.
    /// </returns>
    /// <example>
    /// <code>
    /// var metrics = E2EMetrics.FromTestRun(100, 98, 2, 0, 3, 1500);
    /// if (!metrics.MeetsQualityStandards)
    ///     logger.LogWarning("E2E quality not met: Coverage={Coverage}%, Pass={PassRate}%, Flaky={FlakyRate}%",
    ///         metrics.Coverage, metrics.PassRate, metrics.FlakyRate);
    /// </code>
    /// </example>
    public bool MeetsQualityStandards =>
        Coverage >= QualityThresholds.MinimumCoverage &&
        PassRate >= QualityThresholds.MinimumPassRate &&
        FlakyRate <= QualityThresholds.MaximumFlakyRate;

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
