// Issue #833 — Gamebook (libro-game) translation + OCR + glossary metrics.
// 7 metrics scoped to TranslateGamebookSegmentQueryHandler + SegmentGamebookPhotoCommandHandler.
//
// Circuit-breaker state for the LLM provider routing translation requests is already
// exported by MeepleAiMetrics.LlmOperational.cs (Issue #5480) — reused, not duplicated.
//
// Bulkhead policy (LlmBulkheadPolicy) and queue-depth gauge deferred to follow-up
// issue per spec-panel review 2026-05-18; needs 14-day post-launch traffic baseline
// to size correctly.
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    #region Gamebook Translation Metrics (Issue #833)

    /// <summary>
    /// Counter for total gamebook translation requests by status.
    /// Labels: status (success, failure, cancelled).
    /// </summary>
    public static readonly Counter<long> GamebookTranslationRequestsTotal = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.translation_requests_total",
        unit: "requests",
        description: "Total gamebook translation requests by status (success/failure/cancelled)");

    /// <summary>
    /// Histogram for gamebook translation latency in seconds.
    /// Labels: stage (full = end-to-end wall-clock, streaming = time-to-first-chunk).
    /// </summary>
    public static readonly Histogram<double> GamebookTranslationLatencySeconds = Meter.CreateHistogram<double>(
        name: "meepleai.gamebook.translation_latency_seconds",
        unit: "s",
        description: "Gamebook translation latency in seconds, labeled by stage (full/streaming)");

    /// <summary>
    /// Histogram for token counts consumed by translation requests.
    /// Labels: kind (prompt = system+user input tokens, completion = generated output tokens).
    /// </summary>
    public static readonly Histogram<long> GamebookTranslationTokenCount = Meter.CreateHistogram<long>(
        name: "meepleai.gamebook.translation_token_count",
        unit: "tokens",
        description: "Token count consumed by gamebook translation (prompt vs completion)");

    /// <summary>
    /// Counter for cumulative translation cost in EUR.
    /// Labels: provider. EUR derived from USD via fixed rate constant <see cref="UsdToEurRate"/>.
    /// </summary>
    public static readonly Counter<double> GamebookTranslationCostEur = Meter.CreateCounter<double>(
        name: "meepleai.gamebook.translation_cost_eur",
        unit: "EUR",
        description: "Cumulative gamebook translation cost in EUR (USD x fixed rate, see spec)");

    /// <summary>
    /// Histogram for OCR confidence score per segment (0.0 to 1.0).
    /// Labels: language (ita, eng, etc — Tesseract language code).
    /// </summary>
    public static readonly Histogram<double> GamebookOcrConfidenceScore = Meter.CreateHistogram<double>(
        name: "meepleai.gamebook.ocr_confidence_score",
        unit: "score",
        description: "OCR confidence score per gamebook segment (0.0-1.0), labeled by language");

    /// <summary>
    /// Counter for OCR segmentation match quality outcomes.
    /// Labels: quality (exact, partial, miss).
    /// </summary>
    public static readonly Counter<long> GamebookOcrSegmentationMatchQualityTotal = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.ocr_segmentation_match_quality_total",
        unit: "matches",
        description: "OCR segmentation match quality outcomes (exact/partial/miss)");

    /// <summary>
    /// Histogram for glossary term consistency rate per translation (0.0 to 1.0).
    /// Computed as: (translated text matches expected glossary mapping) / (total glossary terms applicable).
    /// Labels: campaign_id_hash (anonymized — first 8 chars of SHA-256 of campaign UUID).
    /// </summary>
    public static readonly Histogram<double> GamebookGlossaryConsistencyRate = Meter.CreateHistogram<double>(
        name: "meepleai.gamebook.glossary_consistency_rate",
        unit: "ratio",
        description: "Glossary term consistency rate per gamebook translation (0.0-1.0)");

    /// <summary>
    /// Fixed USD-to-EUR conversion rate for cost tracking accuracy.
    /// Per spec-panel decision 2026-05-18: runtime FX provider deferred to separate issue.
    /// Rate sourced from ECB monthly average for 2026-Q2 (approximate).
    /// Update with PR if rate drifts > 5% from observed mid-market.
    /// </summary>
    public const double UsdToEurRate = 0.92;

    /// <summary>
    /// Records a successful or failed gamebook translation request with all 4 translation metrics.
    /// </summary>
    /// <param name="status">"success" / "failure" / "cancelled"</param>
    /// <param name="latencyFullSeconds">End-to-end wall-clock duration in seconds</param>
    /// <param name="latencyStreamingSeconds">Time-to-first-chunk in seconds, or null if no streaming start</param>
    /// <param name="promptTokens">Tokens consumed by prompt (null when usage unavailable)</param>
    /// <param name="completionTokens">Tokens generated as completion (null when usage unavailable)</param>
    /// <param name="costUsd">Cost in USD reported by provider (null when unavailable)</param>
    /// <param name="provider">LLM provider name (e.g. "openrouter", "deepseek")</param>
    public static void RecordGamebookTranslationRequest(
        string status,
        double latencyFullSeconds,
        double? latencyStreamingSeconds,
        long? promptTokens,
        long? completionTokens,
        double? costUsd,
        string provider)
    {
        GamebookTranslationRequestsTotal.Add(1, new TagList { { "status", status } });
        GamebookTranslationLatencySeconds.Record(latencyFullSeconds, new TagList { { "stage", "full" } });

        if (latencyStreamingSeconds.HasValue)
        {
            GamebookTranslationLatencySeconds.Record(
                latencyStreamingSeconds.Value,
                new TagList { { "stage", "streaming" } });
        }

        if (promptTokens.HasValue)
        {
            GamebookTranslationTokenCount.Record(
                promptTokens.Value,
                new TagList { { "kind", "prompt" } });
        }

        if (completionTokens.HasValue)
        {
            GamebookTranslationTokenCount.Record(
                completionTokens.Value,
                new TagList { { "kind", "completion" } });
        }

        if (costUsd.HasValue && costUsd.Value > 0)
        {
            GamebookTranslationCostEur.Add(
                costUsd.Value * UsdToEurRate,
                new TagList { { "provider", provider } });
        }
    }

    /// <summary>
    /// Records OCR confidence + segmentation match quality from a gamebook photo segmentation pass.
    /// </summary>
    /// <param name="confidenceScores">Per-segment OCR confidence (0.0-1.0). One Record() per entry.</param>
    /// <param name="language">Tesseract language code (e.g. "ita", "eng")</param>
    /// <param name="exactMatches">Count of segmentation matches classified as exact</param>
    /// <param name="partialMatches">Count of segmentation matches classified as partial (fuzzy)</param>
    /// <param name="misses">Count of segmentation misses (no usable match)</param>
    public static void RecordGamebookOcrSegmentation(
        IReadOnlyCollection<double> confidenceScores,
        string language,
        long exactMatches,
        long partialMatches,
        long misses)
    {
        var languageTag = new TagList { { "language", language } };
        foreach (var score in confidenceScores)
        {
            GamebookOcrConfidenceScore.Record(score, languageTag);
        }

        if (exactMatches > 0)
        {
            GamebookOcrSegmentationMatchQualityTotal.Add(
                exactMatches,
                new TagList { { "quality", "exact" } });
        }
        if (partialMatches > 0)
        {
            GamebookOcrSegmentationMatchQualityTotal.Add(
                partialMatches,
                new TagList { { "quality", "partial" } });
        }
        if (misses > 0)
        {
            GamebookOcrSegmentationMatchQualityTotal.Add(
                misses,
                new TagList { { "quality", "miss" } });
        }
    }

    /// <summary>
    /// Records a glossary consistency observation for a completed translation.
    /// </summary>
    /// <param name="rate">Consistency rate 0.0-1.0 (translated_matches / total_applicable_terms)</param>
    /// <param name="campaignIdHash">Anonymized campaign id (first 8 chars of SHA-256)</param>
    public static void RecordGamebookGlossaryConsistency(double rate, string campaignIdHash)
    {
        GamebookGlossaryConsistencyRate.Record(
            rate,
            new TagList { { "campaign_id_hash", campaignIdHash } });
    }

    /// <summary>
    /// Counter for ownership/authorization failures during gamebook translation requests.
    /// Labels: reason (forbidden = wrong owner, not_found = missing campaign).
    /// Issue #1415.
    /// </summary>
    public static readonly Counter<long> GamebookTranslationAuthzFailuresTotal = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.translation_authz_failures_total",
        unit: "failures",
        description: "Total ownership/authorization failures during gamebook translation by reason (forbidden/not_found)");

    /// <summary>
    /// Counter for DB preflight timeouts in the campaign ownership guard.
    /// Increments when the 2-second timeout in <c>CampaignOwnershipGuard.AssertOwnedByAsync</c> trips.
    /// Issue #1415.
    /// </summary>
    public static readonly Counter<long> GamebookTranslationPreflightTimeoutTotal = Meter.CreateCounter<long>(
        name: "meepleai.gamebook.translation_preflight_timeout_total",
        unit: "timeouts",
        description: "Total preflight DB timeouts in the campaign ownership guard");

    /// <summary>
    /// Records an ownership/authorization failure observed during gamebook translation.
    /// Issue #1415.
    /// </summary>
    /// <param name="reason">"forbidden" (wrong owner) or "not_found" (missing campaign)</param>
    public static void RecordGamebookTranslationAuthzFailure(string reason)
    {
        GamebookTranslationAuthzFailuresTotal.Add(1, new TagList { { "reason", reason } });
    }

    /// <summary>
    /// Records a preflight DB timeout in the campaign ownership guard. Issue #1415.
    /// </summary>
    public static void RecordGamebookTranslationPreflightTimeout()
    {
        GamebookTranslationPreflightTimeoutTotal.Add(1);
    }

    #endregion
}
