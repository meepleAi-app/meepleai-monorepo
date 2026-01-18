using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Enums;

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
        if (lighthouseScore < 0 || lighthouseScore > 100)
        {
            throw new ValidationException("Lighthouse score must be between 0 and 100");
        }

        if (axeViolations < 0)
        {
            throw new ValidationException("Axe violations cannot be negative");
        }

        // Note: No validation needed for status enum - type-safe by design

        LighthouseScore = lighthouseScore;
        AxeViolations = axeViolations;
        WcagLevels = wcagLevels ?? throw new ArgumentNullException(nameof(wcagLevels));
        LastRunAt = lastRunAt;
        Status = status;
    }

    /// <summary>
    /// Creates AccessibilityMetrics from test results.
    /// Uses Percentage ValueObject for type-safe score handling.
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
            lighthouseScore,
            axeViolations,
            wcagLevels,
            lastRunAt,
            status);
    }

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
