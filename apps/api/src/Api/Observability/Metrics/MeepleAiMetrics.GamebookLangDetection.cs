// Issue #1559 — NTextCat lang detection metrics partial.
// Counter/histogram naming follows the repo convention `meepleai.<domain>.<metric>` (snake_dot lowercased).
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region Gamebook Lang Detection Metrics (Issue #1559)

    /// <summary>
    /// Counter for NTextCat detection failures (library crashes caught by service).
    /// Labels: reason (exception type name, e.g. "InvalidOperationException").
    /// </summary>
    private static readonly Counter<long> GamebookLangDetectionFailures = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.lang_detection.failures",
        unit: "{failure}",
        description: "Count of NTextCat detection failures (library crashes caught by service)");

    /// <summary>
    /// Histogram for distribution of NLP confidence scores (0..1) from NTextCat lang detection.
    /// Labels: lang (detected ISO 639-1 code or "out_of_allowlist").
    /// </summary>
    private static readonly Histogram<double> GamebookLangDetectionConfidence = Meter.CreateHistogram<double>(
        name: "meepleai.gamebook.lang_detection.confidence",
        unit: "1",
        description: "Distribution of NLP confidence scores (0..1) from NTextCat lang detection");

    /// <summary>
    /// Counter for lang detection outcomes by detected lang code.
    /// Labels: lang (EN/FR/DE/ES/IT/out_of_allowlist).
    /// </summary>
    private static readonly Counter<long> GamebookLangDetectionResults = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.lang_detection.results",
        unit: "{detection}",
        description: "Lang detection outcomes by detected lang code (EN/FR/DE/ES/IT/out_of_allowlist=null)");

    /// <summary>
    /// Records a NTextCat detection failure (library crash caught by the service layer).
    /// Call this from inside the catch block in SegmentGamebookPhotoCommandHandler before
    /// assigning the safe-default LanguageDetectionResult(null, 0.0).
    /// </summary>
    /// <param name="reason">Exception type name (ex.GetType().Name).</param>
    public static void RecordGamebookLangDetectionFailure(string reason)
    {
        GamebookLangDetectionFailures.Add(1, new KeyValuePair<string, object?>("reason", reason));
    }

    /// <summary>
    /// Records a successful lang detection result (called on the happy path after Detect()).
    /// Also records the confidence histogram observation.
    /// </summary>
    /// <param name="detectedLang">
    /// Detected ISO 639-1 uppercase code (EN/FR/DE/ES/IT), or null when NTextCat returned a
    /// language outside the project allowlist. Null is recorded as "out_of_allowlist".
    /// </param>
    /// <param name="confidence">Confidence score in the [0, 1] range.</param>
    public static void RecordGamebookLangDetectionResult(string? detectedLang, double confidence)
    {
        var langTag = detectedLang ?? "out_of_allowlist";
        GamebookLangDetectionResults.Add(1, new KeyValuePair<string, object?>("lang", langTag));
        GamebookLangDetectionConfidence.Record(confidence, new KeyValuePair<string, object?>("lang", langTag));
    }

    #endregion
}
