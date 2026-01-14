using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Guards;

namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value object for performance testing metrics (Issue #2139)
/// Represents Core Web Vitals and performance budgets
/// </summary>
internal sealed class PerformanceMetrics : ValueObject
{
    /// <summary>
    /// Largest Contentful Paint (ms) - Core Web Vital
    /// </summary>
    public decimal Lcp { get; }

    /// <summary>
    /// First Input Delay (ms) - Core Web Vital
    /// </summary>
    public decimal Fid { get; }

    /// <summary>
    /// Cumulative Layout Shift - Core Web Vital
    /// </summary>
    public decimal Cls { get; }

    /// <summary>
    /// First Contentful Paint (ms)
    /// </summary>
    public decimal Fcp { get; }

    /// <summary>
    /// Time to Interactive (ms)
    /// </summary>
    public decimal Tti { get; }

    /// <summary>
    /// Total Blocking Time (ms)
    /// </summary>
    public decimal Tbt { get; }

    /// <summary>
    /// Speed Index
    /// </summary>
    public decimal SpeedIndex { get; }

    /// <summary>
    /// Lighthouse performance score (0-100)
    /// </summary>
    public decimal PerformanceScore { get; }

    /// <summary>
    /// Performance budget status ("pass", "warning", "fail")
    /// </summary>
    public string BudgetStatus { get; }

    /// <summary>
    /// Timestamp of the last performance test run
    /// </summary>
    public DateTime LastRunAt { get; }

    /// <summary>
    /// Private constructor for factory methods.
    /// Use FromLighthouseReport() or CreateDefault() to create instances.
    /// </summary>
    private PerformanceMetrics(
        decimal lcp,
        decimal fid,
        decimal cls,
        decimal fcp,
        decimal tti,
        decimal tbt,
        decimal speedIndex,
        decimal performanceScore,
        string budgetStatus,
        DateTime lastRunAt)
    {
        Guard.AgainstOutOfRange(performanceScore, nameof(performanceScore), QualityThresholds.MinimumPercentage, QualityThresholds.MaximumPercentage);
        Guard.AgainstNegative(lcp, nameof(lcp));
        Guard.AgainstNegative(fid, nameof(fid));
        Guard.AgainstNegative(cls, nameof(cls));
        Guard.AgainstNegative(fcp, nameof(fcp));
        Guard.AgainstNegative(tti, nameof(tti));
        Guard.AgainstNegative(tbt, nameof(tbt));
        Guard.AgainstNegative(speedIndex, nameof(speedIndex));
        Guard.AgainstNullOrWhiteSpace(budgetStatus, nameof(budgetStatus));

        Lcp = lcp;
        Fid = fid;
        Cls = cls;
        Fcp = fcp;
        Tti = tti;
        Tbt = tbt;
        SpeedIndex = speedIndex;
        PerformanceScore = performanceScore;
        BudgetStatus = budgetStatus;
        LastRunAt = lastRunAt;
    }

    /// <summary>
    /// Creates PerformanceMetrics from Lighthouse test results.
    /// Automatically calculates budget status based on Core Web Vitals thresholds.
    /// </summary>
    /// <param name="lcp">Largest Contentful Paint in milliseconds</param>
    /// <param name="fid">First Input Delay in milliseconds</param>
    /// <param name="cls">Cumulative Layout Shift score</param>
    /// <param name="fcp">First Contentful Paint in milliseconds</param>
    /// <param name="tti">Time to Interactive in milliseconds</param>
    /// <param name="tbt">Total Blocking Time in milliseconds</param>
    /// <param name="speedIndex">Speed Index score</param>
    /// <param name="performanceScore">Lighthouse performance score (0-100)</param>
    /// <param name="lastRunAt">Timestamp when the test run occurred</param>
    /// <returns>New PerformanceMetrics instance with calculated budget status</returns>
    public static PerformanceMetrics FromLighthouseReport(
        decimal lcp,
        decimal fid,
        decimal cls,
        decimal fcp,
        decimal tti,
        decimal tbt,
        decimal speedIndex,
        decimal performanceScore,
        DateTime lastRunAt)
    {
        Guard.AgainstNegative(lcp, nameof(lcp));
        Guard.AgainstNegative(fid, nameof(fid));
        Guard.AgainstNegative(cls, nameof(cls));
        Guard.AgainstNegative(fcp, nameof(fcp));
        Guard.AgainstNegative(tti, nameof(tti));
        Guard.AgainstNegative(tbt, nameof(tbt));
        Guard.AgainstNegative(speedIndex, nameof(speedIndex));
        Guard.AgainstOutOfRange(performanceScore, nameof(performanceScore), QualityThresholds.MinimumPercentage, QualityThresholds.MaximumPercentage);

        // Calculate budget status based on Core Web Vitals
        var budgetStatus = CalculateBudgetStatus(lcp, fid, cls, performanceScore);

        return new PerformanceMetrics(
            lcp: lcp,
            fid: fid,
            cls: cls,
            fcp: fcp,
            tti: tti,
            tbt: tbt,
            speedIndex: speedIndex,
            performanceScore: performanceScore,
            budgetStatus: budgetStatus,
            lastRunAt: lastRunAt);
    }

    /// <summary>
    /// Creates default PerformanceMetrics instance for testing or initialization.
    /// All metrics are set to zero and budget status is "unknown".
    /// </summary>
    public static PerformanceMetrics CreateDefault() => new(
        lcp: 0m,
        fid: 0m,
        cls: 0m,
        fcp: 0m,
        tti: 0m,
        tbt: 0m,
        speedIndex: 0m,
        performanceScore: 0m,
        budgetStatus: "unknown",
        lastRunAt: DateTime.UtcNow);

    /// <summary>
    /// Empty PerformanceMetrics instance with zero values.
    /// </summary>
    public static readonly PerformanceMetrics Empty = CreateDefault();

    /// <summary>
    /// Calculates performance budget status based on Core Web Vitals thresholds.
    /// Pass: All Core Web Vitals meet good thresholds (LCP under 2500ms, FID under 100ms, CLS under 0.1, Score over 90)
    /// Warning: All Core Web Vitals meet needs-improvement thresholds (LCP under 4000ms, FID under 300ms, CLS under 0.25, Score over 50)
    /// Fail: Otherwise
    /// </summary>
    private static string CalculateBudgetStatus(decimal lcp, decimal fid, decimal cls, decimal performanceScore)
    {
        // Pass: All metrics meet "good" thresholds
        if (lcp <= WebVitalThresholds.LcpGoodThreshold &&
            fid <= WebVitalThresholds.FidGoodThreshold &&
            cls <= WebVitalThresholds.ClsGoodThreshold &&
            performanceScore >= WebVitalThresholds.MinimumPerformanceScore)
            return "pass";

        // Warning: All metrics meet "needs improvement" thresholds
        if (lcp <= WebVitalThresholds.LcpNeedsImprovementThreshold &&
            fid <= WebVitalThresholds.FidNeedsImprovementThreshold &&
            cls <= WebVitalThresholds.ClsNeedsImprovementThreshold &&
            performanceScore >= 50)
            return "warning";

        // Fail: At least one metric fails thresholds
        return "fail";
    }

    /// <summary>
    /// Determines if performance metrics meet Core Web Vitals thresholds.
    /// LCP &lt;= 2500ms, FID &lt;= 100ms, CLS &lt;= 0.1, Performance Score &gt;= 90.
    /// </summary>
    public bool MeetsCoreWebVitals =>
        Lcp <= WebVitalThresholds.LcpGoodThreshold &&
        Fid <= WebVitalThresholds.FidGoodThreshold &&
        Cls <= WebVitalThresholds.ClsGoodThreshold &&
        PerformanceScore >= WebVitalThresholds.MinimumPerformanceScore;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Lcp;
        yield return Fid;
        yield return Cls;
        yield return Fcp;
        yield return Tti;
        yield return Tbt;
        yield return SpeedIndex;
        yield return PerformanceScore;
        yield return BudgetStatus;
        yield return LastRunAt;
    }

    public override string ToString() =>
        $"Performance: Score={PerformanceScore:F1}, LCP={Lcp}ms, FID={Fid}ms, CLS={Cls:F3}, Budget={BudgetStatus}";
}
