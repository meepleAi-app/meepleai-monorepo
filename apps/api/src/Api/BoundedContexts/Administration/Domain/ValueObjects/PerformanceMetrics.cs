using Api.SharedKernel.Domain.ValueObjects;

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
        if (performanceScore < 0 || performanceScore > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(performanceScore), "Performance score must be between 0 and 100");
        }

        if (lcp < 0) throw new ArgumentOutOfRangeException(nameof(lcp), "LCP cannot be negative");
        if (fid < 0) throw new ArgumentOutOfRangeException(nameof(fid), "FID cannot be negative");
        if (cls < 0) throw new ArgumentOutOfRangeException(nameof(cls), "CLS cannot be negative");
        if (fcp < 0) throw new ArgumentOutOfRangeException(nameof(fcp), "FCP cannot be negative");
        if (tti < 0) throw new ArgumentOutOfRangeException(nameof(tti), "TTI cannot be negative");
        if (tbt < 0) throw new ArgumentOutOfRangeException(nameof(tbt), "TBT cannot be negative");
        if (speedIndex < 0) throw new ArgumentOutOfRangeException(nameof(speedIndex), "Speed Index cannot be negative");

        if (string.IsNullOrWhiteSpace(budgetStatus))
        {
            throw new ArgumentException("Budget status cannot be empty", nameof(budgetStatus));
        }

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
    /// Determines if performance metrics meet Core Web Vitals thresholds
    /// LCP <= 2500ms, FID <= 100ms, CLS <= 0.1, Performance Score >= 90
    /// </summary>
    public bool MeetsCoreWebVitals =>
        Lcp <= 2500 &&
        Fid <= 100 &&
        Cls <= 0.1m &&
        PerformanceScore >= 90;

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
