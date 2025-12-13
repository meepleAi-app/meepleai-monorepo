using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value object for accessibility testing metrics (Issue #2139)
/// Represents Lighthouse and axe-core accessibility scores
/// </summary>
public sealed class AccessibilityMetrics : ValueObject
{
    /// <summary>
    /// Lighthouse accessibility score (0-100)
    /// </summary>
    public decimal LighthouseScore { get; }

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
    /// Status of accessibility tests ("pass", "warning", "fail")
    /// </summary>
    public string Status { get; }

    public AccessibilityMetrics(
        decimal lighthouseScore,
        int axeViolations,
        IReadOnlyList<string> wcagLevels,
        DateTime lastRunAt,
        string status)
    {
        if (lighthouseScore < 0 || lighthouseScore > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(lighthouseScore), "Lighthouse score must be between 0 and 100");
        }

        if (axeViolations < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(axeViolations), "Axe violations cannot be negative");
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Status cannot be empty", nameof(status));
        }

        LighthouseScore = lighthouseScore;
        AxeViolations = axeViolations;
        WcagLevels = wcagLevels ?? throw new ArgumentNullException(nameof(wcagLevels));
        LastRunAt = lastRunAt;
        Status = status;
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
        $"Accessibility: Lighthouse={LighthouseScore:F1}, Violations={AxeViolations}, WCAG={string.Join(",", WcagLevels)}, Status={Status}";
}
