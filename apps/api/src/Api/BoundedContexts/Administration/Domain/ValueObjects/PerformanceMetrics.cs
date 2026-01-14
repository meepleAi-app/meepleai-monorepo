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

    public PerformanceMetrics(
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
    /// Determines if performance metrics meet Core Web Vitals thresholds.
    /// LCP &lt;= 2500ms, FID &lt;= 100ms, CLS &lt;= 0.1, Performance Score &gt;= 90.
    /// </summary>
    public bool MeetsCoreWebVitals =>
        Lcp <= WebVitalThresholds.LcpGoodThreshold &&
        Fid <= WebVitalThresholds.FidGoodThreshold &&
        Cls <= WebVitalThresholds.ClsGoodThreshold &&
        PerformanceScore >= WebVitalThresholds.MinimumPerformanceScore;

    /// <summary>
    /// Creates an empty PerformanceMetrics instance for initial state.
    /// </summary>
    public static PerformanceMetrics Empty() => new(
        lcp: 0m,
        fid: 0m,
        cls: 0m,
        fcp: 0m,
        tti: 0m,
        tbt: 0m,
        speedIndex: 0m,
        performanceScore: 0m,
        budgetStatus: "pending",
        lastRunAt: DateTime.MinValue);

    /// <summary>
    /// Creates PerformanceMetrics from Lighthouse report data.
    /// </summary>
    /// <param name="lcpMs">Largest Contentful Paint in milliseconds</param>
    /// <param name="fidMs">First Input Delay in milliseconds</param>
    /// <param name="cls">Cumulative Layout Shift score</param>
    /// <param name="fcpMs">First Contentful Paint in milliseconds</param>
    /// <param name="ttiMs">Time to Interactive in milliseconds</param>
    /// <param name="tbtMs">Total Blocking Time in milliseconds</param>
    /// <param name="speedIndex">Speed Index score</param>
    /// <param name="performanceScore">Lighthouse performance score (0-100)</param>
    /// <returns>PerformanceMetrics instance with calculated budget status</returns>
    public static PerformanceMetrics FromLighthouseReport(
        decimal lcpMs,
        decimal fidMs,
        decimal cls,
        decimal fcpMs,
        decimal ttiMs,
        decimal tbtMs,
        decimal speedIndex,
        decimal performanceScore)
    {
        var budgetStatus = performanceScore >= WebVitalThresholds.MinimumPerformanceScore
            && lcpMs <= WebVitalThresholds.LcpGoodThreshold
            && fidMs <= WebVitalThresholds.FidGoodThreshold
            && cls <= WebVitalThresholds.ClsGoodThreshold
                ? "pass"
                : performanceScore >= 50 ? "warning" : "fail";

        return new PerformanceMetrics(
            lcp: lcpMs,
            fid: fidMs,
            cls: cls,
            fcp: fcpMs,
            tti: ttiMs,
            tbt: tbtMs,
            speedIndex: speedIndex,
            performanceScore: performanceScore,
            budgetStatus: budgetStatus,
            lastRunAt: DateTime.UtcNow);
    }

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
