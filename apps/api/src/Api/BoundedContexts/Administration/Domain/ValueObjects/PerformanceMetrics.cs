using Api.SharedKernel.Constants;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Enums;
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
    /// Performance budget compliance status (Pass, Warning, Fail).
    /// Indicates whether performance metrics meet defined budget thresholds.
    /// </summary>
    public PerformanceBudgetStatus BudgetStatus { get; }

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
        PerformanceBudgetStatus budgetStatus,
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
        // Note: No validation needed for enum - type-safe by design

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
