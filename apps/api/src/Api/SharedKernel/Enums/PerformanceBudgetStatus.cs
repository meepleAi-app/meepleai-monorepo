namespace Api.SharedKernel.Enums;

/// <summary>
/// Represents the performance budget compliance status.
/// Used to indicate whether performance metrics meet defined budget thresholds.
/// </summary>
/// <remarks>
/// Performance budgets define acceptable limits for metrics like:
/// <list type="bullet">
///   <item><description>JavaScript bundle size</description></item>
///   <item><description>CSS file size</description></item>
///   <item><description>Image optimization</description></item>
///   <item><description>Time to Interactive (TTI)</description></item>
///   <item><description>First Contentful Paint (FCP)</description></item>
/// </list>
/// Status is determined by comparing actual metrics against budget thresholds.
/// </remarks>
internal enum PerformanceBudgetStatus
{
    /// <summary>
    /// Performance meets all budget targets.
    /// All metrics are within acceptable limits.
    /// </summary>
    Pass = 0,

    /// <summary>
    /// Performance approaching budget limits.
    /// Some metrics are near thresholds but haven't exceeded them yet.
    /// Action recommended to prevent future budget violations.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Performance exceeds budget limits.
    /// One or more metrics have exceeded acceptable thresholds.
    /// Immediate action required to optimize performance.
    /// </summary>
    Fail = 2,

    /// <summary>
    /// No performance data available.
    /// Typically occurs when performance tests haven't been run yet.
    /// </summary>
    NoData = 3
}
