using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value object for accessibility testing metrics (Issue #2139)
/// Represents Lighthouse and axe-core accessibility scores
/// </summary>
internal sealed class AccessibilityMetrics : ValueObject
{
    /// <summary>
    /// Lighthouse accessibility score (0-100).
    /// Type-safe Percentage ensures valid range.
    /// </summary>
    public Percentage LighthouseScore { get; }

    /// <summary>
    /// Number of axe-core violations detected
    /// </summary>
    public int AxeViolations { get; }

    /// <summary>
    /// WCAG compliance levels passed (e.g., "A", "AA", "AAA")
    /// </summary>
    public IReadOnlyList<string> WcagLevels { get; }

    /// <summary>
    /// Timestamp of the last accessibility test run
    /// </summary>
    public DateTime LastRunAt { get; }

    /// <summary>
    /// Status of accessibility tests (Pass, Warning, Fail, NoData).
    /// Indicates whether accessibility tests meet quality standards.
    /// </summary>
    public TestExecutionStatus Status { get; }

    public AccessibilityMetrics(
        Percentage lighthouseScore,
        int axeViolations,
        IReadOnlyList<string> wcagLevels,
        DateTime lastRunAt,
        TestExecutionStatus status)
    {
        // Percentage validation is handled by Percentage.Create() - type-safe by design
        if (axeViolations < 0)
        {
            throw new ValidationException("Axe violations cannot be negative");
        }

        LighthouseScore = lighthouseScore ?? throw new ArgumentNullException(nameof(lighthouseScore));
        AxeViolations = axeViolations;
        WcagLevels = wcagLevels ?? throw new ArgumentNullException(nameof(wcagLevels));
        LastRunAt = lastRunAt;
        Status = status;
    }

    /// <summary>
    /// Creates AccessibilityMetrics from raw values.
    /// Convenience factory method for creating instances from decimal scores.
    /// </summary>
    /// <param name="lighthouseScore">Lighthouse accessibility score (0-100)</param>
    /// <param name="axeViolations">Number of axe-core violations</param>
    /// <param name="wcagLevels">WCAG compliance levels passed</param>
    /// <param name="lastRunAt">Timestamp of the test run</param>
    /// <param name="status">Test execution status</param>
    /// <returns>New AccessibilityMetrics instance</returns>
    public static AccessibilityMetrics Create(
        decimal lighthouseScore,
        int axeViolations,
        IReadOnlyList<string> wcagLevels,
        DateTime lastRunAt,
        TestExecutionStatus status)
    {
        return new AccessibilityMetrics(
            Percentage.Create(lighthouseScore),
            axeViolations,
            wcagLevels,
            lastRunAt,
            status);
    }

    /// <summary>
    /// Creates default AccessibilityMetrics instance for testing or initialization.
    /// </summary>
    public static AccessibilityMetrics CreateDefault() => new(
        Percentage.Zero,
        0,
        Array.Empty<string>(),
        DateTime.UtcNow,
        TestExecutionStatus.NoData);

    /// <summary>
    /// Empty AccessibilityMetrics instance with zero values.
    /// </summary>
    public static readonly AccessibilityMetrics Empty = CreateDefault();

    /// <summary>
    /// Determines if accessibility metrics meet quality standards
    /// (Lighthouse >= 90, no violations, WCAG AA minimum)
    /// </summary>
    public bool MeetsQualityStandards =>
        LighthouseScore >= 90 &&
        AxeViolations == 0 &&
        WcagLevels.Contains("AA", StringComparer.OrdinalIgnoreCase);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return LighthouseScore;
        yield return AxeViolations;
        yield return string.Join(",", WcagLevels);
        yield return LastRunAt;
        yield return Status;
    }

    public override string ToString() =>
        $"Accessibility: Lighthouse={LighthouseScore}, Violations={AxeViolations}, WCAG={string.Join(",", WcagLevels)}, Status={Status}";
}
