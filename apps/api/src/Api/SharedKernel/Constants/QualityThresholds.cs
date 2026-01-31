namespace Api.SharedKernel.Constants;

/// <summary>
/// Quality standard thresholds for E2E testing metrics.
/// Based on MeepleAI's quality standards (ADR-xxx).
/// </summary>
internal static class QualityThresholds
{
    /// <summary>
    /// Minimum test coverage percentage required (industry best practice for critical systems).
    /// </summary>
    public const decimal MinimumCoverage = 90;

    /// <summary>
    /// Minimum pass rate percentage required (allows 5% for known flaky tests under investigation).
    /// </summary>
    public const decimal MinimumPassRate = 95;

    /// <summary>
    /// Maximum flaky test rate percentage allowed (acceptable threshold before CI becomes unreliable).
    /// </summary>
    public const decimal MaximumFlakyRate = 5;

    /// <summary>
    /// Warning pass rate threshold (below MinimumPassRate but acceptable temporarily during investigation).
    /// Used to indicate degraded quality that needs attention.
    /// </summary>
    public const decimal WarningPassRate = 80;

    /// <summary>
    /// Warning flaky rate threshold (above MaximumFlakyRate but not critical).
    /// Used to indicate increased instability requiring investigation.
    /// </summary>
    public const decimal WarningFlakyRate = 10;

    /// <summary>
    /// Minimum percentage value (0%).
    /// </summary>
    public const decimal MinimumPercentage = 0;

    /// <summary>
    /// Maximum percentage value (100%).
    /// </summary>
    public const decimal MaximumPercentage = 100;
}
