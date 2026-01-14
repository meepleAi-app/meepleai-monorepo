namespace Api.SharedKernel.Enums;

/// <summary>
/// Represents the execution status of test suites (E2E, Accessibility, Integration).
/// Used to indicate whether tests meet quality standards based on pass rates and flakiness.
/// </summary>
/// <remarks>
/// Status determination logic:
/// <list type="bullet">
///   <item><description><see cref="Pass"/>: Tests passed with excellent quality (≥95% pass rate, ≤5% flaky rate)</description></item>
///   <item><description><see cref="Warning"/>: Tests passed with warnings (80-95% pass rate, 5-10% flaky rate)</description></item>
///   <item><description><see cref="Fail"/>: Tests failed quality standards (less than 80% pass rate or more than 10% flaky)</description></item>
///   <item><description><see cref="NoData"/>: No test execution data available yet</description></item>
/// </list>
/// </remarks>
internal enum TestExecutionStatus
{
    /// <summary>
    /// Tests passed with excellent quality metrics.
    /// Pass rate ≥ 95%, Flaky rate ≤ 5%.
    /// </summary>
    Pass = 0,

    /// <summary>
    /// Tests passed but with quality concerns.
    /// Pass rate 80-95%, Flaky rate 5-10%.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Tests failed to meet minimum quality standards.
    /// Pass rate less than 80% or Flaky rate more than 10%.
    /// </summary>
    Fail = 2,

    /// <summary>
    /// No test execution data available.
    /// Typically occurs when tests haven't been run yet.
    /// </summary>
    NoData = 3
}
