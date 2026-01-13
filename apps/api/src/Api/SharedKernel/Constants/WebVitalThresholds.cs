namespace Api.SharedKernel.Constants;

/// <summary>
/// Core Web Vitals thresholds based on Google's standards.
/// Reference: https://web.dev/vitals/
/// </summary>
internal static class WebVitalThresholds
{
    /// <summary>
    /// Largest Contentful Paint "Good" threshold (milliseconds).
    /// Google Core Web Vitals: LCP should occur within 2500ms of page load.
    /// </summary>
    public const decimal LcpGoodThreshold = 2500;

    /// <summary>
    /// First Input Delay "Good" threshold (milliseconds).
    /// Google Core Web Vitals: FID should be less than 100ms.
    /// </summary>
    public const decimal FidGoodThreshold = 100;

    /// <summary>
    /// Cumulative Layout Shift "Good" threshold (score).
    /// Google Core Web Vitals: CLS should be less than 0.1.
    /// </summary>
    public const decimal ClsGoodThreshold = 0.1m;

    /// <summary>
    /// Minimum Lighthouse performance score required.
    /// Lighthouse scoring: 90-100 is "Good" performance.
    /// </summary>
    public const decimal MinimumPerformanceScore = 90;
}
