namespace Api.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Configuration for the copyright leak guard (#447).
/// Bound from "Copyright" section of appsettings.json.
/// </summary>
internal sealed class CopyrightLeakGuardOptions
{
    /// <summary>
    /// Minimum number of consecutive words that must match a Protected chunk
    /// to flag as verbatim leak. Default 12 (≈ one sentence in IT/EN).
    /// Must be &gt;= 3 to avoid trivial false positives.
    /// </summary>
    public int VerbatimRunThreshold { get; set; } = 12;

    /// <summary>
    /// Maximum milliseconds allowed for a single scan before cancellation.
    /// Default 500ms. Must be &gt; 0.
    /// </summary>
    public int ScanTimeoutMs { get; set; } = 500;

    /// <summary>
    /// Reserved for #448 — future failure mode switching.
    /// Currently only "FailOpen" is implemented.
    /// </summary>
    public string FailureMode { get; set; } = "FailOpen";

    /// <summary>
    /// Reserved for #448 — future recovery strategy switching.
    /// Currently only "FallbackCanned" is implemented.
    /// </summary>
    public string RecoveryAction { get; set; } = "FallbackCanned";
}
